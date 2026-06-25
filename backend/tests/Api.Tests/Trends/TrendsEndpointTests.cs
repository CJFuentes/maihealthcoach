using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Trends;

/// <summary>
/// Integration tests for the trends time-series endpoint (issue #43):
/// <c>GET /api/v1/me/trends?from=&amp;to=&amp;range=</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>), whose
/// <c>EnsureCreated</c> applies the catalog <c>HasData</c> seed so the seeded exercise activities are
/// present. Foods are seeded directly into the shared database via a service scope; the profile,
/// diary entries, and water entries are created through the public API exactly as a client would.
/// Each test uses a unique <c>sub</c> claim so provisioned users never collide on the shared
/// database, and today-relative cases compute dates from <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>
/// (captured once per test) so window arithmetic tracks the server's notion of "today" — mirroring
/// <c>DashboardEndpointTests</c>.
/// </remarks>
public sealed class TrendsEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string TrendsRoute = "/api/v1/me/trends";
    private const string WaterRoute = "/api/v1/me/water";
    private const string DiaryRoute = "/api/v1/me/diary";
    private const string ProfileRoute = "/api/v1/me/profile";

    // A complete profile whose age-independent targets are deterministic:
    //   protein = round(80 * 2) = 160 g ; water = 80*35 + 350 (ModeratelyActive) = 3150 ml.
    // Carries a known body weight (80 kg) which also drives the exercise calories-burned snapshot.
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

    public TrendsEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -- Auth guard --------------------------------------------------------------

    [Fact]
    public async Task GetTrends_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{TrendsRoute}?range=7");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Default window: last 30 days --------------------------------------------

    [Fact]
    public async Task GetTrends_NoParams_DefaultsToLast30Days()
    {
        var token = TokenFor("trends_default30");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var body = await (await Get(token, TrendsRoute)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(today.AddDays(-29).ToString("yyyy-MM-dd"), body.GetProperty("from").GetString());
        Assert.Equal(today.ToString("yyyy-MM-dd"), body.GetProperty("to").GetString());

        foreach (var name in new[] { "caloriesConsumed", "caloriesBurned", "netCalories", "waterMl" })
        {
            Assert.Equal(30, body.GetProperty(name).GetArrayLength());
        }
    }

    // -- range=7/30/90 trailing windows ------------------------------------------

    [Fact]
    public async Task GetTrends_WithRange7_Returns7Days()
    {
        var token = TokenFor("trends_range7");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var body = await (await Get(token, $"{TrendsRoute}?range=7")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(today.AddDays(-6).ToString("yyyy-MM-dd"), body.GetProperty("from").GetString());
        Assert.Equal(today.ToString("yyyy-MM-dd"), body.GetProperty("to").GetString());

        foreach (var name in new[] { "caloriesConsumed", "caloriesBurned", "netCalories", "waterMl" })
        {
            Assert.Equal(7, body.GetProperty(name).GetArrayLength());
        }
    }

    [Fact]
    public async Task GetTrends_WithRange30_Returns30Days()
    {
        var token = TokenFor("trends_range30");

        var body = await (await Get(token, $"{TrendsRoute}?range=30")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(30, body.GetProperty("caloriesConsumed").GetArrayLength());
    }

    [Fact]
    public async Task GetTrends_WithRange90_Returns90Days()
    {
        var token = TokenFor("trends_range90");

        var body = await (await Get(token, $"{TrendsRoute}?range=90")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(90, body.GetProperty("caloriesConsumed").GetArrayLength());
    }

    // -- from/to wins over range --------------------------------------------------

    [Fact]
    public async Task GetTrends_FromToWinsOverRange()
    {
        var token = TokenFor("trends_fromto_wins");

        var body = await (await Get(token, $"{TrendsRoute}?range=7&from=2026-06-01&to=2026-06-07"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("2026-06-01", body.GetProperty("from").GetString());
        Assert.Equal("2026-06-07", body.GetProperty("to").GetString());
        Assert.Equal(7, body.GetProperty("caloriesConsumed").GetArrayLength());
    }

    // -- Validation: bad range ----------------------------------------------------

    [Fact]
    public async Task GetTrends_InvalidRange_Returns400WithRangeError()
    {
        var token = TokenFor("trends_bad_range");

        var response = await Get(token, $"{TrendsRoute}?range=14");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("range", out _));
    }

    // -- Validation: from after to ------------------------------------------------

    [Fact]
    public async Task GetTrends_FromAfterTo_Returns400WithFromError()
    {
        var token = TokenFor("trends_from_after_to");

        var response = await Get(token, $"{TrendsRoute}?from=2026-06-07&to=2026-06-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("from", out _));
    }

    // -- Window cap: 366 days OK, 367 rejected -----------------------------------

    [Fact]
    public async Task GetTrends_Span366_Returns200()
    {
        var token = TokenFor("trends_span366");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await Get(
            token,
            $"{TrendsRoute}?from={today.AddDays(-365):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(366, body.GetProperty("caloriesConsumed").GetArrayLength());
    }

    [Fact]
    public async Task GetTrends_Span367_Returns400()
    {
        var token = TokenFor("trends_span367");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var response = await Get(
            token,
            $"{TrendsRoute}?from={today.AddDays(-366):yyyy-MM-dd}&to={today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Only from supplied: to defaults to today --------------------------------

    [Fact]
    public async Task GetTrends_OnlyFrom_DefaultsToToToday()
    {
        var token = TokenFor("trends_only_from");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var body = await (await Get(token, $"{TrendsRoute}?from={today.AddDays(-3):yyyy-MM-dd}"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(today.ToString("yyyy-MM-dd"), body.GetProperty("to").GetString());
        Assert.Equal(today.AddDays(-3).ToString("yyyy-MM-dd"), body.GetProperty("from").GetString());
    }

    // -- Diary entries surface in caloriesConsumed -------------------------------

    [Fact]
    public async Task GetTrends_WithDiaryEntries_CaloriesConsumedNonZero()
    {
        var token = TokenFor("trends_diary");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        // 1 cup (245 g) -> 59 * 2.45 = 144.55 kcal on today.
        await PostDiary(token, NewEntry(foodId, cupServingId, 1, "Breakfast", today.ToString("yyyy-MM-dd")));

        var body = await (await Get(token, $"{TrendsRoute}?range=7")).Content.ReadFromJsonAsync<JsonElement>();

        var consumed = body.GetProperty("caloriesConsumed");
        var todayStr = today.ToString("yyyy-MM-dd");

        // The last dense point is today and carries the consumed calories (> 0).
        var lastPoint = consumed[consumed.GetArrayLength() - 1];
        Assert.Equal(todayStr, lastPoint.GetProperty("date").GetString());
        Assert.True(lastPoint.GetProperty("value").GetDecimal() > 0m);

        // An earlier day with no data is 0-filled (first point of the 7-day window).
        var firstPoint = consumed[0];
        Assert.Equal(today.AddDays(-6).ToString("yyyy-MM-dd"), firstPoint.GetProperty("date").GetString());
        Assert.Equal(0m, firstPoint.GetProperty("value").GetDecimal());
    }

    // -- Water entries surface in waterMl ----------------------------------------

    [Fact]
    public async Task GetTrends_WithWaterEntries_WaterMlNonZero()
    {
        var token = TokenFor("trends_water");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PostWater(token, 500, today.ToString("yyyy-MM-dd"));

        var body = await (await Get(token, $"{TrendsRoute}?range=7")).Content.ReadFromJsonAsync<JsonElement>();

        var water = body.GetProperty("waterMl");
        var todayStr = today.ToString("yyyy-MM-dd");

        var todayPoint = FindByDate(water, todayStr);
        Assert.Equal(500m, todayPoint.GetProperty("value").GetDecimal());
    }

    // -- Two weight measurements same UTC day: latest instant wins ---------------

    [Fact]
    public async Task GetTrends_TwoWeightMeasurementsSameUtcDay_LatestInstantWins()
    {
        var token = TokenFor("trends_weight_latest");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // First PUT records 80.0 kg; second PUT (same run/day) records 82.0 kg. The 0.001 kg dedup
        // guard does not suppress 82 vs 80, so two rows exist on the same UTC day -> latest (82) wins.
        await Put(token, ProfileRoute, CompleteProfile);
        await Put(token, ProfileRoute, new
        {
            heightCm = 178.0,
            dateOfBirth = "1990-01-01",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            weightKg = 82.0,
        });

        var body = await (await Get(token, $"{TrendsRoute}?range=7")).Content.ReadFromJsonAsync<JsonElement>();

        var weight = body.GetProperty("weight");
        var todayPoint = FindByDate(weight, today.ToString("yyyy-MM-dd"));
        Assert.Equal(82.0, todayPoint.GetProperty("weightKg").GetDouble());
    }

    // -- Cross-user isolation -----------------------------------------------------

    [Fact]
    public async Task GetTrends_CrossUserIsolation()
    {
        var tokenA = TokenFor("trends_isolation_a");
        var tokenB = TokenFor("trends_isolation_b");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PostWater(tokenA, 500, today.ToString("yyyy-MM-dd"));

        var body = await (await Get(tokenB, $"{TrendsRoute}?range=7")).Content.ReadFromJsonAsync<JsonElement>();

        var water = body.GetProperty("waterMl");
        foreach (var point in water.EnumerateArray())
        {
            Assert.Equal(0m, point.GetProperty("value").GetDecimal());
        }
    }

    // -- helpers -----------------------------------------------------------------

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

    private static JsonElement FindByDate(JsonElement array, string date)
    {
        foreach (var point in array.EnumerateArray())
        {
            if (point.GetProperty("date").GetString() == date)
            {
                return point;
            }
        }

        throw new Xunit.Sdk.XunitException($"No point with date '{date}' found in series.");
    }

    /// <summary>
    /// Seeds a "Greek Yogurt" food (59 kcal / 10 g protein per 100 g) with its canonical 100 g
    /// serving and a "1 cup" = 245 g serving, returning (foodId, gramServingId, cupServingId).
    /// Mirrors the summary/streaks/diary endpoint tests' seed so the asserted nutrition math is shared.
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

    private async Task<HttpResponseMessage> PostDiary(string token, object body) =>
        await PostTo(token, DiaryRoute, body);

    private async Task<HttpResponseMessage> PostWater(string token, int amountMl, string date) =>
        await PostTo(token, WaterRoute, new { amountMl, date });

    private async Task<HttpResponseMessage> PostTo(string token, string route, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
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
