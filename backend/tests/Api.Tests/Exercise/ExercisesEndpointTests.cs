using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Exercise;

/// <summary>
/// Integration tests for the exercise catalog endpoints (issue #33):
/// <c>GET /api/v1/exercises</c> (list/search) and <c>POST /api/v1/exercises</c> (create custom).
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>), whose
/// <c>EnsureCreated</c> applies the catalog <c>HasData</c> seed, so the seeded activities are
/// present in the test database. Each test uses a unique <c>sub</c> claim so provisioned users
/// never collide on the shared database, which keeps custom-activity visibility tests isolated.
/// </remarks>
public sealed class ExercisesEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string ExercisesRoute = "/api/v1/exercises";

    private readonly HttpClient _client;

    public ExercisesEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Auth guard: every route requires a bearer token ──────────────────────────────────────

    [Fact]
    public async Task ListExercises_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(ExercisesRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateExercise_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ExercisesRoute)
        {
            Content = JsonContent.Create(new { name = "Test", category = "Cardio", metValue = 5.0 }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET: seed data present and queryable ─────────────────────────────────────────────────

    [Fact]
    public async Task ListExercises_WithToken_Returns200AndContainsSeededActivities()
    {
        var token = TokenFor("ex_list_seed");

        var response = await Get(token, ExercisesRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var count = body.GetProperty("count").GetInt32();
        Assert.True(count >= 17, $"Expected at least 17 seeded activities, got {count}.");

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i =>
            i.GetProperty("name").GetString()!.Contains("Running", StringComparison.OrdinalIgnoreCase)
            && i.GetProperty("category").GetString() == "Cardio");
        Assert.Contains(items, i =>
            i.GetProperty("name").GetString()!.Contains("Yoga", StringComparison.OrdinalIgnoreCase)
            && i.GetProperty("category").GetString() == "Flexibility");

        // All seeded shared activities report IsCustom == false.
        Assert.All(items, i => Assert.False(i.GetProperty("isCustom").GetBoolean()));
    }

    [Fact]
    public async Task ListExercises_WithQFilter_ReturnsOnlyMatchingNames_CaseInsensitive()
    {
        var token = TokenFor("ex_list_qfilter");

        // Lowercase query must still match seeded "Yoga (...)" — proves case-insensitive search
        // (the production-vs-test parity guarantee from EF.Functions.Like over ToLower()).
        var response = await Get(token, $"{ExercisesRoute}?q=yoga");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.True(items.Count >= 2, $"Expected at least 2 yoga seed rows, got {items.Count}.");
        Assert.All(items, i =>
            Assert.Contains("yoga", i.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListExercises_WithWildcardOnlyQuery_DoesNotMatchEverything()
    {
        var token = TokenFor("ex_list_wildcard");

        // A literal '%' is escaped, so it matches a literal percent sign in a name. No seeded
        // activity name contains '%', so the result is empty — proving the wildcard was escaped
        // rather than treated as "match all".
        var response = await Get(token, $"{ExercisesRoute}?q=%25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task ListExercises_WithCategoryFilter_ReturnsOnlyCategoryItems()
    {
        var token = TokenFor("ex_list_category");

        var response = await Get(token, $"{ExercisesRoute}?category=Strength");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.True(items.Count >= 3, $"Expected at least 3 Strength seed rows, got {items.Count}.");
        Assert.All(items, i => Assert.Equal("Strength", i.GetProperty("category").GetString()));
    }

    [Fact]
    public async Task ListExercises_WithCategoryFilter_IsCaseInsensitive()
    {
        var token = TokenFor("ex_list_category_ci");

        var response = await Get(token, $"{ExercisesRoute}?category=strength");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("count").GetInt32() >= 3);
    }

    [Fact]
    public async Task ListExercises_WithInvalidCategory_Returns400WithFieldError()
    {
        var token = TokenFor("ex_list_bad_category");

        var response = await Get(token, $"{ExercisesRoute}?category=NotARealCategory");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("category", out _));
    }

    [Fact]
    public async Task ListExercises_WithBothFilters_AppliesAndSemantics()
    {
        var token = TokenFor("ex_list_both");

        var response = await Get(token, $"{ExercisesRoute}?q=running&category=Cardio");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.All(items, i =>
        {
            Assert.Contains("running", i.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Cardio", i.GetProperty("category").GetString());
        });
    }

    // ── GET: visibility isolation across users ───────────────────────────────────────────────

    [Fact]
    public async Task ListExercises_DoesNotReturnOtherUsersCustomActivities()
    {
        var tokenA = TokenFor("ex_visibility_userA");
        var tokenB = TokenFor("ex_visibility_userB");

        // User A creates a uniquely named custom activity.
        var uniqueName = $"Owner A Secret {Guid.NewGuid():N}";
        var createResponse = await Post(tokenA, new { name = uniqueName, category = "Other", metValue = 6.0 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdId = created.GetProperty("id").GetGuid();

        // User B must not see User A's custom activity.
        var listResponse = await Get(tokenB, ExercisesRoute);
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(items, i => i.GetProperty("id").GetGuid() == createdId);
        Assert.DoesNotContain(items, i => i.GetProperty("name").GetString() == uniqueName);
    }

    // ── POST: create custom activity ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateExercise_ValidRequest_Returns201WithBody()
    {
        var token = TokenFor("ex_create_valid");

        var response = await Post(token, new
        {
            name = "Indoor Rock Climbing",
            category = "Other",
            metValue = 5.8,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/v1/exercises/", response.Headers.Location!.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal("Indoor Rock Climbing", body.GetProperty("name").GetString());
        Assert.Equal("Other", body.GetProperty("category").GetString());
        Assert.Equal(5.8m, body.GetProperty("metValue").GetDecimal());
        Assert.True(body.GetProperty("isCustom").GetBoolean());
    }

    [Fact]
    public async Task CreateExercise_ThenAppearsInCallersListAsCustom()
    {
        var token = TokenFor("ex_create_then_list");

        var name = $"My Custom Activity {Guid.NewGuid():N}";
        var createResponse = await Post(token, new { name, category = "Cardio", metValue = 7.0 });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var listResponse = await Get(token, ExercisesRoute);
        var items = (await listResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToList();

        var mine = items.SingleOrDefault(i => i.GetProperty("id").GetGuid() == createdId);
        Assert.True(mine.ValueKind != JsonValueKind.Undefined, "Created activity not found in caller's list.");
        Assert.True(mine.GetProperty("isCustom").GetBoolean());
    }

    [Theory]
    [InlineData("", "Cardio", 5.0, "name")]
    [InlineData("Test", "Burpees", 5.0, "category")]
    [InlineData("Test", "", 5.0, "category")]
    [InlineData("Test", "Cardio", 0, "metValue")]
    [InlineData("Test", "Cardio", -1.0, "metValue")]
    [InlineData("Test", "Cardio", 100.0, "metValue")]
    public async Task CreateExercise_InvalidRequest_Returns400WithFieldError(
        string name, string category, double metValue, string expectedErrorKey)
    {
        var token = TokenFor($"ex_create_invalid_{expectedErrorKey}_{Guid.NewGuid():N}");

        var response = await Post(token, new { name, category, metValue });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(
            body.GetProperty("errors").TryGetProperty(expectedErrorKey, out _),
            $"Expected a '{expectedErrorKey}' validation error.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Post(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ExercisesRoute)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
