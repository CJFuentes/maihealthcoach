using System.Net;
using System.Net.Http.Json;
using MAIHealthCoach.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MAIHealthCoach.Api.Tests;

public sealed class HealthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Liveness_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LivenessLive_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_ReturnsOkOrServiceUnavailable_WithinTimeout()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await _client.GetAsync("/healthz/ready", cts.Token);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503 but got {(int)response.StatusCode}.");
    }

    [Fact]
    public void HealthChecks_PostgresRegistration_IsNamedAndTaggedReady()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;

        var registration = options.Registrations
            .SingleOrDefault(r => r.Name == "postgres");

        Assert.NotNull(registration);
        Assert.Contains(DependencyInjection.ReadyTag, registration!.Tags);
    }

    [Fact]
    public void HealthChecks_PostgresRegistration_HasThreeSecondTimeout()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;

        var registration = options.Registrations
            .SingleOrDefault(r => r.Name == "postgres");

        Assert.NotNull(registration);
        Assert.Equal(TimeSpan.FromSeconds(3), registration!.Timeout);
    }

    [Fact]
    public async Task Ping_ReturnsVersionPayload()
    {
        var response = await _client.GetAsync("/api/v1/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PingResponse>();
        Assert.NotNull(payload);
        Assert.Equal("MAIHealthCoach.Api", payload!.Service);
        Assert.False(string.IsNullOrWhiteSpace(payload.Version));
        Assert.True(payload.Timestamp <= DateTime.UtcNow);
    }

    private sealed record PingResponse(string Service, string Version, DateTime Timestamp);
}
