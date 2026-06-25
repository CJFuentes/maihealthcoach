using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Observability;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MAIHealthCoach.Api.Observability;

/// <summary>
/// Wires OpenTelemetry tracing and metrics into the application (issue #47): HTTP server, EF Core,
/// and outbound <c>HttpClient</c> tracing; ASP.NET Core, <c>HttpClient</c>, and .NET runtime metrics
/// plus the application's custom instruments; and configurable OTLP and Prometheus exporters.
/// </summary>
/// <remarks>
/// <para>
/// Every exporter is <strong>off by default</strong>. No OTLP exporter is registered unless a valid
/// absolute http(s) endpoint is configured (via <see cref="TelemetryOptions.OtlpOptions.Endpoint"/>
/// or the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable), and the Prometheus
/// scraping endpoint is mapped only when <see cref="TelemetryOptions.PrometheusOptions.Enabled"/> is
/// set. Because no exporter is registered when unconfigured, the SDK never dials a collector and
/// startup never blocks — the app builds, starts, and passes health checks with no telemetry config.
/// </para>
/// <para>
/// A garbage endpoint string is never handed to the OTLP exporter: the endpoint is validated as an
/// absolute http(s) URL before the exporter is registered, so a misconfigured value is ignored
/// rather than thrown at startup.
/// </para>
/// </remarks>
public static class TelemetryExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics with the configured (optional) exporters. Binds
    /// and validates <see cref="TelemetryOptions"/>, then enables the OTLP exporter only when a valid
    /// absolute http(s) endpoint is present and the Prometheus exporter only when explicitly enabled.
    /// </summary>
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        // Bind + format-validate Telemetry options (lazy, when-present — mirrors the other options).
        builder.Services.AddOptions<TelemetryOptions>()
            .Bind(builder.Configuration.GetSection(TelemetryOptions.SectionName));
        builder.Services.AddSingleton<IValidateOptions<TelemetryOptions>, TelemetryOptionsValidator>();

        // Read the raw config to decide which exporters to register. The exporter decision must be
        // made here (at registration), so it cannot use the IOptions pipeline resolved per request.
        var telemetryOpts = builder.Configuration.GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions();

        var configEndpoint = telemetryOpts.Otlp.Endpoint;
        var envEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        // Only enable OTLP if a VALID absolute http(s) endpoint is supplied (config or env). Never
        // hand a garbage string to the exporter, and never throw at startup over a bad value.
        var otlpEnabled = IsAbsoluteHttpUrl(configEndpoint) || IsAbsoluteHttpUrl(envEndpoint);
        var prometheusEnabled = telemetryOpts.Prometheus.Enabled;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(AppTelemetry.ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddSource(AppTelemetry.ActivitySourceName)
                    // Exclude infra paths (/metrics, /healthz) from self-tracing so probe and scrape
                    // traffic does not generate server spans.
                    .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !IsInfraPath(ctx.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();

                if (otlpEnabled)
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        // Prefer the explicit config endpoint; otherwise the SDK uses the
                        // OTEL_EXPORTER_OTLP_ENDPOINT env var natively.
                        if (IsAbsoluteHttpUrl(configEndpoint))
                        {
                            otlp.Endpoint = new Uri(configEndpoint);
                        }
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(AppTelemetry.MeterName)
                    // Note: the metrics ASP.NET Core instrumentation (MeterProviderBuilder) exposes no
                    // Filter in OpenTelemetry 1.16.0, so infra paths cannot be excluded from HTTP
                    // server metrics here (unlike tracing). Scrape/probe traffic is low-frequency and
                    // harmless in the request-duration histogram.
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (otlpEnabled)
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        if (IsAbsoluteHttpUrl(configEndpoint))
                        {
                            otlp.Endpoint = new Uri(configEndpoint);
                        }
                    });
                }

                if (prometheusEnabled)
                {
                    metrics.AddPrometheusExporter();
                }
            });

        return builder;
    }

    /// <summary>
    /// Maps the Prometheus scraping endpoint (<c>/metrics</c>) when Prometheus is enabled. A no-op
    /// otherwise, so the metrics surface is published only when explicitly opted in.
    /// </summary>
    public static WebApplication MapObservabilityEndpoints(this WebApplication app)
    {
        var telemetryOpts = app.Configuration.GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions();

        if (telemetryOpts.Prometheus.Enabled)
        {
            // Exposes /metrics for Prometheus to scrape.
            app.MapPrometheusScrapingEndpoint();
        }

        return app;
    }

    /// <summary>
    /// True only for a non-empty, absolute http(s) URL. Null/empty/relative/non-http values return
    /// false so a missing or garbage endpoint is treated as "OTLP disabled".
    /// </summary>
    private static bool IsAbsoluteHttpUrl(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrEmpty(uri.Host);

    /// <summary>
    /// True for the infrastructure paths (<c>/metrics</c>, <c>/healthz</c>) that should not generate
    /// server spans — scrape and probe traffic is noise in traces.
    /// </summary>
    private static bool IsInfraPath(PathString path) =>
        path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase);
}
