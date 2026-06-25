using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="RateLimitingOptions"/> on a range-only basis. The values all ship with safe
/// defaults, so this only guards against a misconfigured override (e.g. a non-positive permit limit
/// or window) being supplied. Validation is lazy — resolved on first use, consistent with the other
/// options validators — so the app still builds and starts with no <c>RateLimiting</c> section present.
/// </summary>
internal sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        var failures = new List<string>();

        if (options.GlobalPermitLimit < 1)
        {
            failures.Add($"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.GlobalPermitLimit)} must be at least 1.");
        }

        if (options.GlobalWindowSeconds < 1)
        {
            failures.Add($"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.GlobalWindowSeconds)} must be at least 1.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
