using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="AiOptions"/> on a <em>format-only, when-present</em> basis.
/// The <see cref="AiOptions.ApiKey"/> is deliberately <strong>not</strong> required, so the
/// API can build, start, and pass health checks before the Claude integration (issue #36) is
/// wired up. Only values that are actually supplied are checked: this catches a misconfigured
/// base URL or a non-positive timeout early, while still allowing a key-less local/CI run.
/// </summary>
internal sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.BaseUrl)
            && !IsAbsoluteHttpUrl(options.BaseUrl))
        {
            failures.Add($"{AiOptions.SectionName}:{nameof(AiOptions.BaseUrl)} must be an absolute http(s) URL.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            failures.Add($"{AiOptions.SectionName}:{nameof(AiOptions.DefaultModel)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.EscalationModel))
        {
            failures.Add($"{AiOptions.SectionName}:{nameof(AiOptions.EscalationModel)} must not be empty.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add($"{AiOptions.SectionName}:{nameof(AiOptions.TimeoutSeconds)} must be greater than zero.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
