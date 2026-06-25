using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Notifications;

/// <summary>
/// Integration tests for the reminder-preferences endpoints (issue #48):
/// <c>GET /api/v1/me/reminder-preferences</c> and <c>PUT /api/v1/me/reminder-preferences</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Preferences are
/// read/upserted through the public API exactly as a client would. Each test uses a unique <c>sub</c>
/// claim so provisioned users never collide on the shared database. Time-of-day values round-trip as
/// <c>"HH:mm"</c> strings.
/// </remarks>
public sealed class ReminderPreferencesEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string PreferencesRoute = "/api/v1/me/reminder-preferences";

    private readonly HttpClient _client;

    public ReminderPreferencesEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // A fully-populated, valid preferences payload used by the round-trip tests.
    private static object FullPreferences => new
    {
        mealRemindersEnabled = true,
        waterRemindersEnabled = true,
        mealReminderTimes = new[] { "08:00", "12:30" },
        waterReminderTime = "10:00",
        quietHoursStart = "22:00",
        quietHoursEnd = "07:00",
        utcOffsetMinutes = -300,
    };

    // ── Auth guard ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(PreferencesRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, PreferencesRoute)
        {
            Content = JsonContent.Create(FullPreferences),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET defaults before any PUT ──────────────────────────────────────────────

    [Fact]
    public async Task Get_BeforeAnyPut_Returns200WithDisabledDefaults()
    {
        var token = TokenFor("prefs_get_defaults");

        var response = await Get(token, PreferencesRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("mealRemindersEnabled").GetBoolean());
        Assert.False(body.GetProperty("waterRemindersEnabled").GetBoolean());
        Assert.Equal(0, body.GetProperty("mealReminderTimes").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("waterReminderTime").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("quietHoursStart").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("quietHoursEnd").ValueKind);
        Assert.Equal(0, body.GetProperty("utcOffsetMinutes").GetInt32());
    }

    // ── PUT create then GET round-trip ───────────────────────────────────────────

    [Fact]
    public async Task Put_CreatesPreferences_Returns201ThenGetReturnsSavedValues()
    {
        var token = TokenFor("prefs_put_create");

        var putResponse = await Put(token, PreferencesRoute, FullPreferences);
        Assert.Equal(HttpStatusCode.Created, putResponse.StatusCode);

        var getResponse = await Get(token, PreferencesRoute);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mealRemindersEnabled").GetBoolean());
        Assert.True(body.GetProperty("waterRemindersEnabled").GetBoolean());

        var times = body.GetProperty("mealReminderTimes");
        Assert.Equal(2, times.GetArrayLength());
        Assert.Equal("08:00", times[0].GetString());
        Assert.Equal("12:30", times[1].GetString());

        Assert.Equal("10:00", body.GetProperty("waterReminderTime").GetString());
        Assert.Equal("22:00", body.GetProperty("quietHoursStart").GetString());
        Assert.Equal("07:00", body.GetProperty("quietHoursEnd").GetString());
        Assert.Equal(-300, body.GetProperty("utcOffsetMinutes").GetInt32());
    }

    [Fact]
    public async Task Put_SecondTime_Returns200AndReflectsUpdatedValues()
    {
        var token = TokenFor("prefs_put_update");

        var first = await Put(token, PreferencesRoute, FullPreferences);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // A second PUT is an update of the existing row → 200, not 201.
        var updated = new
        {
            mealRemindersEnabled = false,
            waterRemindersEnabled = true,
            mealReminderTimes = new[] { "09:15" },
            waterReminderTime = (string?)null,
            quietHoursStart = (string?)null,
            quietHoursEnd = (string?)null,
            utcOffsetMinutes = 60,
        };
        var second = await Put(token, PreferencesRoute, updated);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var body = await (await Get(token, PreferencesRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("mealRemindersEnabled").GetBoolean());
        Assert.True(body.GetProperty("waterRemindersEnabled").GetBoolean());
        Assert.Equal(1, body.GetProperty("mealReminderTimes").GetArrayLength());
        Assert.Equal("09:15", body.GetProperty("mealReminderTimes")[0].GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("waterReminderTime").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("quietHoursStart").ValueKind);
        Assert.Equal(60, body.GetProperty("utcOffsetMinutes").GetInt32());
    }

    // ── Cross-user isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Put_DoesNotAffectAnotherUsersPreferences()
    {
        var userAToken = TokenFor("prefs_xuser_a");
        var userBToken = TokenFor("prefs_xuser_b");

        await Put(userAToken, PreferencesRoute, FullPreferences);

        // User B never PUT, so they still see the disabled defaults.
        var bBody = await (await Get(userBToken, PreferencesRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(bBody.GetProperty("mealRemindersEnabled").GetBoolean());
        Assert.False(bBody.GetProperty("waterRemindersEnabled").GetBoolean());
        Assert.Equal(0, bBody.GetProperty("mealReminderTimes").GetArrayLength());
        Assert.Equal(0, bBody.GetProperty("utcOffsetMinutes").GetInt32());
    }

    // ── PUT validation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Put_TooManyMealReminderTimes_Returns400WithFieldError()
    {
        var token = TokenFor("prefs_put_too_many_times");

        var response = await Put(token, PreferencesRoute, new
        {
            mealRemindersEnabled = true,
            waterRemindersEnabled = false,
            mealReminderTimes = new[] { "06:00", "09:00", "12:00", "15:00", "18:00", "21:00" },
            waterReminderTime = (string?)null,
            quietHoursStart = (string?)null,
            quietHoursEnd = (string?)null,
            utcOffsetMinutes = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("mealReminderTimes", out _));
    }

    [Fact]
    public async Task Put_InvalidTimeFormat_Returns400WithFieldError()
    {
        var token = TokenFor("prefs_put_bad_time");

        var response = await Put(token, PreferencesRoute, new
        {
            mealRemindersEnabled = true,
            waterRemindersEnabled = false,
            mealReminderTimes = new[] { "25:00" },
            waterReminderTime = (string?)null,
            quietHoursStart = (string?)null,
            quietHoursEnd = (string?)null,
            utcOffsetMinutes = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("mealReminderTimes", out _));
    }

    [Fact]
    public async Task Put_OnlyOneQuietHoursBoundProvided_Returns400WithFieldError()
    {
        var token = TokenFor("prefs_put_half_quiet_hours");

        var response = await Put(token, PreferencesRoute, new
        {
            mealRemindersEnabled = false,
            waterRemindersEnabled = false,
            mealReminderTimes = Array.Empty<string>(),
            waterReminderTime = (string?)null,
            quietHoursStart = "22:00",
            quietHoursEnd = (string?)null,
            utcOffsetMinutes = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("quietHours", out _));
    }

    [Fact]
    public async Task Put_UtcOffsetOutOfRange_Returns400WithFieldError()
    {
        var token = TokenFor("prefs_put_offset_out_of_range");

        var response = await Put(token, PreferencesRoute, new
        {
            mealRemindersEnabled = false,
            waterRemindersEnabled = false,
            mealReminderTimes = Array.Empty<string>(),
            waterReminderTime = (string?)null,
            quietHoursStart = (string?)null,
            quietHoursEnd = (string?)null,
            utcOffsetMinutes = 900, // beyond the +840 cap.
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("utcOffsetMinutes", out _));
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
}
