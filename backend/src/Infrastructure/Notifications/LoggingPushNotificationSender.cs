using MAIHealthCoach.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace MAIHealthCoach.Infrastructure.Notifications;

/// <summary>
/// The shipped default <see cref="IPushNotificationSender"/> (issue #48): a no-op that logs the
/// payload it would have delivered instead of contacting any push provider. This is the pluggable
/// seam — a real FCM/APNs/Web Push sender replaces this registration later without touching the
/// background scheduling logic. Never throws, honouring the interface contract.
/// </summary>
internal sealed class LoggingPushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<LoggingPushNotificationSender> _logger;

    public LoggingPushNotificationSender(ILogger<LoggingPushNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        // A push token is a bearer credential for delivering to a device, so it is never logged in
        // full — only a short prefix + length, enough to correlate without leaking a usable token to
        // log sinks. A real provider sender must observe the same hygiene.
        _logger.LogInformation(
            "Push notification (no-op): Token={TokenPreview} Platform={Platform} Title={Title} Body={Body}",
            DescribeToken(payload.Token),
            payload.Platform,
            payload.Title,
            payload.Body);

        return Task.CompletedTask;
    }

    private static string DescribeToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "<empty>";
        }

        var prefix = token[..Math.Min(8, token.Length)];
        return $"{prefix}…({token.Length} chars)";
    }
}
