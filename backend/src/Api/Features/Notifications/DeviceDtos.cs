namespace MAIHealthCoach.Api.Features.Notifications;

/// <summary>
/// Request body for registering (or refreshing) a push device (issue #48). <see cref="Platform"/> is
/// a case-insensitive platform name (<c>iOS</c>/<c>Android</c>/<c>Web</c>), validated and parsed in
/// the handler rather than by the model binder so an unknown value yields a 400 field error.
/// </summary>
/// <param name="Token">The platform push token.</param>
/// <param name="Platform">The client platform name.</param>
/// <param name="Name">Optional device label.</param>
public record RegisterDeviceRequest(string Token, string Platform, string? Name);

/// <summary>
/// API representation of a registered push device (issue #48).
/// </summary>
/// <param name="Id">The registration's stable identifier (used by the unregister endpoint).</param>
/// <param name="Token">The platform push token.</param>
/// <param name="Platform">The client platform name.</param>
/// <param name="Name">Optional device label.</param>
/// <param name="LastSeenAt">UTC instant the device last registered or refreshed.</param>
/// <param name="CreatedAt">UTC instant the registration was created.</param>
/// <param name="UpdatedAt">UTC instant the registration was last updated.</param>
public record DeviceResponse(
    Guid Id,
    string Token,
    string Platform,
    string? Name,
    DateTimeOffset LastSeenAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
