using System.Collections.Concurrent;
using MAIHealthCoach.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Verifies AC3's structured-logging requirement that the per-request correlation ID is
/// not merely echoed in the response header but is actually attached to the log scope, so
/// every log event emitted during the request carries it as a structured property.
///
/// The response-header echo and the <c>ILogger.BeginScope</c> enrichment are independent
/// code paths in <see cref="CorrelationIdMiddleware"/>. The existing header tests would all
/// stay green if the BeginScope block were removed, silently breaking the requirement that
/// logs are correlatable per request. This test closes that gap by capturing emitted Serilog
/// events through an in-memory sink (registered in DI and picked up via ReadFrom.Services)
/// and asserting the supplied correlation ID surfaces as the <c>CorrelationId</c> property.
/// </summary>
public sealed class CorrelationIdLogScopeTests
{
    [Fact]
    public async Task Request_WithCorrelationIdHeader_EmitsLogEventCarryingCorrelationIdProperty()
    {
        const string correlationId = "log-scope-correlation-7f3a";
        var sink = new CapturingSink();

        await using var factory = new SinkCapturingFactory(sink);
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // Serilog's request-logging middleware emits a completion event per request. With the
        // correlation scope active and FromLogContext enrichment enabled, that event must carry
        // the correlation ID as a structured property under the middleware's scope key.
        var correlated = sink.Events.Any(e =>
            e.Properties.TryGetValue(CorrelationIdMiddleware.HttpContextItemKey, out var value)
            && value is ScalarValue scalar
            && correlationId.Equals(scalar.Value?.ToString(), StringComparison.Ordinal));

        Assert.True(
            correlated,
            $"Expected at least one emitted log event to carry " +
            $"{CorrelationIdMiddleware.HttpContextItemKey}='{correlationId}' as a structured property. " +
            $"Captured {sink.Events.Count} event(s).");
    }

    /// <summary>
    /// In-memory Serilog sink that retains every emitted <see cref="LogEvent"/> for assertion.
    /// Registered in DI as an <see cref="ILogEventSink"/> so Program.cs's
    /// <c>ReadFrom.Services</c> wires it into the active logging pipeline alongside the console sink.
    /// </summary>
    private sealed class CapturingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> _events = new();

        public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

        public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
    }

    private sealed class SinkCapturingFactory : WebApplicationFactory<Program>
    {
        private readonly ILogEventSink _sink;

        public SinkCapturingFactory(ILogEventSink sink) => _sink = sink;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseEnvironment("Development");
            builder.UseSetting("Database:AutoMigrate", "false");

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_sink);
            });
        }
    }
}
