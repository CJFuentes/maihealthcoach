using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Api.Features.Coach;

/// <summary>
/// Per-user rate limiting for the coach chat send endpoint (issue #39). Partitions a fixed-window
/// limiter by the authenticated subject (<c>sub</c> claim) so each user has an independent budget,
/// and rejects over-budget requests with a standards-compliant 429 (problem+json + Retry-After).
/// </summary>
internal static class ChatRateLimiting
{
    /// <summary>Name of the rate-limiting policy applied to the chat send endpoint.</summary>
    public const string ChatSendPolicyName = "coach-chat-send";

    /// <summary>
    /// Registers the rate limiter with a per-user fixed-window policy for the chat send endpoint.
    /// Options are resolved <em>per request</em> (not captured at registration) so test overrides
    /// applied via <c>UseSetting</c> take effect, and the partition key is read from the
    /// authenticated principal — hence <c>UseRateLimiter</c> must run after authentication.
    /// </summary>
    public static IServiceCollection AddCoachChatRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.AddPolicy(ChatSendPolicyName, httpContext =>
            {
                var opts = httpContext.RequestServices
                    .GetRequiredService<IOptions<CoachChatOptions>>().Value;

                var sub = httpContext.User.FindFirstValue("sub");
                if (string.IsNullOrEmpty(sub))
                {
                    // Unauthenticated requests never reach a limited handler (the endpoint requires
                    // authorization), but fail open rather than partitioning everyone into one bucket.
                    return RateLimitPartition.GetNoLimiter("anon");
                }

                return RateLimitPartition.GetFixedWindowLimiter(sub, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opts.PermitLimit,
                    Window = TimeSpan.FromSeconds(opts.WindowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });

            limiterOptions.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                // Pass the content type to WriteAsJsonAsync's contentType parameter: the generic
                // overload otherwise forces "application/json", overwriting a pre-set ContentType.
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        type = "https://tools.ietf.org/html/rfc6585#section-4",
                        title = "Too many requests.",
                        status = 429,
                        detail = "You have sent too many chat messages. Please wait and try again.",
                    },
                    options: null,
                    contentType: "application/problem+json",
                    ct);
            };
        });

        return services;
    }
}
