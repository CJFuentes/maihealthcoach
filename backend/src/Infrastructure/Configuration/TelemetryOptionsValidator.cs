using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Validates <see cref="TelemetryOptions"/> on a <em>format-only, when-present</em> basis. All
/// telemetry values ship disabled by default, so this only catches a misconfigured OTLP endpoint —
/// a non-empty value that is not an absolute http(s) URL — early, without forcing any run to supply
/// real telemetry configuration. An empty endpoint is valid and simply leaves OTLP disabled.
/// </summary>
public sealed class TelemetryOptionsValidator : IValidateOptions<TelemetryOptions>
{
    public ValidateOptionsResult Validate(string? name, TelemetryOptions options)
    {
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.Otlp.Endpoint)
            && !IsAbsoluteHttpUrl(options.Otlp.Endpoint))
        {
            failures.Add($"{TelemetryOptions.SectionName}:{nameof(TelemetryOptions.Otlp)}:{nameof(TelemetryOptions.OtlpOptions.Endpoint)} must be an absolute http(s) URL when supplied.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);
}
