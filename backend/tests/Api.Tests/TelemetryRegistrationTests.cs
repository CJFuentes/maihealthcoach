using System.Net;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Integration tests for the OpenTelemetry wiring (issue #47): that <c>AddObservability()</c>
/// registers the tracer/meter providers in DI, that telemetry registration does not break host
/// startup or the health surface, that <see cref="TelemetryOptions"/> default to disabled, and that
/// the Prometheus <c>/metrics</c> scraping endpoint is mapped only when explicitly enabled.
/// </summary>
public sealed class TelemetryRegistrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TelemetryRegistrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public void TracerProvider_IsRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var tracerProvider = scope.ServiceProvider.GetService<TracerProvider>();

        Assert.NotNull(tracerProvider);
    }

    [Fact]
    public void MeterProvider_IsRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var meterProvider = scope.ServiceProvider.GetService<MeterProvider>();

        Assert.NotNull(meterProvider);
    }

    [Fact]
    public async Task LivenessLive_StillReturnsOk_AfterTelemetryRegistration()
    {
        // Guards that adding the OpenTelemetry pipeline did not break host startup / the health surface.
        var response = await _client.GetAsync("/healthz/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void TelemetryOptions_Defaults_AreDisabled()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<TelemetryOptions>>()
            .Value;

        Assert.Equal(string.Empty, options.Otlp.Endpoint);
        Assert.False(options.Prometheus.Enabled);
    }

    [Fact]
    public async Task Metrics_Returns404_WhenPrometheusDisabled()
    {
        // Default factory leaves Prometheus disabled, so /metrics is never mapped.
        var response = await _client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Metrics_Returns200TextPlain_WhenPrometheusEnabled()
    {
        await using var factory = new PrometheusEnabledFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.StartsWith(
            "text/plain",
            response.Content.Headers.ContentType!.MediaType,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Boots the app exactly like <see cref="TestWebApplicationFactory"/> (Development environment,
    /// <c>Database:AutoMigrate=false</c> so no Postgres connection is attempted) but with the
    /// Prometheus scraping endpoint enabled, so the one test above can assert <c>/metrics</c> is mapped.
    /// </summary>
    private sealed class PrometheusEnabledFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // UseEnvironment must precede base.ConfigureWebHost (mirrors TestWebApplicationFactory).
            builder.UseEnvironment("Development");

            base.ConfigureWebHost(builder);

            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("Telemetry:Prometheus:Enabled", "true");
        }
    }
}
