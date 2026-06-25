using System.Net;
using System.Net.Http.Headers;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.RateLimiting;

/// <summary>
/// Integration tests for the global API rate limiter (issue #45). Bound to
/// <see cref="GlobalRateLimitTestWebApplicationFactory"/>, which lowers
/// <c>RateLimiting:GlobalPermitLimit</c> to 3 over a long window and owns its own host (and therefore
/// its own rate-limiter partition store), so limiter state never leaks into other tests. Each test
/// uses a <c>sub</c> unique to the case so its fixed-window bucket starts empty.
/// </summary>
public sealed class GlobalRateLimitTests : IClassFixture<GlobalRateLimitTestWebApplicationFactory>
{
    private const string MeRoute = "/api/v1/me";

    private readonly HttpClient _client;

    public GlobalRateLimitTests(GlobalRateLimitTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GlobalLimiter_BurstOverLimit_Returns429WithRetryAfter()
    {
        var token = JwtTestHelper.CreateToken("global_burst", "global_burst@test.local");

        // GlobalPermitLimit is 3 for this host: the first three requests succeed, the fourth is
        // rejected with a 429.
        var r1 = await Get(token, MeRoute);
        var r2 = await Get(token, MeRoute);
        var r3 = await Get(token, MeRoute);
        var r4 = await Get(token, MeRoute);

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, r4.StatusCode);

        // The 429 carries problem+json and a Retry-After header.
        Assert.Contains(
            "application/problem+json",
            r4.Content.Headers.ContentType?.MediaType ?? string.Empty);
        Assert.True(r4.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task GlobalLimiter_NormalTraffic_PassesThrough()
    {
        var token = JwtTestHelper.CreateToken("global_normal", "global_normal@test.local");

        // Staying at or under the budget is never throttled.
        var r1 = await Get(token, MeRoute);
        var r2 = await Get(token, MeRoute);

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Fact]
    public async Task GlobalLimiter_DifferentUsers_HaveIndependentBuckets()
    {
        // One user exhausting their budget must not throttle a different user — the global limiter
        // partitions by the authenticated 'sub'.
        var heavyToken = JwtTestHelper.CreateToken("global_heavy", "global_heavy@test.local");
        for (var i = 0; i < GlobalRateLimitTestWebApplicationFactory.GlobalPermitLimit + 1; i++)
        {
            await Get(heavyToken, MeRoute);
        }

        var lightToken = JwtTestHelper.CreateToken("global_light", "global_light@test.local");
        var response = await Get(lightToken, MeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoints_AreExemptFromRateLimiting()
    {
        // Burst well past the global budget against the health endpoints; they must never be
        // throttled (a 429 on a probe would take the service out of rotation).
        for (var i = 0; i < GlobalRateLimitTestWebApplicationFactory.GlobalPermitLimit + 5; i++)
        {
            var live = await _client.GetAsync("/healthz/live");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, live.StatusCode);
        }

        var final = await _client.GetAsync("/healthz/live");
        Assert.Equal(HttpStatusCode.OK, final.StatusCode);
    }

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
