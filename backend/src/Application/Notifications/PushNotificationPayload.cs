using MAIHealthCoach.Domain.Notifications;

namespace MAIHealthCoach.Application.Notifications;

/// <summary>
/// A transport-agnostic push-notification message bound for a single device token (issue #48). The
/// concrete <see cref="IPushNotificationSender"/> implementation maps this onto the platform's wire
/// format (FCM/APNs/Web Push).
/// </summary>
/// <param name="Token">The destination device's push token.</param>
/// <param name="Platform">The destination device's platform.</param>
/// <param name="Title">The notification title shown to the user.</param>
/// <param name="Body">The notification body text.</param>
/// <param name="Category">
/// Optional category/topic key (e.g. <c>"meal_reminder"</c>) used by the client to group or route
/// the notification.
/// </param>
public sealed record PushNotificationPayload(
    string Token,
    DevicePlatform Platform,
    string Title,
    string Body,
    string? Category = null);
