namespace MAIHealthCoach.Application.Notifications;

/// <summary>
/// Abstraction over the push-notification delivery transport (issue #48). The reminder background
/// service depends on this seam so the actual FCM/APNs/Web Push integration can be plugged in later
/// without touching the scheduling logic; the shipped default is a no-op logging sender.
/// </summary>
/// <remarks>
/// Implementations <strong>MUST NOT throw</strong>. The background service sends to many devices per
/// tick and treats delivery as best-effort fire-and-forget; an implementation that throws would abort
/// the remaining sends for that user. Swallow and log transport errors internally instead.
/// </remarks>
public interface IPushNotificationSender
{
    /// <summary>
    /// Delivers a single push payload. Never throws — transport failures are handled internally.
    /// </summary>
    /// <param name="payload">The notification to deliver.</param>
    /// <param name="cancellationToken">Token to cancel the in-flight delivery.</param>
    Task SendAsync(PushNotificationPayload payload, CancellationToken cancellationToken = default);
}
