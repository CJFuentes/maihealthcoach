using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Notifications;

/// <summary>
/// A push-notification device registration: binds a platform-specific push <see cref="Token"/>
/// (FCM/APNs/Web Push) to a <see cref="UserId">user</see> so reminder payloads (issue #48) can be
/// targeted at that device. A single physical device produces one row; re-registering the same
/// token simply <see cref="Update"/>s the existing row's metadata and <see cref="LastSeenAt"/>.
/// </summary>
/// <remarks>
/// The <see cref="Token"/> is globally unique (enforced by a unique index in the EF configuration),
/// since a token identifies exactly one device install. When a token already owned by another user
/// is registered — e.g. the same physical device is handed to a new account — it is handed off via
/// <see cref="ReassignTo"/> rather than rejected, mirroring how the API resolves the unique-index
/// race. Actual delivery is out of scope here: the server-side infra ships with a no-op sender and a
/// pluggable <c>IPushNotificationSender</c> seam.
/// </remarks>
public sealed class DeviceRegistration : EntityBase
{
    /// <summary>Foreign key referencing the owning user's <c>Users.Id</c>.</summary>
    public Guid UserId { get; private set; }

    /// <summary>The platform-specific push token. Globally unique; never null or blank.</summary>
    public string Token { get; private set; } = "";

    /// <summary>The client platform this token targets (drives the delivery transport).</summary>
    public DevicePlatform Platform { get; private set; }

    /// <summary>Optional human-friendly device label (e.g. "Carlos's iPhone"). May be null.</summary>
    public string? Name { get; private set; }

    /// <summary>UTC instant the device last registered or refreshed its token.</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private DeviceRegistration() { }

    /// <summary>
    /// Creates a new <see cref="DeviceRegistration"/> for the given user and token. Audit timestamps
    /// and <see cref="LastSeenAt"/> are stamped here so the entity is fully initialized before it is
    /// added to the change tracker.
    /// </summary>
    /// <param name="userId">The owning user's internal <c>Users.Id</c>.</param>
    /// <param name="token">The platform push token. Must not be null or whitespace.</param>
    /// <param name="platform">The client platform the token targets.</param>
    /// <param name="name">Optional device label.</param>
    /// <exception cref="ArgumentException"><paramref name="token"/> is null, empty, or whitespace.</exception>
    public static DeviceRegistration Create(Guid userId, string token, DevicePlatform platform, string? name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var now = DateTimeOffset.UtcNow;
        return new DeviceRegistration
        {
            UserId = userId,
            Token = token,
            Platform = platform,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now,
            LastSeenAt = now,
        };
    }

    /// <summary>
    /// Refreshes the mutable metadata of an existing registration (the token itself is immutable —
    /// a new token is a new row). Bumps <see cref="LastSeenAt"/> and <see cref="EntityBase.UpdatedAt"/>.
    /// </summary>
    /// <param name="platform">Replacement client platform.</param>
    /// <param name="name">Replacement device label (may be null).</param>
    public void Update(DevicePlatform platform, string? name)
    {
        Platform = platform;
        Name = name;

        var now = DateTimeOffset.UtcNow;
        LastSeenAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Hands this device registration off to a different user. Used by the register-conflict path
    /// when a token already owned by another account is registered from the same physical device.
    /// Bumps <see cref="LastSeenAt"/> and <see cref="EntityBase.UpdatedAt"/>.
    /// </summary>
    /// <param name="userId">The new owning user's internal <c>Users.Id</c>.</param>
    public void ReassignTo(Guid userId)
    {
        UserId = userId;

        var now = DateTimeOffset.UtcNow;
        LastSeenAt = now;
        UpdatedAt = now;
    }
}
