using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MAIHealthCoach.Api.Tests.Auth;

/// <summary>
/// Integration tests for the protected <c>GET /api/v1/me</c> endpoint and Clerk JWT
/// validation (issue #12). Tokens are signed locally; the database is SQLite in-memory —
/// no Clerk servers and no Postgres are involved.
/// </summary>
public sealed class MeEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MeEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMe_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithExpiredToken_Returns401()
    {
        var token = JwtTestHelper.CreateToken("user_expired", "expired@test.local", expired: true);

        var response = await SendWithToken(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithGarbageToken_Returns401()
    {
        var response = await SendWithToken("not-a-real-jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200AndProvisionsUser()
    {
        const string clerkUserId = "user_provision_001";
        const string email = "alice@test.local";
        var token = JwtTestHelper.CreateToken(clerkUserId, email);

        var response = await SendWithToken(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(clerkUserId, body.GetProperty("clerkUserId").GetString());
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("id").GetString()));
        Assert.True(body.TryGetProperty("createdAt", out _));
    }

    [Fact]
    public async Task GetMe_WithValidToken_SecondCall_ReusesSameUser()
    {
        const string clerkUserId = "user_provision_002";
        const string email = "bob@test.local";
        var token = JwtTestHelper.CreateToken(clerkUserId, email);

        var first = await ReadMe(token);
        var second = await ReadMe(token);

        // Same local id on both calls -> provisioned once, reused thereafter.
        Assert.Equal(
            first.GetProperty("id").GetString(),
            second.GetProperty("id").GetString());
    }

    private async Task<HttpResponseMessage> SendWithToken(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> ReadMe(string token)
    {
        var response = await SendWithToken(token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
