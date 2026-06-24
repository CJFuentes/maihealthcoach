using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Profiles;

/// <summary>
/// Integration tests for the authenticated profile endpoints (issue #16):
/// <c>GET /api/v1/me/profile</c> and <c>PUT /api/v1/me/profile</c>. Tokens are signed
/// locally and the database is SQLite in-memory — no Clerk servers and no Postgres.
/// </summary>
/// <remarks>
/// The class fixture keeps ONE in-memory SQLite database alive for the whole class, so
/// every test uses a UNIQUE clerk user id (<c>sub</c>) to provision a distinct user and
/// profile and avoid cross-contamination between tests.
/// </remarks>
public sealed class ProfileEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string ProfileRoute = "/api/v1/me/profile";

    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProfileEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── 1 & 2: unauthenticated access ────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(ProfileRoute);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutProfile_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, ProfileRoute)
        {
            Content = JsonContent.Create(new { heightCm = 180.0 }),
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 3: GET before any PUT => 404 ─────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_FreshUserBeforeAnyPut_Returns404()
    {
        var token = TokenFor("user_profile_get_404");

        var response = await Get(token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 4: full PUT then GET round-trip ──────────────────────────────────────────

    [Fact]
    public async Task PutProfile_FullValidBody_PersistsAndEchoesAllValues()
    {
        var token = TokenFor("user_profile_full_put");

        var body = new
        {
            heightCm = 178.5,
            dateOfBirth = "1990-04-15",
            biologicalSex = "Male",
            activityLevel = "ModeratelyActive",
            primaryGoal = "Lose",
            units = "Metric",
            dietType = "Vegetarian",
            allergies = "peanuts, shellfish",
            weightKg = 80.0,
        };

        var putResponse = await Put(token, body);

        // Upsert of a brand-new profile returns 201 Created; accept 200 too for robustness.
        Assert.True(
            putResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected 200 or 201 but got {(int)putResponse.StatusCode}.");

        var put = await putResponse.Content.ReadFromJsonAsync<JsonElement>();
        AssertFullProfile(put);
        Assert.Equal(1, put.GetProperty("weightHistory").GetArrayLength());

        // The values must survive a fresh read.
        var getResponse = await Get(token);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var got = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        AssertFullProfile(got);
    }

    private static void AssertFullProfile(JsonElement profile)
    {
        Assert.Equal(178.5, profile.GetProperty("heightCm").GetDouble());
        Assert.Equal("1990-04-15", profile.GetProperty("dateOfBirth").GetString());
        Assert.Equal("Male", profile.GetProperty("biologicalSex").GetString());
        Assert.Equal("ModeratelyActive", profile.GetProperty("activityLevel").GetString());
        Assert.Equal("Lose", profile.GetProperty("primaryGoal").GetString());
        Assert.Equal("Metric", profile.GetProperty("units").GetString());
        Assert.Equal("Vegetarian", profile.GetProperty("dietType").GetString());
        Assert.Equal("peanuts, shellfish", profile.GetProperty("allergies").GetString());
        Assert.Equal(80.0, profile.GetProperty("latestWeightKg").GetDouble());
    }

    // ── 5: second PUT mutates scalar fields ──────────────────────────────────────

    [Fact]
    public async Task PutProfile_SecondPut_UpdatesScalarFields()
    {
        var token = TokenFor("user_profile_scalar_update");

        await Put(token, new
        {
            heightCm = 170.0,
            primaryGoal = "Maintain",
            units = "Metric",
        });

        await Put(token, new
        {
            heightCm = 172.0,
            primaryGoal = "Gain",
            units = "Imperial",
        });

        var got = await (await Get(token)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(172.0, got.GetProperty("heightCm").GetDouble());
        Assert.Equal("Gain", got.GetProperty("primaryGoal").GetString());
        Assert.Equal("Imperial", got.GetProperty("units").GetString());
    }

    // ── 6: weight time-series behaviour ──────────────────────────────────────────

    [Fact]
    public async Task PutProfile_WeightTimeSeries_AppendsOnChangeAndSuppressesDuplicates()
    {
        var token = TokenFor("user_profile_weight_series");

        // First weight => 1 entry.
        var afterFirst = await (await Put(token, new { weightKg = 80.0 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, afterFirst.GetProperty("weightHistory").GetArrayLength());
        Assert.Equal(80.0, afterFirst.GetProperty("latestWeightKg").GetDouble());

        // Changed weight => 2 entries, latest reflects new value.
        var afterSecond = await (await Put(token, new { weightKg = 82.0 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, afterSecond.GetProperty("weightHistory").GetArrayLength());
        Assert.Equal(82.0, afterSecond.GetProperty("latestWeightKg").GetDouble());

        // Same weight again => changed-value guard suppresses the append (still 2).
        var afterDuplicate = await (await Put(token, new { weightKg = 82.0 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, afterDuplicate.GetProperty("weightHistory").GetArrayLength());
        Assert.Equal(82.0, afterDuplicate.GetProperty("latestWeightKg").GetDouble());

        // PUT with no weight at all => no append (still 2).
        var afterNoWeight = await (await Put(token, new { heightCm = 175.0 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, afterNoWeight.GetProperty("weightHistory").GetArrayLength());
        Assert.Equal(82.0, afterNoWeight.GetProperty("latestWeightKg").GetDouble());
    }

    // ── 7: validation failures => 400 problem+json, field error, no persistence ──

    [Theory]
    [InlineData("user_val_height_small", "heightCm", 10.0, "weightKg")]
    [InlineData("user_val_height_large", "heightCm", 500.0, "weightKg")]
    public async Task PutProfile_HeightOutOfRange_Returns400WithFieldError(
        string sub, string field, double height, string _)
    {
        var token = TokenFor(sub);

        var response = await Put(token, new { heightCm = height });

        await AssertValidationProblem(response, field);
        await AssertProfileNotPersisted(token);
    }

    [Theory]
    [InlineData("user_val_weight_small", 5.0)]
    [InlineData("user_val_weight_large", 999.0)]
    public async Task PutProfile_WeightOutOfRange_Returns400WithFieldError(string sub, double weight)
    {
        var token = TokenFor(sub);

        var response = await Put(token, new { weightKg = weight });

        await AssertValidationProblem(response, "weightKg");
        await AssertProfileNotPersisted(token);
    }

    [Theory]
    [InlineData("user_val_dob_young", "2020-01-01")] // age < 13
    [InlineData("user_val_dob_old", "1850-01-01")]   // age > 120
    public async Task PutProfile_ImplausibleDateOfBirth_Returns400WithFieldError(
        string sub, string dob)
    {
        var token = TokenFor(sub);

        var response = await Put(token, new { dateOfBirth = dob });

        await AssertValidationProblem(response, "dateOfBirth");
        await AssertProfileNotPersisted(token);
    }

    [Theory]
    [InlineData("user_val_enum_activity", "activityLevel", "superhuman")]
    [InlineData("user_val_enum_goal", "primaryGoal", "destroy")]
    public async Task PutProfile_InvalidEnumValue_Returns400WithFieldError(
        string sub, string field, string value)
    {
        var token = TokenFor(sub);

        object body = field == "activityLevel"
            ? new { activityLevel = value }
            : new { primaryGoal = value };

        var response = await Put(token, body);

        await AssertValidationProblem(response, field);
        await AssertProfileNotPersisted(token);
    }

    // ── 8: enum case-insensitivity ───────────────────────────────────────────────

    [Fact]
    public async Task PutProfile_LowercaseEnumValues_AreAccepted()
    {
        var token = TokenFor("user_profile_lowercase_enums");

        var response = await Put(token, new
        {
            units = "metric",
            activityLevel = "sedentary",
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"Expected 200 or 201 but got {(int)response.StatusCode}.");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Metric", body.GetProperty("units").GetString());
        Assert.Equal("Sedentary", body.GetProperty("activityLevel").GetString());
    }

    // ── 9: expired token => 401 ──────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_WithExpiredToken_Returns401()
    {
        var token = JwtTestHelper.CreateToken(
            "user_profile_expired", "expired-profile@test.local", expired: true);

        var response = await Get(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private async Task<HttpResponseMessage> Get(string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProfileRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Put(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, ProfileRoute)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static async Task AssertValidationProblem(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(
            problem.TryGetProperty("errors", out var errors),
            "Validation problem response is missing an 'errors' object.");
        Assert.True(
            errors.TryGetProperty(field, out _),
            $"Validation problem 'errors' object is missing the '{field}' key.");
    }

    private async Task AssertProfileNotPersisted(string token)
    {
        var response = await Get(token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
