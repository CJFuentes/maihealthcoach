using System.Diagnostics.Metrics;
using MAIHealthCoach.Infrastructure.Observability;

namespace MAIHealthCoach.Infrastructure.Tests.Observability;

/// <summary>
/// Verifies the application's custom telemetry primitives (issue #47): the shared instrument names
/// the OpenTelemetry SDK subscribes to (<c>AddMeter</c> / <c>AddSource</c>) and that the
/// <see cref="AppTelemetry.CoachRequests"/> counter actually emits measurements observable by a
/// listener — without standing up a real exporter.
/// </summary>
/// <remarks>
/// The <see cref="AppTelemetry.CoachRequests"/> counter is a process-global static shared with the
/// rest of the test suite, so these tests must never assert on an absolute meter total. The counter
/// measurement test wires a <see cref="MeterListener"/>, starts it, then performs its own Add calls
/// and asserts only on the DELTA its own callback captured — values recorded before the listener
/// starts (or by other tests) are not observed by this listener and cannot perturb the assertion.
/// </remarks>
public sealed class AppTelemetryTests
{
    [Fact]
    public void CoachRequests_Add_EmitsMeasurementsObservableByListener()
    {
        long observed = 0;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == AppTelemetry.MeterName
                    && instrument.Name == "coach.requests")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>(
            (_, value, _, _) => Interlocked.Add(ref observed, value));

        // Only Add calls made after Start() are observed by this listener — so the captured delta is
        // exactly what we record below, independent of any other test's writes to the global counter.
        listener.Start();

        AppTelemetry.CoachRequests.Add(1);
        AppTelemetry.CoachRequests.Add(2);

        Assert.Equal(3, Interlocked.Read(ref observed));
    }

    [Fact]
    public void CoachRequests_HasExpectedInstrumentName()
    {
        Assert.Equal("coach.requests", AppTelemetry.CoachRequests.Name);
    }

    [Fact]
    public void MeterName_HasExpectedValue()
    {
        Assert.Equal("MAIHealthCoach.Infrastructure", AppTelemetry.MeterName);
    }

    [Fact]
    public void ActivitySourceName_HasExpectedValue()
    {
        Assert.Equal("MAIHealthCoach.Infrastructure", AppTelemetry.ActivitySourceName);
    }

    [Fact]
    public void ServiceName_HasExpectedValue()
    {
        Assert.Equal("MAIHealthCoach.Api", AppTelemetry.ServiceName);
    }
}
