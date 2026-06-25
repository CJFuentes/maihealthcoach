using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Api.RateLimiting;

/// <summary>
/// API rate limiting and abuse protection (issue #45). Configures a single
/// <see cref="Microsoft.AspNetCore.RateLimiting"/> setup with two layers:
/// <list type="number">
///   <item><description>
///   A <strong>global limiter</strong> applied to every request, partitioned by the authenticated
///   user (the <c>sub</c> claim) when present, else by client IP — so anonymous abuse is bounded
///   too. Health-check and OpenAPI paths are exempt so probes and docs are never throttled into
///   failure.
///   </description></item>
///   <item><description>
///   A stricter named policy (<see cref="CoachPolicyName"/>) for the expensive coach LLM endpoints
///   (chat / meal-suggestions / nudge) which hit Anthropic and are the prime abuse target. The
///   per-user budget is driven by <see cref="CoachChatOptions"/>, <strong>consolidating</strong>
///   issue #39's chat limiter so a single knob governs the coach per-user limit (and #39's
///   per-user-429 behaviour still holds).
///   </description></item>
/// </list>
/// Exceeding either limit returns a standards-compliant 429 (problem+json + <c>Retry-After</c>).
/// Options are resolved <em>per request</em> (not captured at registration) so test overrides applied
/// via <c>UseSetting</c> take effect, and partition keys are read from the authenticated principal —
/// hence <c>UseRateLimiter</c> must run after authentication.
/// </summary>
internal static class ApiRateLimiting
{
    /// <summary>Name of the rate-limiting policy applied to the coach LLM endpoints.</summary>
    public const string CoachPolicyName = "coach-llm";

    /// <summary>
    /// Registers the rate limiter with a per-partition global limiter and the named coach policy.
    /// </summary>
    public static IServiceCollection AddApiRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var opts = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                // Never throttle health probes or the OpenAPI document — these must stay available
                // even under load, and probes failing would take the service out of rotation.
                if (!opts.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                if (IsExempt(httpContext))
                {
                    return RateLimitPartition.GetNoLimiter("exempt");
                }

                var partitionKey = ResolvePartitionKey(httpContext);

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opts.GlobalPermitLimit,
                    Window = TimeSpan.FromSeconds(opts.GlobalWindowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
            });

            limiterOptions.AddPolicy(CoachPolicyName, httpContext =>
            {
                // Respect the master switch so disabling rate limiting turns off the coach policy
                // too — a true kill-switch rather than a global-only one.
                var rateLimitOpts = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                if (!rateLimitOpts.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                // The coach per-user budget reuses CoachChatOptions so it stays a single knob
                // consolidated with issue #39's chat limiter.
                var opts = httpContext.RequestServices
                    .GetRequiredService<IOptions<CoachChatOptions>>().Value;

                var sub = httpContext.User.FindFirstValue("sub");
                if (string.IsNullOrEmpty(sub))
                {
                    // Coach endpoints require authorization, so an unauthenticated request never
                    // reaches a limited handler. Fail open rather than collapsing every anonymous
                    // caller into one shared bucket.
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
                        detail = "You have made too many requests. Please wait and try again.",
                    },
                    options: null,
                    contentType: "application/problem+json",
                    ct);
            };
        });

        return services;
    }

    /// <summary>
    /// Health-check and OpenAPI paths are exempt from the global limiter so probes and docs are
    /// never rate-limited into failure.
    /// </summary>
    private static bool IsExempt(HttpContext httpContext)
    {
        var path = httpContext.Request.Path;
        return path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Partition the global limiter by the authenticated subject (<c>sub</c> claim) when present,
    /// else by the client IP so anonymous traffic is bounded too. Falls back to a single shared
    /// "unknown" bucket only when the IP cannot be determined.
    /// </summary>
    private static string ResolvePartitionKey(HttpContext httpContext)
    {
        var sub = httpContext.User.FindFirstValue("sub");
        if (!string.IsNullOrEmpty(sub))
        {
            return $"user:{sub}";
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrEmpty(ip) ? "ip:unknown" : $"ip:{ip}";
    }
}
