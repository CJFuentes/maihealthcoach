using System.Net;
using System.Text.Json;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Verifies the per-version OpenAPI document endpoint introduced for issue #8.
/// </summary>
public sealed class OpenApiEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OpenApiEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApiV1Document_ReturnsOk()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiV1Document_ContainsLiteralPingPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("/api/v1/ping", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiV1Document_IsValidOpenApiDocumentListingPingPath()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        // Must carry an OpenAPI version field — proves it is a real spec a client
        // codegen tool can consume, not merely a 200 with arbitrary JSON.
        Assert.True(
            root.TryGetProperty("openapi", out var openApiVersion),
            "OpenAPI document is missing the 'openapi' version field.");
        Assert.False(string.IsNullOrWhiteSpace(openApiVersion.GetString()));

        // The versioned ping endpoint must be listed as a structured path entry,
        // not just present somewhere in the raw text.
        Assert.True(
            root.TryGetProperty("paths", out var paths),
            "OpenAPI document is missing the 'paths' object.");
        Assert.Equal(JsonValueKind.Object, paths.ValueKind);
        Assert.True(
            paths.TryGetProperty("/api/v1/ping", out _),
            "OpenAPI 'paths' object does not list /api/v1/ping.");
    }
}
