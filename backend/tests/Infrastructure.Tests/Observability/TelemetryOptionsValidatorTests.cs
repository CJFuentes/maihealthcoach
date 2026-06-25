using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Tests.Observability;

/// <summary>
/// Validation tests for <see cref="TelemetryOptions"/> bound through the real options pipeline
/// (issue #47). All telemetry ships disabled by default, so the validator's only job is to reject a
/// non-empty OTLP endpoint that is not an absolute http(s) URL with a host — while leaving the app
/// startable with no telemetry config at all. The options are resolved exactly as the app resolves
/// them (binding + the lazily-applied <see cref="TelemetryOptionsValidator"/>), so a bad value
/// surfaces as an <see cref="OptionsValidationException"/> on first <c>.Value</c> access.
/// </summary>
public sealed class TelemetryOptionsValidatorTests
{
    private static IServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<TelemetryOptions>()
            .Bind(configuration.GetSection(TelemetryOptions.SectionName));
        services.AddSingleton<IValidateOptions<TelemetryOptions>, TelemetryOptionsValidator>();

        return services.BuildServiceProvider();
    }

    private static TelemetryOptions Resolve(Dictionary<string, string?> settings) =>
        BuildProvider(settings).GetRequiredService<IOptions<TelemetryOptions>>().Value;

    [Fact]
    public void Defaults_AreValidAndDisabled()
    {
        // The core guarantee: with NO telemetry config, resolving the options (which triggers the
        // lazy validator) must succeed and leave every exporter disabled so the app starts green.
        var options = Resolve([]);

        Assert.Equal(string.Empty, options.Otlp.Endpoint);
        Assert.False(options.Prometheus.Enabled);
    }

    [Fact]
    public void EmptyEndpoint_IsValid()
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            ["Telemetry:Otlp:Endpoint"] = "",
        });

        Assert.Equal(string.Empty, options.Otlp.Endpoint);
    }

    [Theory]
    [InlineData("https://collector.example.com:4317")]
    [InlineData("http://localhost:4317")]
    public void ValidHttpEndpoint_IsAccepted(string endpoint)
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            ["Telemetry:Otlp:Endpoint"] = endpoint,
        });

        Assert.Equal(endpoint, options.Otlp.Endpoint);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("http://")]
    [InlineData("ftp://collector.example.com")]
    public void MalformedOrHostlessEndpoint_FailsValidation(string endpoint)
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Telemetry:Otlp:Endpoint"] = endpoint,
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<TelemetryOptions>>().Value);
    }

    [Fact]
    public void PrometheusEnabled_BindsAndIsValid()
    {
        var options = Resolve(new Dictionary<string, string?>
        {
            ["Telemetry:Prometheus:Enabled"] = "true",
        });

        Assert.True(options.Prometheus.Enabled);
    }
}
