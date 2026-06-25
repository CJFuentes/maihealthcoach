namespace MAIHealthCoach.Domain.Notifications;

/// <summary>
/// The client platform a <see cref="DeviceRegistration"/> token belongs to (issue #48). Determines
/// how a push payload is ultimately delivered (FCM for Android, APNs for iOS, Web Push for browsers).
/// Persisted as its string name, not its ordinal, so column values stay stable if members are
/// reordered.
/// </summary>
public enum DevicePlatform
{
    /// <summary>Apple iOS / iPadOS device (delivered via APNs).</summary>
    iOS = 0,

    /// <summary>Android device (delivered via Firebase Cloud Messaging).</summary>
    Android = 1,

    /// <summary>Web browser (delivered via the Web Push protocol).</summary>
    Web = 2,
}
