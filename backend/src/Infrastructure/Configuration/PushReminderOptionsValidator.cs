using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="PushReminderOptions"/> on a range-only basis. The values all ship with safe
/// defaults, so this only guards against a misconfigured override (e.g. a sub-minute tick interval or
/// a non-positive per-tick cap). Validation is lazy — resolved on first use, consistent with the
/// other options validators — so the app still builds and starts with no <c>PushReminder</c> section.
/// </summary>
internal sealed class PushReminderOptionsValidator : IValidateOptions<PushReminderOptions>
{
    public ValidateOptionsResult Validate(string? name, PushReminderOptions options)
    {
        var failures = new List<string>();

        if (options.CheckIntervalSeconds < 60)
        {
            failures.Add($"{PushReminderOptions.SectionName}:{nameof(PushReminderOptions.CheckIntervalSeconds)} must be at least 60.");
        }

        if (options.MaxUsersPerTick < 1)
        {
            failures.Add($"{PushReminderOptions.SectionName}:{nameof(PushReminderOptions.MaxUsersPerTick)} must be greater than 0.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
