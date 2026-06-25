using MAIHealthCoach.Domain.Notifications;

namespace MAIHealthCoach.Api.Features.Notifications;

/// <summary>
/// Pure-static validator for device-registration requests (issue #48). Returns a dictionary of field
/// errors keyed by camelCase field name, compatible with <c>Results.ValidationProblem(errors)</c>.
/// Never throws; an unknown platform string produces a field error rather than an exception.
/// </summary>
internal static class DeviceValidator
{
    private const int MaxTokenLength = 1024;
    private const int MaxNameLength = 128;

    /// <summary>The accepted platform names, matched case-insensitively.</summary>
    private static readonly HashSet<string> ValidPlatforms =
        new(StringComparer.OrdinalIgnoreCase) { "iOS", "Android", "Web" };

    /// <summary>Validates a <see cref="RegisterDeviceRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateRegister(RegisterDeviceRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            errors["token"] = ["Token is required."];
        }
        else if (request.Token.Length > MaxTokenLength)
        {
            errors["token"] = [$"Token must not exceed {MaxTokenLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            errors["platform"] = ["Platform is required."];
        }
        else if (!ValidPlatforms.Contains(request.Platform))
        {
            errors["platform"] = ["Platform must be one of: iOS, Android, Web."];
        }

        if (request.Name is { Length: > MaxNameLength })
        {
            errors["name"] = [$"Name must not exceed {MaxNameLength} characters."];
        }

        return errors;
    }

    /// <summary>
    /// Parses a platform name to its <see cref="DevicePlatform"/> value. Returns false for null,
    /// blank, or any value that is not a defined enum member.
    /// </summary>
    internal static bool TryParsePlatform(string? platform, out DevicePlatform parsed)
    {
        if (Enum.TryParse(platform, ignoreCase: true, out parsed)
            && Enum.IsDefined(parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
