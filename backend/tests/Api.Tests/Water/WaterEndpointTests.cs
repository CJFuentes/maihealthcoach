using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Water;

/// <summary>
/// Integration tests for the water-log endpoints (issue #31):
/// <c>POST /api/v1/me/water</c>, <c>GET /api/v1/me/water?date=</c>,
/// <c>PUT /api/v1/me/water/{id}</c>, and <c>DELETE /api/v1/me/water/{id}</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). The profile
/// (so the water goal computes) and the water entries are created through the public API exactly as
/// a client would. Each test uses a unique <c>sub</c> claim so provisioned users never collide on
/// the shared database.
/// </remarks>
public sealed class WaterEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string WaterRoute = "/api/v1/me/water";
    private const string ProfileRoute = "/api/v1/me/profile";
    private const string GoalOverridesRoute = "/api/v1/me/goals/overrides";

    // A complete profile whose water target is deterministic:
    //   water = 80 kg * 35 ml/kg + 350 (ModeratelyActive bump) = 3150 ml.
    private static readonly object CompleteProfile = new
    {
        heightCm = 178.0,
        dateOfBirth = "1990-01-01",
        biologicalSex = "Male",
        activityLevel = "ModeratelyActive",
        primaryGoal = "Lose",
        weightKg = 80.0,
    };

    private const int ExpectedGoalMl = 3150;

    private readonly HttpClient _client;

    public WaterEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Auth guard: every water route requires a bearer token ────────────────────

    [Fact]
    public async Task AddEntry_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WaterRoute)
        {
            Content = JsonContent.Create(new { amountMl = 500, date = "2026-06-24" }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDay_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{WaterRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EditEntry_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{WaterRoute}/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { amountMl = 500, date = "2026-06-24" }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEntry_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"{WaterRoute}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST happy path: entry saved + day totals returned ───────────────────────

    [Fact]
    public async Task AddEntry_ValidRequestNoProfile_Returns201WithDayTotalsAndNullGoal()
    {
        var token = TokenFor("water_add_no_profile");

        var response = await Post(token, new { amountMl = 500, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-24", body.GetProperty("date").GetString());
        Assert.False(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(500, body.GetProperty("consumedMl").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("goalMl").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("remainingMl").ValueKind);

        var entries = body.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal(500, entries[0].GetProperty("amountMl").GetInt32());
    }

    [Fact]
    public async Task AddEntry_WithProfile_Returns201WithGoalAndRemaining()
    {
        var token = TokenFor("water_add_with_profile");
        await Put(token, ProfileRoute, CompleteProfile);

        var response = await Post(token, new { amountMl = 500, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(500, body.GetProperty("consumedMl").GetInt32());
        Assert.Equal(ExpectedGoalMl, body.GetProperty("goalMl").GetInt32());
        Assert.Equal(ExpectedGoalMl - 500, body.GetProperty("remainingMl").GetInt32());
    }

    [Fact]
    public async Task AddEntry_QuickAddAccumulatesRunningTotal()
    {
        var token = TokenFor("water_add_accumulate");
        await Put(token, ProfileRoute, CompleteProfile);

        await Post(token, new { amountMl = 500, date = "2026-06-24" });
        var second = await Post(token, new { amountMl = 250, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(750, body.GetProperty("consumedMl").GetInt32());
        Assert.Equal(ExpectedGoalMl - 750, body.GetProperty("remainingMl").GetInt32());
        Assert.Equal(2, body.GetProperty("entries").GetArrayLength());
    }

    // ── POST validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddEntry_ZeroAmount_Returns400WithFieldError()
    {
        var token = TokenFor("water_add_zero");

        var response = await Post(token, new { amountMl = 0, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("amountMl", out _));
    }

    [Fact]
    public async Task AddEntry_NegativeAmount_Returns400WithFieldError()
    {
        var token = TokenFor("water_add_negative");

        var response = await Post(token, new { amountMl = -100, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("amountMl", out _));
    }

    [Fact]
    public async Task AddEntry_OverCapAmount_Returns400WithFieldError()
    {
        var token = TokenFor("water_add_over_cap");

        var response = await Post(token, new { amountMl = 10_001, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("amountMl", out _));
    }

    [Fact]
    public async Task AddEntry_InvalidDate_Returns400WithFieldError()
    {
        var token = TokenFor("water_add_bad_date");

        var response = await Post(token, new { amountMl = 500, date = "24-06-2026" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    // ── GET day total + entries ──────────────────────────────────────────────────

    [Fact]
    public async Task GetDay_WithEntries_ReturnsTotalAndEntries()
    {
        var token = TokenFor("water_get_entries");

        await Post(token, new { amountMl = 500, date = "2026-06-24" });
        await Post(token, new { amountMl = 300, date = "2026-06-24" });
        // A different day must NOT contribute to this day's total.
        await Post(token, new { amountMl = 999, date = "2026-06-25" });

        var response = await Get(token, $"{WaterRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-24", body.GetProperty("date").GetString());
        Assert.Equal(800, body.GetProperty("consumedMl").GetInt32());
        Assert.Equal(2, body.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task GetDay_EmptyDay_ReturnsZeroConsumedAndEmptyEntries()
    {
        var token = TokenFor("water_get_empty");

        var response = await Get(token, $"{WaterRoute}?date=2026-01-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("consumedMl").GetInt32());
        Assert.Equal(0, body.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task GetDay_NoDateParam_DefaultsToTodayUtc()
    {
        var token = TokenFor("water_get_default_today");
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var response = await Get(token, WaterRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(today, body.GetProperty("date").GetString());
    }

    [Fact]
    public async Task GetDay_MalformedDate_Returns400WithFieldError()
    {
        var token = TokenFor("water_get_bad_date");

        var response = await Get(token, $"{WaterRoute}?date=not-a-date");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    [Fact]
    public async Task GetDay_NoProfile_GoalsAvailableFalse()
    {
        var token = TokenFor("water_get_no_profile");
        await Post(token, new { amountMl = 250, date = "2026-06-24" });

        var response = await Get(token, $"{WaterRoute}?date=2026-06-24");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("goalMl").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("remainingMl").ValueKind);
    }

    [Fact]
    public async Task GetDay_WithProfile_ReturnsGoalAndRemaining()
    {
        var token = TokenFor("water_get_with_profile");
        await Put(token, ProfileRoute, CompleteProfile);
        await Post(token, new { amountMl = 1000, date = "2026-06-24" });

        var response = await Get(token, $"{WaterRoute}?date=2026-06-24");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("goalsAvailable").GetBoolean());
        Assert.Equal(ExpectedGoalMl, body.GetProperty("goalMl").GetInt32());
        Assert.Equal(ExpectedGoalMl - 1000, body.GetProperty("remainingMl").GetInt32());
    }

    [Fact]
    public async Task GetDay_WithWaterOverride_UsesOverriddenGoal()
    {
        var token = TokenFor("water_get_override");
        await Put(token, ProfileRoute, CompleteProfile);
        // Override the water goal to 4000 ml; consumed-vs-goal must reflect the override.
        await Put(token, GoalOverridesRoute, new { waterMl = 4000 });
        await Post(token, new { amountMl = 500, date = "2026-06-24" });

        var response = await Get(token, $"{WaterRoute}?date=2026-06-24");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4000, body.GetProperty("goalMl").GetInt32());
        Assert.Equal(3500, body.GetProperty("remainingMl").GetInt32());
    }

    // ── PUT edit (incl. over-goal negative remaining) ────────────────────────────

    [Fact]
    public async Task EditEntry_ChangesAmount_Returns200WithUpdatedDayTotals()
    {
        var token = TokenFor("water_edit");
        await Put(token, ProfileRoute, CompleteProfile);

        var created = await Post(token, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Put(token, $"{WaterRoute}/{id}", new { amountMl = 750, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(750, body.GetProperty("consumedMl").GetInt32());
        Assert.Equal(ExpectedGoalMl - 750, body.GetProperty("remainingMl").GetInt32());
    }

    [Fact]
    public async Task EditEntry_MovesToAnotherDay_RemovesFromOldDay()
    {
        var token = TokenFor("water_edit_move_day");

        var created = await Post(token, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Put(token, $"{WaterRoute}/{id}", new { amountMl = 500, date = "2026-06-30" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-30", body.GetProperty("date").GetString());

        var oldDay = await (await Get(token, $"{WaterRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, oldDay.GetProperty("consumedMl").GetInt32());
        var newDay = await (await Get(token, $"{WaterRoute}?date=2026-06-30")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, newDay.GetProperty("consumedMl").GetInt32());
    }

    [Fact]
    public async Task EditEntry_OverGoal_RemainingIsNegative()
    {
        var token = TokenFor("water_edit_over_goal");
        await Put(token, ProfileRoute, CompleteProfile);

        var created = await Post(token, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        // 5000 ml exceeds the 3150 ml goal → remaining must be negative, not clamped.
        var response = await Put(token, $"{WaterRoute}/{id}", new { amountMl = 5000, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ExpectedGoalMl - 5000, body.GetProperty("remainingMl").GetInt32());
    }

    [Fact]
    public async Task EditEntry_UnknownId_Returns404()
    {
        var token = TokenFor("water_edit_unknown");

        var response = await Put(token, $"{WaterRoute}/{Guid.NewGuid()}", new { amountMl = 500, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditEntry_ZeroAmount_Returns400()
    {
        var token = TokenFor("water_edit_zero");

        var created = await Post(token, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Put(token, $"{WaterRoute}/{id}", new { amountMl = 0, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEntry_Existing_Returns204ThenGoneFromDay()
    {
        var token = TokenFor("water_delete");

        var created = await Post(token, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Delete(token, $"{WaterRoute}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var day = await (await Get(token, $"{WaterRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, day.GetProperty("consumedMl").GetInt32());
        Assert.Equal(0, day.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task DeleteEntry_UnknownId_Returns404()
    {
        var token = TokenFor("water_delete_unknown");

        var response = await Delete(token, $"{WaterRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Cross-user isolation: another user's entry is invisible (404) ────────────

    [Fact]
    public async Task GetDay_OnlyReturnsCurrentUsersEntries()
    {
        var userAToken = TokenFor("water_xuser_list_a");
        var userBToken = TokenFor("water_xuser_list_b");

        await Post(userAToken, new { amountMl = 500, date = "2026-07-01" });
        await Post(userBToken, new { amountMl = 999, date = "2026-07-01" });

        var aDay = await (await Get(userAToken, $"{WaterRoute}?date=2026-07-01")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, aDay.GetProperty("consumedMl").GetInt32());
        Assert.Equal(1, aDay.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task EditEntry_OtherUsersEntry_Returns404()
    {
        var ownerToken = TokenFor("water_xuser_owner_edit");
        var attackerToken = TokenFor("water_xuser_attacker_edit");

        var created = await Post(ownerToken, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var response = await Put(attackerToken, $"{WaterRoute}/{id}", new { amountMl = 999, date = "2026-06-24" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEntry_OtherUsersEntry_Returns404AndEntrySurvives()
    {
        var ownerToken = TokenFor("water_xuser_owner_del");
        var attackerToken = TokenFor("water_xuser_attacker_del");

        var created = await Post(ownerToken, new { amountMl = 500, date = "2026-06-24" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("entries")[0].GetProperty("id").GetGuid();

        var attackerResponse = await Delete(attackerToken, $"{WaterRoute}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, attackerResponse.StatusCode);

        // The owner can still see their entry — it was not deleted.
        var ownerDay = await (await Get(ownerToken, $"{WaterRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, ownerDay.GetProperty("consumedMl").GetInt32());
        Assert.Equal(1, ownerDay.GetProperty("entries").GetArrayLength());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private async Task<HttpResponseMessage> Post(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, WaterRoute)
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
