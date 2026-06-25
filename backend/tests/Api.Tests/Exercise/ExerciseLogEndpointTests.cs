using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Exercise;

/// <summary>
/// Integration tests for the exercise-log endpoints (issue #34):
/// <c>POST /api/v1/me/exercise</c>, <c>GET /api/v1/me/exercise?date=</c>,
/// <c>PUT /api/v1/me/exercise/{id}</c>, and <c>DELETE /api/v1/me/exercise/{id}</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>), whose
/// <c>EnsureCreated</c> applies the catalog <c>HasData</c> seed so the seeded activities (and their
/// known MET values) are present. The profile (which supplies <c>LatestWeightKg</c> for the
/// calories-burned snapshot) and the entries themselves are created through the public API exactly
/// as a client would. Each test uses a unique <c>sub</c> claim so provisioned users never collide on
/// the shared database, which keeps cross-user isolation and per-user weight tests independent.
/// </remarks>
public sealed class ExerciseLogEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string ExerciseRoute = "/api/v1/me/exercise";
    private const string ExercisesCatalogRoute = "/api/v1/exercises";
    private const string ProfileRoute = "/api/v1/me/profile";

    // A seeded shared activity with a known MET (Running, 6 mph), per the catalog seed. The id is a
    // stable HasData literal; the MET (9.8) is read from the catalog at runtime so the expected
    // calories are derived rather than hard-coded.
    private static readonly Guid RunningActivityId =
        new("01975a00-0001-7000-8000-000000000002");

    // A complete profile carrying a known body weight (70 kg). LatestWeightKg drives the
    // calories-burned snapshot: kcal = round(MET × weightKg × minutes/60, AwayFromZero).
    private static object ProfileWithWeight(double weightKg) => new
    {
        heightCm = 178.0,
        dateOfBirth = "1990-01-01",
        biologicalSex = "Male",
        activityLevel = "ModeratelyActive",
        primaryGoal = "Lose",
        weightKg,
    };

    private readonly HttpClient _client;

    public ExerciseLogEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Auth guard: every exercise-log route requires a bearer token ─────────────────

    [Fact]
    public async Task LogExercise_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ExerciseRoute)
        {
            Content = JsonContent.Create(
                new { exerciseActivityId = RunningActivityId, durationMinutes = 30, date = "2026-06-24" }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetExerciseDay_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{ExerciseRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EditExercise_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{ExerciseRoute}/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { durationMinutes = 30, date = "2026-06-24" }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteExercise_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"{ExerciseRoute}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST happy path: entry saved + day totals returned ───────────────────────────

    [Fact]
    public async Task LogExercise_ValidRequest_Returns201_WithDayResponse()
    {
        var token = TokenFor("ex_log_valid");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        var response = await Post(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/v1/me/exercise/", response.Headers.Location!.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-24", body.GetProperty("date").GetString());
        Assert.True(body.GetProperty("totalCaloriesBurned").GetInt32() > 0);
        Assert.Equal(1, body.GetProperty("entryCount").GetInt32());

        var entries = body.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        var entry = entries[0];
        Assert.Equal(RunningActivityId, entry.GetProperty("exerciseActivityId").GetGuid());
        Assert.Contains("Running", entry.GetProperty("activityName").GetString()!);
        Assert.Equal("Cardio", entry.GetProperty("activityCategory").GetString());
        Assert.Equal(30, entry.GetProperty("durationMinutes").GetInt32());
        Assert.True(entry.GetProperty("caloriesBurned").GetInt32() > 0);
    }

    // ── THE key correctness test: calories burned is the MET snapshot ────────────────

    [Fact]
    public async Task LogExercise_ComputesCaloriesBurned_Correctly()
    {
        var token = TokenFor("ex_log_kcal");
        const double weightKg = 70.0;
        const int durationMinutes = 30;
        await Put(token, ProfileRoute, ProfileWithWeight(weightKg));

        // Derive the expected kcal from the activity's catalog MET so the test is not a magic number:
        // kcal = round(MET × weightKg × minutes/60, AwayFromZero). For Running (9.8) at 70 kg / 30 min
        // this is round(9.8 × 70 × 0.5) = 343.
        var met = await GetActivityMetValue(token, RunningActivityId);
        var expectedKcal = (int)Math.Round(met * weightKg * (durationMinutes / 60.0), MidpointRounding.AwayFromZero);

        var response = await Post(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entry = body.GetProperty("entries")[0];

        Assert.Equal(expectedKcal, entry.GetProperty("caloriesBurned").GetInt32());
        Assert.Equal(343, entry.GetProperty("caloriesBurned").GetInt32());
        Assert.Equal(expectedKcal, body.GetProperty("totalCaloriesBurned").GetInt32());
    }

    // ── GET day total + entries ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetExerciseDay_WithNoDate_Returns200_DefaultsToToday()
    {
        var token = TokenFor("ex_get_default_today");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        await Post(token, new { exerciseActivityId = RunningActivityId, durationMinutes = 20, date = today });

        var response = await Get(token, ExerciseRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(today, body.GetProperty("date").GetString());
        Assert.Equal(1, body.GetProperty("entryCount").GetInt32());
        Assert.True(body.GetProperty("totalCaloriesBurned").GetInt32() > 0);
    }

    [Fact]
    public async Task GetExerciseDay_ReturnsTotalAcrossEntries()
    {
        var token = TokenFor("ex_get_total");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        await Post(token, new { exerciseActivityId = RunningActivityId, durationMinutes = 30, date = "2026-06-24" });
        await Post(token, new { exerciseActivityId = RunningActivityId, durationMinutes = 15, date = "2026-06-24" });
        // A different day must NOT contribute to this day's total.
        await Post(token, new { exerciseActivityId = RunningActivityId, durationMinutes = 99, date = "2026-06-25" });

        var response = await Get(token, $"{ExerciseRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = body.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal(2, body.GetProperty("entryCount").GetInt32());

        var expectedTotal = entries.Sum(e => e.GetProperty("caloriesBurned").GetInt32());
        Assert.Equal(expectedTotal, body.GetProperty("totalCaloriesBurned").GetInt32());
    }

    [Fact]
    public async Task GetExerciseDay_WithMalformedDate_Returns400()
    {
        var token = TokenFor("ex_get_bad_date");

        var response = await Get(token, $"{ExerciseRoute}?date=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    // ── PUT edit: duration change recomputes the kcal snapshot ───────────────────────

    [Fact]
    public async Task EditExercise_UpdatesDurationAndRecomputesCalories()
    {
        var token = TokenFor("ex_edit_recompute");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        var created = await Post(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();
        var entry = createdBody.GetProperty("entries")[0];
        var id = entry.GetProperty("id").GetGuid();
        var originalKcal = entry.GetProperty("caloriesBurned").GetInt32();

        // Doubling the duration (30 → 60 min) doubles the kcal because the snapshot is linear in
        // duration for a fixed MET and weight.
        var response = await Put(token, $"{ExerciseRoute}/{id}", new { durationMinutes = 60, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var updated = body.GetProperty("entries")[0];

        Assert.Equal(60, updated.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(originalKcal * 2, updated.GetProperty("caloriesBurned").GetInt32());
        Assert.Equal(originalKcal * 2, body.GetProperty("totalCaloriesBurned").GetInt32());
    }

    [Fact]
    public async Task EditExercise_OtherUsersEntry_Returns404()
    {
        var ownerToken = TokenFor("ex_edit_owner");
        var attackerToken = TokenFor("ex_edit_attacker");
        await Put(ownerToken, ProfileRoute, ProfileWithWeight(70.0));
        await Put(attackerToken, ProfileRoute, ProfileWithWeight(80.0));

        var created = await Post(ownerToken, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Put(attackerToken, $"{ExerciseRoute}/{id}", new { durationMinutes = 99, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteExercise_RemovesEntry_Returns204()
    {
        var token = TokenFor("ex_delete");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        var created = await Post(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Delete(token, $"{ExerciseRoute}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var day = await (await Get(token, $"{ExerciseRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, day.GetProperty("entryCount").GetInt32());
        Assert.Equal(0, day.GetProperty("totalCaloriesBurned").GetInt32());
        Assert.Equal(0, day.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task DeleteExercise_OtherUsersEntry_Returns404()
    {
        var ownerToken = TokenFor("ex_delete_owner");
        var attackerToken = TokenFor("ex_delete_attacker");
        await Put(ownerToken, ProfileRoute, ProfileWithWeight(70.0));

        var created = await Post(ownerToken, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var attackerResponse = await Delete(attackerToken, $"{ExerciseRoute}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, attackerResponse.StatusCode);

        // The owner can still see their entry — it was not deleted.
        var ownerDay = await (await Get(ownerToken, $"{ExerciseRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, ownerDay.GetProperty("entryCount").GetInt32());
    }

    // ── POST validation & visibility ─────────────────────────────────────────────────

    [Fact]
    public async Task LogExercise_InvalidActivityId_Returns404()
    {
        var token = TokenFor("ex_log_unknown_activity");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        // A well-formed but non-existent activity id is not leaked as a distinct status: 404.
        var response = await Post(token, new
        {
            exerciseActivityId = Guid.NewGuid(),
            durationMinutes = 30,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LogExercise_EmptyActivityId_Returns400()
    {
        var token = TokenFor("ex_log_empty_activity");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        var response = await Post(token, new
        {
            exerciseActivityId = Guid.Empty,
            durationMinutes = 30,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("exerciseActivityId", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(1441)]
    public async Task LogExercise_InvalidDuration_Returns400(int durationMinutes)
    {
        var token = TokenFor($"ex_log_bad_duration_{durationMinutes}");
        await Put(token, ProfileRoute, ProfileWithWeight(70.0));

        var response = await Post(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("durationMinutes", out _));
    }

    [Fact]
    public async Task LogExercise_WithNoRecordedWeight_Returns422()
    {
        // A fresh user with NO profile (hence no recorded weight) cannot have kcal estimated.
        var token = TokenFor("ex_log_no_weight");

        var response = await Post(token, new
        {
            exerciseActivityId = RunningActivityId,
            durationMinutes = 30,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task LogExercise_CannotSeeOtherUsersCustomActivity_Returns404()
    {
        var ownerToken = TokenFor("ex_log_custom_owner");
        var otherToken = TokenFor("ex_log_custom_other");
        await Put(otherToken, ProfileRoute, ProfileWithWeight(70.0));

        // User A creates a custom activity; user B must not be able to log it.
        var createResponse = await PostTo(ownerToken, ExercisesCatalogRoute, new
        {
            name = $"Owner Custom {Guid.NewGuid():N}",
            category = "Other",
            metValue = 6.0,
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var customId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var response = await Post(otherToken, new
        {
            exerciseActivityId = customId,
            durationMinutes = 30,
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    /// <summary>
    /// Reads an activity's MET value from the catalog (<c>GET /api/v1/exercises</c>) so the expected
    /// calories-burned value is derived from the live seed rather than hard-coded.
    /// </summary>
    private async Task<double> GetActivityMetValue(string token, Guid activityId)
    {
        var listResponse = await Get(token, ExercisesCatalogRoute);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var items = (await listResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray();

        var match = items.Single(i => i.GetProperty("id").GetGuid() == activityId);
        return match.GetProperty("metValue").GetDouble();
    }

    private async Task<HttpResponseMessage> Post(string token, object body) =>
        await PostTo(token, ExerciseRoute, body);

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

    private async Task<HttpResponseMessage> Delete(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
