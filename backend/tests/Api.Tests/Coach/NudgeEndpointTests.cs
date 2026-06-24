using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Api.Tests.Coach;

/// <summary>
/// Integration tests for <c>GET /api/v1/me/coach/nudge</c> (issue #38). Reuses the signed-JWT harness
/// and in-memory SQLite database, swapping the coach service for a stub so the tests are deterministic
/// and never call Anthropic. Each test uses a unique <c>sub</c> claim to avoid cross-test state, and
/// sets <c>StubService.Handler</c> explicitly so it never relies on state left over from a prior test
/// (tests in a class run sequentially).
/// </summary>
/// <remarks>
/// Unlike meal-suggestions, a missing or incomplete profile is the documented friendlier behaviour
/// here: the endpoint returns 200 with a generic encouraging nudge rather than 404/409.
/// </remarks>
public sealed class NudgeEndpointTests : IClassFixture<CoachTestWebApplicationFactory>
{
    private const string NudgeRoute = "/api/v1/me/coach/nudge";
    private const string ProfileRoute = "/api/v1/me/profile";

    // A valid nudge JSON object whose message carries no digits, so tests can assert that no streak
    // or adherence number was fabricated by the endpoint when those signals are absent.
    private const string NudgeJson =
        """{"message": "Every healthy choice counts — keep showing up for yourself.", "tone": "encouraging"}""";

    private readonly CoachTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NudgeEndpointTests(CoachTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetNudge_NoToken_Returns401()
    {
        var response = await _client.GetAsync(NudgeRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── No profile => 200 generic nudge (friendlier behaviour, not 404) ──────────

    [Fact]
    public async Task GetNudge_NoProfile_Returns200WithMessageAndDisclaimer()
    {
        ResetStubToNudge();
        var token = TokenFor("nudge_no_profile");

        var response = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.NotNull(body.GetProperty("disclaimer").GetString());
    }

    // ── Incomplete profile => 200 generic nudge (not 409) ────────────────────────

    [Fact]
    public async Task GetNudge_IncompleteProfile_Returns200WithMessage()
    {
        ResetStubToNudge();
        var token = TokenFor("nudge_incomplete_profile");

        await Put(token, ProfileRoute, new { heightCm = 175.0 });

        var response = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    // ── Complete profile => 200 with profile/goal context in the request ─────────

    [Fact]
    public async Task GetNudge_CompleteProfile_FlowsProfileContextAndReturnsDisclaimer()
    {
        CoachRequest? captured = null;
        _factory.StubService.Handler = req =>
        {
            captured = req;
            return CoachResult.Success(NudgeJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);
        };

        var token = TokenFor("nudge_complete_profile");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        var response = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.NotNull(body.GetProperty("disclaimer").GetString());

        Assert.NotNull(captured);
        Assert.NotNull(captured!.Context);
        Assert.False(string.IsNullOrWhiteSpace(captured.Context!.PrimaryGoal));
        Assert.True(captured.Context.DailyCalorieTarget.HasValue);

        ResetStubToNudge();
    }

    // ── Streak & adherence query params flow into the user message ───────────────

    [Fact]
    public async Task GetNudge_WithStreakAndAdherence_FlowsIntoUserMessage()
    {
        CoachRequest? captured = null;
        _factory.StubService.Handler = req =>
        {
            captured = req;
            return CoachResult.Success(NudgeJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);
        };

        var token = TokenFor("nudge_streak_adherence");

        var response = await Get(token, $"{NudgeRoute}?streakDays=5&adherencePercent=80");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(captured);
        Assert.Contains("5", captured!.UserMessage);
        Assert.Contains("80", captured.UserMessage);

        ResetStubToNudge();
    }

    // ── Streak/adherence absent => neutral path, no fabricated numbers ───────────

    [Fact]
    public async Task GetNudge_WithoutStreakOrAdherence_DoesNotFabricateNumbers()
    {
        CoachRequest? captured = null;
        _factory.StubService.Handler = req =>
        {
            captured = req;
            return CoachResult.Success(NudgeJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);
        };

        var token = TokenFor("nudge_no_signals");

        var response = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));

        Assert.NotNull(captured);
        Assert.DoesNotContain("Current streak:", captured!.UserMessage);
        Assert.DoesNotContain("Today's adherence:", captured.UserMessage);

        ResetStubToNudge();
    }

    // ── Validation: invalid adherence/streak => 400 ──────────────────────────────

    [Fact]
    public async Task GetNudge_InvalidAdherencePercent_Returns400()
    {
        ResetStubToNudge();
        var token = TokenFor("nudge_invalid_adherence");

        var response = await Get(token, $"{NudgeRoute}?adherencePercent=150");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetNudge_InvalidStreakDays_Returns400()
    {
        ResetStubToNudge();
        var token = TokenFor("nudge_invalid_streak");

        var response = await Get(token, $"{NudgeRoute}?streakDays=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Configuration failure => 503 ─────────────────────────────────────────────

    [Fact]
    public async Task GetNudge_CoachConfigError_Returns503()
    {
        _factory.StubService.Handler =
            _ => CoachResult.Failure(CoachErrorCategory.ConfigurationError, "not configured");

        var token = TokenFor("nudge_config_error");

        var response = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        ResetStubToNudge();
    }

    // ── Guardrail/disclaimer applies on success ──────────────────────────────────

    [Fact]
    public async Task GetNudge_OnSuccess_ResponseDisclaimerMatchesSafetyDisclaimer()
    {
        ResetStubToNudge();
        var token = TokenFor("nudge_disclaimer");

        var response = await Get(token, NudgeRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var disclaimer = body.GetProperty("disclaimer").GetString();
        Assert.Equal(CoachPromptBuilder.SafetyDisclaimer, disclaimer);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private void ResetStubToNudge() =>
        _factory.StubService.Handler =
            _ => CoachResult.Success(NudgeJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Put(string token, string route, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
