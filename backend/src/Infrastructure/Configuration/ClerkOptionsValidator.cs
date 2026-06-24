using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="ClerkOptions"/> on a <em>format-only, when-present</em> basis.
/// Empty values are intentionally allowed so the API can build, start, and pass health
/// checks before Clerk authentication is wired up (issue #12). Only values that are actually
/// supplied are checked, so a malformed URL is caught early without forcing every developer
/// to configure Clerk just to run the app.
/// </summary>
internal sealed class ClerkOptionsValidator : IValidateOptions<ClerkOptions>
{
    public ValidateOptionsResult Validate(string? name, ClerkOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.Authority)
            && !IsAbsoluteHttpUrl(options.Authority))
        {
            failures.Add($"{ClerkOptions.SectionName}:{nameof(ClerkOptions.Authority)} must be an absolute http(s) URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.JwksUrl)
            && !IsAbsoluteHttpUrl(options.JwksUrl))
        {
            failures.Add($"{ClerkOptions.SectionName}:{nameof(ClerkOptions.JwksUrl)} must be an absolute http(s) URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.Issuer)
            && !IsAbsoluteHttpUrl(options.Issuer))
        {
            failures.Add($"{ClerkOptions.SectionName}:{nameof(ClerkOptions.Issuer)} must be an absolute http(s) URL.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
