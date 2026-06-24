using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Goals;

/// <summary>
/// Integration tests for <c>GET /api/v1/me/goals</c> and <c>PUT /api/v1/me/goals/overrides</c>
/// (issue #17). Reuses the same signed-JWT harness and in-memory SQLite database as
/// <c>ProfileEndpointTests</c>. Each test uses a unique <c>sub</c> claim to avoid cross-test
/// state on the shared database.
/// </summary>
public sealed class GoalsEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string GoalsRoute = "/api/v1/me/goals";
    private const string OverridesRoute = "/api/v1/me/goals/overrides";
    private const string ProfileRoute = "/api/v1/me/profile";

    private readonly HttpClient _client;

    public GoalsEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGoals_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(GoalsRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutOverrides_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, OverridesRoute)
        {
            Content = JsonContent.Create(new { caloriesKcal = 2000 }),
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── No profile => 404 ProblemDetails ─────────────────────────────────────────

    [Fact]
    public async Task GetGoals_NoProfile_Returns404WithProblemDetails()
    {
        var token = TokenFor("goals_no_profile");

        var response = await Get(token, GoalsRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── Profile exists but missing biometrics => 409 ProblemDetails ──────────────

    [Fact]
    public async Task GetGoals_ProfileExistsButMissingBiometrics_Returns409()
    {
        var token = TokenFor("goals_incomplete_profile");

        // Create a profile with only height — no weight, DOB, sex, activity, or goal.
        await Put(token, ProfileRoute, new { heightCm = 175.0 });

        var response = await Get(token, GoalsRoute);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── Complete profile => deterministic computed goals ─────────────────────────
    // Uses a fixed-age profile (DOB chosen so age is stable for the run) and asserts the
    // exact computed values. To keep the asserted age stable regardless of the test-run
    // date we assert structural shape plus positive BMR/TDEE rather than the exact kcal,
    // since age derives from DateTime.UtcNow. Exact math is covered by GoalsCalculatorTests.
    [Fact]
    public async Task GetGoals_CompleteProfile_ReturnsComputedGoals()
    {
        var token = TokenFor("goals_complete_profile");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        var response = await Get(token, GoalsRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        AssertGoalValueShape(body, "calories");
        AssertGoalValueShape(body, "proteinGrams");
        AssertGoalValueShape(body, "carbohydrateGrams");
        AssertGoalValueShape(body, "fatGrams");
        AssertGoalValueShape(body, "waterMl");

        Assert.True(body.GetProperty("bmr").GetInt32() > 0);
        Assert.True(body.GetProperty("tdee").GetInt32() > 0);

        // Water is independent of age, so it is fully deterministic: 80*35 + 350 = 3150.
        Assert.Equal(3150, body.GetProperty("waterMl").GetProperty("value").GetInt32());
        // Protein is independent of age too: round(80*2) = 160.
        Assert.Equal(160, body.GetProperty("proteinGrams").GetProperty("value").GetInt32());

        // No override in effect initially.
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastOverriddenAt").ValueKind);
        Assert.False(body.GetProperty("calories").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("proteinGrams").GetProperty("isOverridden").GetBoolean());
    }

    // ── Targets update when the profile changes (recomputed each request) ─────────
    [Fact]
    public async Task GetGoals_AfterProfileChange_RecomputesTargets()
    {
        var token = TokenFor("goals_recompute_on_change");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "Sedentary",
            primaryGoal = "Maintain",
            weightKg = 80.0,
        });

        var before = await (await Get(token, GoalsRoute)).Content.ReadFromJsonAsync<JsonElement>();
        // Sedentary water bump 0: 80*35 + 0 = 2800.
        Assert.Equal(2800, before.GetProperty("waterMl").GetProperty("value").GetInt32());

        // Change weight and activity — water must recompute.
        await Put(token, ProfileRoute, new { weightKg = 90.0, activityLevel = "VeryActive" });

        var after = await (await Get(token, GoalsRoute)).Content.ReadFromJsonAsync<JsonElement>();
        // VeryActive water bump 500: 90*35 + 500 = 3150 + 500 = 3650.
        Assert.Equal(3650, after.GetProperty("waterMl").GetProperty("value").GetInt32());
        // Protein recomputes too: round(90*2) = 180.
        Assert.Equal(180, after.GetProperty("proteinGrams").GetProperty("value").GetInt32());
    }

    // ── Setting overrides is reflected in a subsequent GET ───────────────────────
    [Fact]
    public async Task PutOverrides_ValidOverrides_ReflectedInGetGoals()
    {
        var token = TokenFor("goals_put_overrides");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        var putResponse = await Put(token, OverridesRoute, new
        {
            caloriesKcal = 2000,
            proteinGrams = 180,
        });
        Assert.True(
            putResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected 200/201 but got {(int)putResponse.StatusCode}.");

        var getResponse = await Get(token, GoalsRoute);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

        var calories = body.GetProperty("calories");
        Assert.Equal(2000, calories.GetProperty("value").GetInt32());
        Assert.True(calories.GetProperty("isOverridden").GetBoolean());
        // The computed value is still reported alongside the override.
        Assert.True(calories.GetProperty("computed").GetInt32() > 0);

        var protein = body.GetProperty("proteinGrams");
        Assert.Equal(180, protein.GetProperty("value").GetInt32());
        Assert.True(protein.GetProperty("isOverridden").GetBoolean());

        // Untouched fields remain computed.
        Assert.False(body.GetProperty("carbohydrateGrams").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("fatGrams").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("waterMl").GetProperty("isOverridden").GetBoolean());

        Assert.NotNull(body.GetProperty("lastOverriddenAt").GetString());
    }

    // ── An empty override body clears all overrides ──────────────────────────────
    [Fact]
    public async Task PutOverrides_EmptyBody_ClearsAllOverrides()
    {
        var token = TokenFor("goals_clear_overrides");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        await Put(token, OverridesRoute, new { caloriesKcal = 1800 });
        await Put(token, OverridesRoute, new { });

        var body = await (await Get(token, GoalsRoute)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("calories").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("proteinGrams").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("carbohydrateGrams").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("fatGrams").GetProperty("isOverridden").GetBoolean());
        Assert.False(body.GetProperty("waterMl").GetProperty("isOverridden").GetBoolean());

        // The clear is itself an override operation, so lastOverriddenAt remains set.
        Assert.NotNull(body.GetProperty("lastOverriddenAt").GetString());
    }

    // ── A calorie override below the minimum is rejected with a field error ──────
    [Fact]
    public async Task PutOverrides_CaloriesBelowMin_Returns400WithFieldError()
    {
        var token = TokenFor("goals_validation_calories");

        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "Sedentary",
            primaryGoal = "Lose",
            weightKg = 80.0,
        });

        var response = await Put(token, OverridesRoute, new { caloriesKcal = 500 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("caloriesKcal", out _));
    }

    // ── PUT overrides with no profile => 404 ─────────────────────────────────────
    [Fact]
    public async Task PutOverrides_NoProfile_Returns404()
    {
        var token = TokenFor("goals_override_no_profile");

        var response = await Put(token, OverridesRoute, new { caloriesKcal = 2000 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

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

    private static void AssertGoalValueShape(JsonElement body, string fieldName)
    {
        Assert.True(body.TryGetProperty(fieldName, out var field),
            $"Response missing field '{fieldName}'.");
        Assert.True(field.TryGetProperty("value", out _),
            $"'{fieldName}' missing 'value' sub-field.");
        Assert.True(field.TryGetProperty("computed", out _),
            $"'{fieldName}' missing 'computed' sub-field.");
        Assert.True(field.TryGetProperty("isOverridden", out _),
            $"'{fieldName}' missing 'isOverridden' sub-field.");
    }
}
