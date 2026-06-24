using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Api.Tests.Coach;

/// <summary>
/// Integration tests for <c>GET /api/v1/me/coach/meal-suggestions</c> (issue #37). Reuses the
/// signed-JWT harness and in-memory SQLite database, swapping the coach service for a stub so the
/// tests are deterministic and never call Anthropic. Each test uses a unique <c>sub</c> claim to
/// avoid cross-test state, and sets <c>StubService.Handler</c> explicitly so it never relies on
/// state left over from a prior test (tests in a class run sequentially).
/// </summary>
public sealed class MealSuggestionsEndpointTests : IClassFixture<CoachTestWebApplicationFactory>
{
    private const string MealSuggestionsRoute = "/api/v1/me/coach/meal-suggestions";
    private const string ProfileRoute = "/api/v1/me/profile";

    private readonly CoachTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MealSuggestionsEndpointTests(CoachTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMealSuggestions_NoToken_Returns401()
    {
        var response = await _client.GetAsync(MealSuggestionsRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── No profile => 404 ProblemDetails ─────────────────────────────────────────

    [Fact]
    public async Task GetMealSuggestions_NoProfile_Returns404()
    {
        ResetStubToDefault();
        var token = TokenFor("coach_no_profile");

        var response = await Get(token, MealSuggestionsRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── Profile exists but missing biometrics => 409 ProblemDetails ──────────────

    [Fact]
    public async Task GetMealSuggestions_IncompleteProfile_Returns409()
    {
        ResetStubToDefault();
        var token = TokenFor("coach_incomplete_profile");

        await Put(token, ProfileRoute, new { heightCm = 175.0 });

        var response = await Get(token, MealSuggestionsRoute);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── Complete profile => 200 with parsed options ──────────────────────────────

    [Fact]
    public async Task GetMealSuggestions_CompleteProfile_Returns200WithParsedOptions()
    {
        ResetStubToDefault();
        var token = TokenFor("coach_complete_profile");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        var response = await Get(token, MealSuggestionsRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var options = body.GetProperty("options");
        Assert.Equal(JsonValueKind.Array, options.ValueKind);
        Assert.True(options.GetArrayLength() >= 1);

        foreach (var option in options.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(option.GetProperty("name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(option.GetProperty("rationale").GetString()));
        }

        Assert.NotNull(body.GetProperty("disclaimer").GetString());
        Assert.True(body.GetProperty("remainingCalories").GetInt32() > 0);
    }

    // ── Dietary constraints and meal type flow into the prompt and context ───────

    [Fact]
    public async Task GetMealSuggestions_HonorsDietaryConstraintsAndMealType_InPrompt()
    {
        CoachRequest? captured = null;
        _factory.StubService.Handler = req =>
        {
            captured = req;
            return CoachResult.Success(
                StubCoachService.DefaultSuggestionJson,
                "claude-sonnet-4-6",
                CoachPromptBuilder.SafetyDisclaimer);
        };

        var token = TokenFor("coach_dietary_constraints");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
            dietType = "Vegan",
            allergies = "peanuts",
        });

        var response = await Get(token, $"{MealSuggestionsRoute}?mealType=Dinner");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(captured);
        Assert.Contains("Dinner", captured!.UserMessage);
        Assert.Contains("ABSOLUTE constraints", captured.UserMessage);

        Assert.NotNull(captured.Context);
        Assert.Contains("Vegan", captured.Context!.DietaryPreferences ?? string.Empty);
        Assert.Contains("peanuts", captured.Context.DietaryPreferences ?? string.Empty);
        Assert.True(captured.Context.DailyCalorieTarget.HasValue);

        ResetStubToDefault();
    }

    // ── Configuration failure => 503 ─────────────────────────────────────────────

    [Fact]
    public async Task GetMealSuggestions_CoachConfigError_Returns503()
    {
        _factory.StubService.Handler =
            _ => CoachResult.Failure(CoachErrorCategory.ConfigurationError, "not configured");

        var token = TokenFor("coach_config_error");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        var response = await Get(token, MealSuggestionsRoute);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        ResetStubToDefault();
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private void ResetStubToDefault() =>
        _factory.StubService.Handler =
            _ => CoachResult.Success(
                StubCoachService.DefaultSuggestionJson,
                "claude-sonnet-4-6",
                CoachPromptBuilder.SafetyDisclaimer);

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
