using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MAIHealthCoach.Api.Tests;

public sealed class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
