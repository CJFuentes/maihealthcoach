using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Dashboard;

/// <summary>
/// Integration tests for the daily dashboard aggregate endpoint (issue #42):
/// <c>GET /api/v1/me/dashboard?date=</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>), whose
/// <c>EnsureCreated</c> applies the catalog <c>HasData</c> seed so the seeded exercise activities
/// (and their MET values) are present. Foods are seeded directly into the shared database via a
/// service scope; the profile, diary entries, water entries, and exercise entries are created
/// through the public API exactly as a client would. Each test uses a unique <c>sub</c> claim so
/// provisioned users never collide on the shared database, and today-relative cases compute dates
/// from <c>DateOnly.FromDateTime(DateTime.UtcNow)</c> (captured once per test) so streak/adherence
/// arithmetic tracks the server's notion of "today" — mirroring <c>StreaksEndpointTests</c>.
/// </remarks>
public sealed class DashboardEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string DashboardRoute = "/api/v1/me/dashboard";
    private const string SummaryRoute = "/api/v1/me/summary";
    private const string WaterRoute = "/api/v1/me/water";
    private const string ExerciseRoute = "/api/v1/me/exercise";
    private const string DiaryRoute = "/api/v1/me/diary";
    private const string ProfileRoute = "/api/v1/me/profile";

    // A seeded shared activity with a known MET (Running, 6 mph), per the catalog seed. Reused from
    // ExerciseLogEndpointTests so the exercise burn is computed from the live seed, not a magic value.
    private static readonly Guid RunningActivityId =
        new("01975a00-0001-7000-8000-000000000002");

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

    private const int ExpectedWaterGoalMl = 3150;

    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DashboardEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -- Auth guard --------------------------------------------------------------

    [Fact]
    public async Task GetDashboard_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{DashboardRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -- Malformed date => 400 ValidationProblem with a field error --------------

    [Fact]
    public async Task GetDashboard_WithMalformedDate_Returns400()
    {
        var token = TokenFor("dash_bad_date");

        var response = await Get(token, $"{DashboardRoute}?date=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    // -- No profile, no data: 200 with zeros + nulls everywhere a target would be -

    [Fact]
    public async Task GetDashboard_NoProfileNoData_ReturnsZerosAndNullsButOk()
    {
        var token = TokenFor("dash_empty");

        var response = await Get(token, $"{DashboardRoute}?date=2026-01-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-01-01", body.GetProperty("date").GetString());
        Assert.False(body.GetProperty("goalsAvailable").GetBoolean());

        // Calories block: consumed zero, every macro target absent, no entries.
        var calories = body.GetProperty("calories");
        Assert.Equal(0, calories.GetProperty("entryCount").GetInt32());
        Assert.Equal(0m, calories.GetProperty("calories").GetProperty("consumed").GetDecimal());
        foreach (var name in new[] { "calories", "proteinG", "carbohydrateG", "fatG" })
        {
            var line = calories.GetProperty(name);
            Assert.Equal(JsonValueKind.Null, line.GetProperty("target").ValueKind);
            Assert.Equal(JsonValueKind.Null, line.GetProperty("remaining").ValueKind);
            Assert.Equal(JsonValueKind.Null, line.GetProperty("percentOfTarget").ValueKind);
        }

        // Water block: consumed zero, no goal.
        var water = body.GetProperty("water");
        Assert.False(water.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(0, water.GetProperty("consumedMl").GetInt32());
        Assert.Equal(JsonValueKind.Null, water.GetProperty("goalMl").ValueKind);
        Assert.Equal(JsonValueKind.Null, water.GetProperty("remainingMl").ValueKind);

        // Exercise block: nothing burned, no entries.
        var exercise = body.GetProperty("exercise");
        Assert.Equal(0m, exercise.GetProperty("totalCaloriesBurned").GetDecimal());
        Assert.Equal(0, exercise.GetProperty("entryCount").GetInt32());

        // Net calories meaningless with no diary AND no exercise -> null.
        Assert.Equal(JsonValueKind.Null, body.GetProperty("netCalories").ValueKind);

        // Streak block: no active days, adherence null without goals.
        var streak = body.GetProperty("streak");
        Assert.Equal(0, streak.GetProperty("currentStreak").GetInt32());
        Assert.Equal(0, streak.GetProperty("longestStreak").GetInt32());
        Assert.Equal(JsonValueKind.Null, streak.GetProperty("caloriesAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Null, streak.GetProperty("waterAdherence7d").ValueKind);
    }

    // -- Diary + profile: consumed/targets reconcile with GET /me/summary --------

    [Fact]
    public async Task GetDashboard_WithProfileAndDiaryEntries_ReturnsConsumedTargetsAndReconcilesWithSummary()
    {
        var token = TokenFor("dash_diary_reconcile");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();
        await Put(token, ProfileRoute, CompleteProfile);

        // Breakfast: 2 cups (245 g each) = 490 g -> 59*4.9 = 289.1 kcal ; 10*4.9 = 49 g protein.
        await PostDiary(token, NewEntry(foodId, cupServingId, 2, "Breakfast", "2026-06-24"));
        // Snack: 0.5 of the 100 g serving = 50 g -> 29.5 kcal ; 5 g protein.
        await PostDiary(token, NewEntry(foodId, gramServingId, 0.5m, "Snack", "2026-06-24"));

        var dashboard = await (await Get(token, $"{DashboardRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var summary = await (await Get(token, $"{SummaryRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("2026-06-24", dashboard.GetProperty("date").GetString());
        Assert.True(dashboard.GetProperty("goalsAvailable").GetBoolean());

        var dashCalories = dashboard.GetProperty("calories");
        Assert.Equal(2, dashCalories.GetProperty("entryCount").GetInt32());

        // Deterministic seeded-food math: consumed calories 289.1 + 29.5 = 318.6, protein 49 + 5 = 54.
        var calLine = dashCalories.GetProperty("calories");
        var proteinLine = dashCalories.GetProperty("proteinG");
        Assert.Equal(318.6m, calLine.GetProperty("consumed").GetDecimal());
        Assert.Equal(54.0m, proteinLine.GetProperty("consumed").GetDecimal());

        // Reconcile calorie + protein consumed/target/remaining with /me/summary for the same date.
        var sumCalories = summary.GetProperty("calories");
        var sumProtein = summary.GetProperty("proteinG");

        Assert.Equal(
            sumCalories.GetProperty("consumed").GetDecimal(),
            calLine.GetProperty("consumed").GetDecimal());
        Assert.Equal(
            sumCalories.GetProperty("target").GetInt32(),
            calLine.GetProperty("target").GetInt32());
        Assert.Equal(
            sumCalories.GetProperty("remaining").GetDecimal(),
            calLine.GetProperty("remaining").GetDecimal());

        Assert.Equal(
            sumProtein.GetProperty("consumed").GetDecimal(),
            proteinLine.GetProperty("consumed").GetDecimal());
        Assert.Equal(
            sumProtein.GetProperty("target").GetInt32(),
            proteinLine.GetProperty("target").GetInt32());
        Assert.Equal(
            sumProtein.GetProperty("remaining").GetDecimal(),
            proteinLine.GetProperty("remaining").GetDecimal());

        // Entry counts agree between the two endpoints.
        Assert.Equal(
            summary.GetProperty("entryCount").GetInt32(),
            dashCalories.GetProperty("entryCount").GetInt32());
    }

    // -- Water entries: dashboard water reconciles with GET /me/water ------------

    [Fact]
    public async Task GetDashboard_WithWaterEntries_ReturnsWaterConsumedVsGoal()
    {
        var token = TokenFor("dash_water");
        await Put(token, ProfileRoute, CompleteProfile);

        await PostWater(token, 500, "2026-06-24");
        await PostWater(token, 250, "2026-06-24");

        var dashboard = await (await Get(token, $"{DashboardRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var waterDay = await (await Get(token, $"{WaterRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var water = dashboard.GetProperty("water");
        Assert.True(water.GetProperty("goalsAvailable").GetBoolean());

        // Sums correctly (500 + 250) and matches /me/water for the same date.
        Assert.Equal(750, water.GetProperty("consumedMl").GetInt32());
        Assert.Equal(waterDay.GetProperty("consumedMl").GetInt32(), water.GetProperty("consumedMl").GetInt32());

        // Goal present and matches /me/water; remaining = goal - consumed.
        Assert.Equal(ExpectedWaterGoalMl, water.GetProperty("goalMl").GetInt32());
        Assert.Equal(waterDay.GetProperty("goalMl").GetInt32(), water.GetProperty("goalMl").GetInt32());
        Assert.Equal(
            water.GetProperty("goalMl").GetInt32() - water.GetProperty("consumedMl").GetInt32(),
            water.GetProperty("remainingMl").GetInt32());
        Assert.Equal(ExpectedWaterGoalMl - 750, water.GetProperty("remainingMl").GetInt32());
    }

    // -- Exercise entries: burned + net calories (consumed - burned), non-null ---

    [Fact]
    public async Task GetDashboard_WithExerciseEntries_ReturnsBurnedAndNetCalories()
    {
        var token = TokenFor("dash_exercise_net");
        var (foodId, _, cupServingId) = SeedGreekYogurt();
        // Profile carries body weight, which the exercise calories-burned snapshot requires.
        await Put(token, ProfileRoute, CompleteProfile);

        // Some diary so net calories has a consumption side: 1 cup (245 g) -> 59*2.45 = 144.55 kcal.
        await PostDiary(token, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24"));

        // Log exercise (prerequisite: seeded activity + recorded body weight, both satisfied above).
        var logResponse = await PostExercise(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });
        Assert.Equal(HttpStatusCode.Created, logResponse.StatusCode);

        var dashboard = await (await Get(token, $"{DashboardRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var exerciseDay = await (await Get(token, $"{ExerciseRoute}?date=2026-06-24"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var exercise = dashboard.GetProperty("exercise");
        Assert.True(exercise.GetProperty("totalCaloriesBurned").GetDecimal() > 0m);
        Assert.Equal(1, exercise.GetProperty("entryCount").GetInt32());

        // Burn reconciles with /me/exercise for the same date.
        var burnedFromExercise = exerciseDay.GetProperty("totalCaloriesBurned").GetDecimal();
        Assert.Equal(burnedFromExercise, exercise.GetProperty("totalCaloriesBurned").GetDecimal());

        // Net calories is NOT null here (diary + exercise both present) and equals
        // round(consumed - burned).
        var netCaloriesProp = dashboard.GetProperty("netCalories");
        Assert.Equal(JsonValueKind.Number, netCaloriesProp.ValueKind);

        var consumed = dashboard.GetProperty("calories").GetProperty("calories")
            .GetProperty("consumed").GetDecimal();
        var expectedNet = (int)Math.Round(consumed - burnedFromExercise, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedNet, netCaloriesProp.GetInt32());
    }

    // -- Streak reflects consecutive active (today-relative) days + adherence ----

    [Fact]
    public async Task GetDashboard_StreakReflectsActiveDays()
    {
        var token = TokenFor("dash_streak");
        await Put(token, ProfileRoute, CompleteProfile);

        // Three consecutive active days ending today: today (diary), yesterday + day-before (water).
        // Mirrors StreaksEndpointTests: dates are computed from UtcNow captured once.
        var (foodId, _, cupServingId) = SeedGreekYogurt();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PostDiary(token, NewEntry(foodId, cupServingId, 1, "Breakfast", today.ToString("yyyy-MM-dd")));
        await PostWater(token, 500, today.AddDays(-1).ToString("yyyy-MM-dd"));
        await PostWater(token, 500, today.AddDays(-2).ToString("yyyy-MM-dd"));

        var body = await (await Get(token, DashboardRoute)).Content.ReadFromJsonAsync<JsonElement>();

        // Default date (no query param) is today.
        Assert.Equal(today.ToString("yyyy-MM-dd"), body.GetProperty("date").GetString());

        var streak = body.GetProperty("streak");
        // Three consecutive active days ending today -> current streak 3.
        Assert.Equal(3, streak.GetProperty("currentStreak").GetInt32());
        Assert.True(streak.GetProperty("longestStreak").GetInt32() >= 3);

        // Profile is complete -> adherence fields are present (non-null).
        Assert.Equal(JsonValueKind.Number, streak.GetProperty("caloriesAdherence7d").ValueKind);
        Assert.Equal(JsonValueKind.Number, streak.GetProperty("waterAdherence7d").ValueKind);
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

    private async Task<HttpResponseMessage> PostExercise(string token, object body) =>
        await PostTo(token, ExerciseRoute, body);

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
