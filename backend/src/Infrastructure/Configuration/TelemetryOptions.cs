namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for OpenTelemetry observability (issue #47), bound from the
/// <c>Telemetry</c> configuration section. Drives the optional OTLP trace/metric exporter and the
/// optional Prometheus scraping endpoint. All values are non-secret and every exporter ships
/// <strong>disabled</strong> by default, so the app builds, starts, and passes health checks with
/// no telemetry configuration supplied — the SDK never dials a collector when unconfigured.
/// </summary>
/// <remarks>
/// The OTLP exporter is enabled only when an endpoint is supplied as a valid absolute http(s) URL.
/// The endpoint may be set either via <see cref="OtlpOptions.Endpoint"/> (the <c>Telemetry:Otlp:Endpoint</c>
/// configuration key, e.g. the <c>Telemetry__Otlp__Endpoint</c> environment variable) or via the
/// standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable that the OpenTelemetry SDK honors
/// natively. The Prometheus scraping endpoint (<c>/metrics</c>) is exposed only when
/// <see cref="PrometheusOptions.Enabled"/> is <see langword="true"/>.
/// </remarks>
public sealed class TelemetryOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "Telemetry";

    /// <summary>OTLP exporter configuration.</summary>
    public OtlpOptions Otlp { get; set; } = new();

    /// <summary>Prometheus scraping-endpoint configuration.</summary>
    public PrometheusOptions Prometheus { get; set; } = new();

    /// <summary>
    /// Configuration for the OpenTelemetry Protocol (OTLP) trace/metric exporter.
    /// </summary>
    public sealed class OtlpOptions
    {
        /// <summary>
        /// Absolute http(s) URL of the OTLP collector endpoint. Empty by default, which leaves the
        /// OTLP exporter disabled. When non-empty it must be a valid absolute http(s) URL — otherwise
        /// it is ignored (never handed to the exporter) so a misconfigured value can never throw at
        /// startup. The standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable is also honored.
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for the Prometheus metrics scraping endpoint (<c>/metrics</c>).
    /// </summary>
    public sealed class PrometheusOptions
    {
        /// <summary>
        /// When <see langword="true"/>, exposes the Prometheus scraping endpoint at <c>/metrics</c>.
        /// Disabled by default so no metrics surface is published unless explicitly opted in.
        /// </summary>
        public bool Enabled { get; set; }
    }
}
