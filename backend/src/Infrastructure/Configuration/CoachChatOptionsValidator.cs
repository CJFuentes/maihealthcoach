using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="CoachChatOptions"/> on a range-only basis. The values all ship with safe
/// defaults, so this only guards against a misconfigured override (e.g. a non-positive permit limit
/// or window) being supplied. Validation is lazy — resolved on first use, consistent with the other
/// options validators — so the app still builds and starts with no <c>CoachChat</c> section present.
/// </summary>
internal sealed class CoachChatOptionsValidator : IValidateOptions<CoachChatOptions>
{
    public ValidateOptionsResult Validate(string? name, CoachChatOptions options)
    {
        var failures = new List<string>();

        if (options.PermitLimit < 1)
        {
            failures.Add($"{CoachChatOptions.SectionName}:{nameof(CoachChatOptions.PermitLimit)} must be at least 1.");
        }

        if (options.WindowSeconds < 1)
        {
            failures.Add($"{CoachChatOptions.SectionName}:{nameof(CoachChatOptions.WindowSeconds)} must be at least 1.");
        }

        if (options.MaxMessageLength < 1)
        {
            failures.Add($"{CoachChatOptions.SectionName}:{nameof(CoachChatOptions.MaxMessageLength)} must be at least 1.");
        }

        if (options.HistoryTurnLimit < 0)
        {
            failures.Add($"{CoachChatOptions.SectionName}:{nameof(CoachChatOptions.HistoryTurnLimit)} must not be negative.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
