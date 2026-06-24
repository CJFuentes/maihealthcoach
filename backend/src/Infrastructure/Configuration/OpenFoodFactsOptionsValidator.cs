using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="OpenFoodFactsOptions"/> on a <em>format-only, when-present</em> basis.
/// All OFF values ship with safe defaults, so this only catches a misconfigured base URL, an
/// empty user-agent (Open Food Facts requires one), or a non-sensible numeric setting early,
/// without forcing any run to supply real configuration.
/// </summary>
internal sealed class OpenFoodFactsOptionsValidator : IValidateOptions<OpenFoodFactsOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenFoodFactsOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.BaseUrl)
            && !IsAbsoluteHttpUrl(options.BaseUrl))
        {
            failures.Add($"{OpenFoodFactsOptions.SectionName}:{nameof(OpenFoodFactsOptions.BaseUrl)} must be an absolute http(s) URL.");
        }

        if (string.IsNullOrWhiteSpace(options.UserAgent))
        {
            failures.Add($"{OpenFoodFactsOptions.SectionName}:{nameof(OpenFoodFactsOptions.UserAgent)} must not be empty (Open Food Facts requires a User-Agent).");
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add($"{OpenFoodFactsOptions.SectionName}:{nameof(OpenFoodFactsOptions.TimeoutSeconds)} must be greater than zero.");
        }

        if (options.CacheTtlDays < 0)
        {
            failures.Add($"{OpenFoodFactsOptions.SectionName}:{nameof(OpenFoodFactsOptions.CacheTtlDays)} must not be negative.");
        }

        if (options.SearchPageSize <= 0)
        {
            failures.Add($"{OpenFoodFactsOptions.SectionName}:{nameof(OpenFoodFactsOptions.SearchPageSize)} must be greater than zero.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
