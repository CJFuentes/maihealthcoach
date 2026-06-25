using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MAIHealthCoach.Infrastructure.Observability;

/// <summary>
/// Process-lifetime telemetry primitives for the MAI Health Coach application (issue #47): the
/// shared <see cref="ActivitySource"/> for custom tracing spans, the <see cref="Meter"/> backing
/// custom metrics, and the application's custom instruments. These names are the contract the
/// OpenTelemetry SDK subscribes to (<c>AddSource</c> / <c>AddMeter</c>).
/// </summary>
/// <remarks>
/// The <see cref="Source"/>, the backing <see cref="Meter"/>, and every instrument exposed here are
/// static singletons with the lifetime of the process. They are intentionally <strong>never
/// disposed</strong> — disposing them would silently stop telemetry collection for the rest of the
/// process. This matches the OpenTelemetry guidance for application-owned instrumentation.
/// </remarks>
public static class AppTelemetry
{
    /// <summary>Logical service name reported on every span/metric resource.</summary>
    public const string ServiceName = "MAIHealthCoach.Api";

    /// <summary>Name of the application's <see cref="ActivitySource"/> (subscribed via <c>AddSource</c>).</summary>
    public const string ActivitySourceName = "MAIHealthCoach.Infrastructure";

    /// <summary>Name of the application's <see cref="Meter"/> (subscribed via <c>AddMeter</c>).</summary>
    public const string MeterName = "MAIHealthCoach.Infrastructure";

    private static readonly Meter _meter = new(MeterName, "1.0");

    /// <summary>
    /// Counter incremented once for every coach request received by the coaching service, regardless
    /// of outcome (issue #47's custom metric).
    /// </summary>
    public static readonly Counter<long> CoachRequests = _meter.CreateCounter<long>(
        "coach.requests",
        unit: "{request}",
        description: "Total number of coach requests received by CoachService.");

    /// <summary>
    /// Shared <see cref="ActivitySource"/> for application-owned custom spans. Subscribed by the
    /// tracing pipeline via <see cref="ActivitySourceName"/>.
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName);
}
