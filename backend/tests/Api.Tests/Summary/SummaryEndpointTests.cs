using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Summary;

/// <summary>
/// Integration tests for the daily nutrition summary endpoint (issue #23):
/// <c>GET /api/v1/me/summary?date=</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Foods are
/// seeded directly into the shared database via a service scope; the profile (so goals compute)
/// and the diary entries are created through the public API exactly as a client would. Each test
/// uses a unique <c>sub</c> claim so provisioned users never collide on the shared database.
/// </remarks>
public sealed class SummaryEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string SummaryRoute = "/api/v1/me/summary";
    private const string DiaryRoute = "/api/v1/me/diary";
    private const string ProfileRoute = "/api/v1/me/profile";

    // A complete profile whose age-independent targets are deterministic:
    //   protein = round(80 * 2) = 160 g ; water = 80*35 + 350 (ModeratelyActive) = 3150 ml.
    private static readonly object CompleteProfile = new
    {
        heightCm = 178.0,
        dateOfBirth = "1990-01-01",
        biologicalSex = "Male",
        activityLevel = "ModeratelyActive",
        primaryGoal = "Lose",
        weightKg = 80.0,
    };

    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SummaryEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{SummaryRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Consumed totals + remaining + percent against goals ──────────────────────

    [Fact]
    public async Task GetSummary_WithEntriesAndGoals_ReturnsConsumedTargetRemainingPercent()
    {
        var token = TokenFor("summary_full");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();

        await Put(token, ProfileRoute, CompleteProfile);

        // Breakfast: 2 cups (245 g each) = 490 g → 59 kcal/100g*490 = 289.1 kcal; 10 g/100g*490 = 49 g protein.
        await Post(token, NewEntry(foodId, cupServingId, 2, "Breakfast", "2026-06-24"));
        // Snack: 0.5 of the 100 g serving = 50 g → 29.5 kcal; 5 g protein.
        await Post(token, NewEntry(foodId, gramServingId, 0.5m, "Snack", "2026-06-24"));

        var response = await Get(token, $"{SummaryRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-24", body.GetProperty("date").GetString());
        Assert.True(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(2, body.GetProperty("entryCount").GetInt32());

        // Consumed calories: 289.1 + 29.5 = 318.6
        var calories = body.GetProperty("calories");
        Assert.Equal(318.6m, calories.GetProperty("consumed").GetDecimal());

        // Consumed protein: 49 + 5 = 54. Target is deterministic at 160 g.
        var protein = body.GetProperty("proteinG");
        Assert.Equal(54.0m, protein.GetProperty("consumed").GetDecimal());
        Assert.Equal(160, protein.GetProperty("target").GetInt32());
        // Remaining = 160 - 54 = 106.
        Assert.Equal(106.0m, protein.GetProperty("remaining").GetDecimal());
        // Percent = 54/160*100 = 33.75 → rounded to one decimal = 33.8 (away-from-zero).
        Assert.Equal(33.8m, protein.GetProperty("percentOfTarget").GetDecimal());

        // Every nutrient line carries a positive integer target when goals are available.
        foreach (var name in new[] { "calories", "proteinG", "carbohydrateG", "fatG" })
        {
            var line = body.GetProperty(name);
            Assert.Equal(JsonValueKind.Number, line.GetProperty("target").ValueKind);
            Assert.True(line.GetProperty("target").GetInt32() > 0);
        }

        // Water target surfaced from goals (informational); no water consumption tracked here.
        Assert.Equal(3150, body.GetProperty("waterTargetMl").GetInt32());
    }

    // ── Per-meal breakdown reconciles with the grand totals ──────────────────────

    [Fact]
    public async Task GetSummary_MealBreakdown_SumsToGrandTotalsInCanonicalOrder()
    {
        var token = TokenFor("summary_meals");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();
        await Put(token, ProfileRoute, CompleteProfile);

        await Post(token, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24")); // 245 g
        await Post(token, NewEntry(foodId, gramServingId, 1, "Dinner", "2026-06-24"));   // 100 g
        // Different day must not leak in.
        await Post(token, NewEntry(foodId, cupServingId, 1, "Lunch", "2026-06-25"));

        var body = await (await Get(token, $"{SummaryRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var meals = body.GetProperty("meals");
        Assert.Equal(2, meals.GetArrayLength()); // Breakfast + Dinner only.
        Assert.Equal("Breakfast", meals[0].GetProperty("mealType").GetString());
        Assert.Equal("Dinner", meals[1].GetProperty("mealType").GetString());

        // Grand total energy equals the sum of the per-meal energies.
        var grand = body.GetProperty("calories").GetProperty("consumed").GetDecimal();
        var mealSum = meals[0].GetProperty("energyKcal").GetDecimal()
                      + meals[1].GetProperty("energyKcal").GetDecimal();
        Assert.Equal(grand, mealSum);

        // 245 g → 59*2.45 = 144.55 kcal ; 100 g → 59 kcal.
        Assert.Equal(144.55m, meals[0].GetProperty("energyKcal").GetDecimal());
        Assert.Equal(59m, meals[1].GetProperty("energyKcal").GetDecimal());
        Assert.Equal(1, meals[0].GetProperty("entryCount").GetInt32());
    }

    // ── Empty day: zeros for consumed, targets still present ─────────────────────

    [Fact]
    public async Task GetSummary_EmptyDayWithGoals_ReturnsZeroConsumedWithTargets()
    {
        var token = TokenFor("summary_empty_day");
        await Put(token, ProfileRoute, CompleteProfile);

        var body = await (await Get(token, $"{SummaryRoute}?date=2026-01-01"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(0, body.GetProperty("entryCount").GetInt32());
        Assert.Equal(0, body.GetProperty("meals").GetArrayLength());

        var protein = body.GetProperty("proteinG");
        Assert.Equal(0m, protein.GetProperty("consumed").GetDecimal());
        Assert.Equal(160, protein.GetProperty("target").GetInt32());
        // Remaining equals the full target; percent is 0.
        Assert.Equal(160m, protein.GetProperty("remaining").GetDecimal());
        Assert.Equal(0m, protein.GetProperty("percentOfTarget").GetDecimal());
    }

    // ── No profile: graceful 200 with consumed totals and null targets ───────────

    [Fact]
    public async Task GetSummary_NoProfile_Returns200WithNullTargets()
    {
        var token = TokenFor("summary_no_profile");
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        // Log food but never create a profile, so goals cannot be computed.
        await Post(token, NewEntry(foodId, cupServingId, 1, "Lunch", "2026-06-24"));

        var response = await Get(token, $"{SummaryRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(1, body.GetProperty("entryCount").GetInt32());

        // Consumed is still reported (245 g → 59*2.45 = 144.55 kcal).
        var calories = body.GetProperty("calories");
        Assert.Equal(144.55m, calories.GetProperty("consumed").GetDecimal());
        // Targets are null without goals.
        Assert.Equal(JsonValueKind.Null, calories.GetProperty("target").ValueKind);
        Assert.Equal(JsonValueKind.Null, calories.GetProperty("remaining").ValueKind);
        Assert.Equal(JsonValueKind.Null, calories.GetProperty("percentOfTarget").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("waterTargetMl").ValueKind);
    }

    // ── Overrides are reflected in the summary's targets ─────────────────────────

    [Fact]
    public async Task GetSummary_WithCalorieOverride_UsesOverriddenTarget()
    {
        var token = TokenFor("summary_override");
        var (foodId, _, cupServingId) = SeedGreekYogurt();
        await Put(token, ProfileRoute, CompleteProfile);
        await Put(token, "/api/v1/me/goals/overrides", new { caloriesKcal = 2000 });

        await Post(token, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24")); // 144.55 kcal

        var body = await (await Get(token, $"{SummaryRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var calories = body.GetProperty("calories");
        Assert.Equal(2000, calories.GetProperty("target").GetInt32());
        // Remaining = 2000 - 144.55 = 1855.45.
        Assert.Equal(1855.45m, calories.GetProperty("remaining").GetDecimal());
    }

    // ── Default date = today when the query param is omitted ─────────────────────

    [Fact]
    public async Task GetSummary_NoDateParam_DefaultsToToday()
    {
        var token = TokenFor("summary_default_date");
        await Put(token, ProfileRoute, CompleteProfile);

        var response = await Get(token, SummaryRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        Assert.Equal(today, body.GetProperty("date").GetString());
    }

    // ── Malformed date => 400 with a field error ─────────────────────────────────

    [Fact]
    public async Task GetSummary_MalformedDate_Returns400WithFieldError()
    {
        var token = TokenFor("summary_bad_date");

        var response = await Get(token, $"{SummaryRoute}?date=24-06-2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    // ── Cross-user isolation: only the caller's entries are summed ───────────────

    [Fact]
    public async Task GetSummary_OnlySumsCurrentUsersEntries()
    {
        var userAToken = TokenFor("summary_xuser_a");
        var userBToken = TokenFor("summary_xuser_b");
        var (foodId, _, cupServingId) = SeedGreekYogurt();
        await Put(userAToken, ProfileRoute, CompleteProfile);

        await Post(userAToken, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-07-01")); // 144.55 kcal
        // User B logs a large amount on the same day; it must NOT affect user A's summary.
        await Post(userBToken, NewEntry(foodId, cupServingId, 10, "Dinner", "2026-07-01"));

        var body = await (await Get(userAToken, $"{SummaryRoute}?date=2026-07-01"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("entryCount").GetInt32());
        Assert.Equal(144.55m, body.GetProperty("calories").GetProperty("consumed").GetDecimal());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private static object NewEntry(Guid foodId, Guid servingId, decimal quantity, string meal, string date) =>
        new
        {
            foodItemId = foodId,
            servingSizeId = servingId,
            quantity,
            mealType = meal,
            date,
        };

    /// <summary>
    /// Seeds a "Greek Yogurt" food (59 kcal / 10 g protein per 100 g) with its canonical 100 g
    /// serving and a "1 cup" = 245 g serving, returning (foodId, gramServingId, cupServingId).
    /// Mirrors the diary endpoint tests' seed so the asserted nutrition math is shared.
    /// </summary>
    private (Guid FoodId, Guid GramServingId, Guid CupServingId) SeedGreekYogurt()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nutrition = NutritionFacts.Create(
            energyKcal: 59m, proteinG: 10m, carbohydrateG: 3.6m, fatG: 0.4m,
            sugarsG: 3.2m, saturatedFatG: 0.1m, sodiumMg: 36m);

        var food = FoodItem.Create("Greek Yogurt", FoodSource.OpenFoodFacts, nutrition, brand: "Fage");
        food.AddServingSize("1 cup", quantity: 1m, unit: "cup", gramsEquivalent: 245m, isDefault: true);

        db.FoodItems.Add(food);
        db.SaveChanges();

        var gramServing = food.ServingSizes.Single(s => s.GramsEquivalent == 100m);
        var cupServing = food.ServingSizes.Single(s => s.Label == "1 cup");
        return (food.Id, gramServing.Id, cupServing.Id);
    }

    private async Task<HttpResponseMessage> Post(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DiaryRoute)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

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
