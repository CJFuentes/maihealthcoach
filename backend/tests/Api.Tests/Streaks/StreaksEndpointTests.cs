using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Streaks;

/// <summary>
/// Integration tests for the streaks endpoint (issue #44): <c>GET /api/v1/me/streaks</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Foods are
/// seeded directly into the shared database via a service scope; the profile, diary entries, and
/// water entries are created through the public API exactly as a client would. Each test uses a
/// unique <c>sub</c> claim so provisioned users never collide on the shared database, and dates are
/// computed from <c>DateOnly.FromDateTime(DateTime.UtcNow)</c> (captured once per test) so the
/// grace/window arithmetic tracks the server's notion of "today".
/// </remarks>
public sealed class StreaksEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string StreaksRoute = "/api/v1/me/streaks";
    private const string DiaryRoute = "/api/v1/me/diary";
    private const string WaterRoute = "/api/v1/me/water";
    private const string ProfileRoute = "/api/v1/me/profile";

    // A complete profile whose targets are deterministic: water target = 80*35 + 350 = 3150 ml.
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

    public StreaksEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStreaks_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(StreaksRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── No logs, no profile: zeros + null adherence ──────────────────────────────

    [Fact]
    public async Task GetStreaks_NoLogsNoProfile_ReturnsZeroStreaksAndNullAdherence()
    {
        var token = TokenFor("streaks_empty");

        var response = await Get(token, StreaksRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(0, body.GetProperty("longestStreak").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("caloriesAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("caloriesAdherence30d").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("waterAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("waterAdherence30d").ValueKind);
    }

    // ── No profile but a water log today: streak counts, adherence still null ─────

    [Fact]
    public async Task GetStreaks_NoProfileButWaterToday_ReturnsStreakOneAndNullAdherence()
    {
        var token = TokenFor("streaks_water_no_profile");
        var today = Today();

        await PostWater(token, 500, today);

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(1, body.GetProperty("longestStreak").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("waterAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("caloriesAdherence7d").ValueKind);
    }

    // ── Water today only: current streak 1 ───────────────────────────────────────

    [Fact]
    public async Task GetStreaks_WaterTodayOnly_ReturnsCurrentStreakOne()
    {
        var token = TokenFor("streaks_water_only");
        await PostWater(token, 250, Today());

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("currentStreak").GetInt32());
    }

    // ── Diary today only: current streak 1 ───────────────────────────────────────

    [Fact]
    public async Task GetStreaks_DiaryTodayOnly_ReturnsCurrentStreakOne()
    {
        var token = TokenFor("streaks_diary_only");
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        await PostDiary(token, NewEntry(foodId, cupServingId, 1, "Breakfast", Today()));

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("currentStreak").GetInt32());
    }

    // ── Diary + water on the same day count once ─────────────────────────────────

    [Fact]
    public async Task GetStreaks_DiaryAndWaterSameDay_CountsOnce()
    {
        var token = TokenFor("streaks_diary_and_water");
        var today = Today();
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        await PostDiary(token, NewEntry(foodId, cupServingId, 1, "Lunch", today));
        await PostWater(token, 500, today);

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        // Two log types, one active day → streak of 1, not 2.
        Assert.Equal(1, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(1, body.GetProperty("longestStreak").GetInt32());
    }

    // ── With profile + water >= target for 7 days: waterAdherence7d == 100 ────────

    [Fact]
    public async Task GetStreaks_WithProfileAndSevenDaysOfWater_ReturnsWaterAdherence100()
    {
        var token = TokenFor("streaks_water_adherence");
        await Put(token, ProfileRoute, CompleteProfile);

        // Log a clearly-large amount (well above any reasonable target) on each of the last 7 days.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 7; i++)
        {
            await PostWater(token, 5000, today.AddDays(-i).ToString("yyyy-MM-dd"));
        }

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Number, body.GetProperty("waterAdherence7d").ValueKind);
        Assert.Equal(100.0m, body.GetProperty("waterAdherence7d").GetDecimal());
    }

    // ── With profile: adherence fields are Number, not null ───────────────────────

    [Fact]
    public async Task GetStreaks_WithProfile_AdherenceFieldsAreNumbers()
    {
        var token = TokenFor("streaks_profile_numbers");
        await Put(token, ProfileRoute, CompleteProfile);

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Number, body.GetProperty("caloriesAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Number, body.GetProperty("caloriesAdherence30d").ValueKind);
        Assert.Equal(JsonValueKind.Number, body.GetProperty("waterAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Number, body.GetProperty("waterAdherence30d").ValueKind);
    }

    // ── Response carries all six fields ──────────────────────────────────────────

    [Fact]
    public async Task GetStreaks_Response_HasAllSixFields()
    {
        var token = TokenFor("streaks_shape");

        var body = await (await Get(token, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        foreach (var name in new[]
                 {
                     "currentStreak", "longestStreak",
                     "caloriesAdherence7d", "caloriesAdherence30d",
                     "waterAdherence7d", "waterAdherence30d",
                 })
        {
            Assert.True(body.TryGetProperty(name, out _), $"Response is missing '{name}'.");
        }
    }

    // ── Cross-user isolation: user B's logs do not affect user A ──────────────────

    [Fact]
    public async Task GetStreaks_OnlyCountsCurrentUsersLogs()
    {
        var userAToken = TokenFor("streaks_xuser_a");
        var userBToken = TokenFor("streaks_xuser_b");
        var today = Today();

        // User B logs water today; user A logs nothing.
        await PostWater(userBToken, 500, today);

        var body = await (await Get(userAToken, StreaksRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("currentStreak").GetInt32());
        Assert.Equal(0, body.GetProperty("longestStreak").GetInt32());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

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
    /// Mirrors the summary/diary endpoint tests' seed.
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

    private async Task<HttpResponseMessage> PostDiary(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DiaryRoute)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostWater(string token, int amountMl, string date)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WaterRoute)
        {
            Content = JsonContent.Create(new { amountMl, date }),
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
