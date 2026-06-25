using System.Net;
using System.Net.Http.Headers;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Api.Tests.Coach;
using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Api.Tests.RateLimiting;

/// <summary>
/// Integration tests for the consolidated coach LLM rate-limiting policy (issue #45). The policy is
/// applied to all three coach LLM endpoints (chat / meal-suggestions / nudge) and partitions a
/// per-user fixed window by the authenticated <c>sub</c>, driven by <c>CoachChat:PermitLimit</c> so
/// it stays a single knob consolidated with issue #39's chat limiter. Bound to
/// <see cref="CoachPolicyRateLimitTestWebApplicationFactory"/> (own host/partition store,
/// <c>PermitLimit = 3</c>); each test uses a unique <c>sub</c> so its bucket starts empty.
/// </summary>
public sealed class CoachPolicyRateLimitTests : IClassFixture<CoachPolicyRateLimitTestWebApplicationFactory>
{
    private const string NudgeRoute = "/api/v1/me/coach/nudge";
    private const string MealSuggestionsRoute = "/api/v1/me/coach/meal-suggestions";

    private const string NudgeJson =
        """{"message": "Every healthy choice counts — keep showing up for yourself.", "tone": "encouraging"}""";

    private readonly CoachPolicyRateLimitTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CoachPolicyRateLimitTests(CoachPolicyRateLimitTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task NudgeEndpoint_RateLimitExceeded_Returns429WithRetryAfter()
    {
        _factory.StubService.Handler =
            _ => CoachResult.Success(NudgeJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);

        var token = JwtTestHelper.CreateToken("coach_nudge_rl", "coach_nudge_rl@test.local");

        // PermitLimit is 3: the first three nudges succeed, the fourth is rejected.
        var r1 = await Get(token, NudgeRoute);
        var r2 = await Get(token, NudgeRoute);
        var r3 = await Get(token, NudgeRoute);
        var r4 = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, r4.StatusCode);

        Assert.Contains(
            "application/problem+json",
            r4.Content.Headers.ContentType?.MediaType ?? string.Empty);
        Assert.True(r4.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task MealSuggestionsEndpoint_RateLimitExceeded_Returns429()
    {
        _factory.StubService.Handler =
            _ => CoachResult.Success(
                StubCoachService.DefaultSuggestionJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);

        var token = JwtTestHelper.CreateToken("coach_meal_rl", "coach_meal_rl@test.local");

        // The limiter runs before the handler, so the policy is exercised regardless of whether the
        // user has a profile: the fourth request to a fresh per-user bucket is rejected with 429.
        HttpResponseMessage? last = null;
        for (var i = 0; i < CoachPolicyRateLimitTestWebApplicationFactory.CoachPermitLimit + 1; i++)
        {
            last = await Get(token, MealSuggestionsRoute);
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task CoachPolicy_DifferentUsers_HaveIndependentBuckets()
    {
        _factory.StubService.Handler =
            _ => CoachResult.Success(NudgeJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);

        // One user exhausts their coach budget...
        var heavyToken = JwtTestHelper.CreateToken("coach_heavy", "coach_heavy@test.local");
        for (var i = 0; i < CoachPolicyRateLimitTestWebApplicationFactory.CoachPermitLimit + 1; i++)
        {
            await Get(heavyToken, NudgeRoute);
        }

        // ...a different user is unaffected (the policy partitions per 'sub').
        var lightToken = JwtTestHelper.CreateToken("coach_light", "coach_light@test.local");
        var response = await Get(lightToken, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
