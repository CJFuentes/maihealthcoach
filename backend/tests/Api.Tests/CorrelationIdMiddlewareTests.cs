using System.Net;
using MAIHealthCoach.Api.Middleware;

namespace MAIHealthCoach.Api.Tests;

public sealed class CorrelationIdMiddlewareTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdMiddlewareTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_WithCorrelationIdHeader_EchoesItInResponse()
    {
        const string correlationId = "test-correlation-abc123";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values),
            $"Response must contain {CorrelationIdMiddleware.HeaderName} header.");
        Assert.Equal(correlationId, values!.Single());
    }

    [Fact]
    public async Task Request_WithoutCorrelationIdHeader_GeneratesOneInResponse()
    {
        var response = await _client.GetAsync("/api/v1/ping");

        Assert.True(
            response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values),
            $"Response must contain {CorrelationIdMiddleware.HeaderName} header even when not supplied.");

        var generated = values!.Single();
        Assert.False(string.IsNullOrWhiteSpace(generated));
        Assert.True(Guid.TryParse(generated, out _),
            $"Auto-generated correlation ID must be a valid GUID but was: '{generated}'.");
    }

    [Fact]
    public async Task Request_OnUnhandledException_CorrelationIdHeaderSurvivesIn500Response()
    {
        const string correlationId = "error-path-correlation-xyz";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/throw-test");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values),
            $"Correlation ID header must be present on 500 responses.");
        Assert.Equal(correlationId, values!.Single());
    }

    [Fact]
    public async Task Request_WithEmptyCorrelationIdHeader_GeneratesNewId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ping");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, string.Empty);

        var response = await _client.SendAsync(request);

        Assert.True(
            response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }
}
