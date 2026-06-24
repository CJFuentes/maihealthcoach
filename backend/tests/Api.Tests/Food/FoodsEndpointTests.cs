using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// Integration tests for the food search &amp; detail endpoints (issue #21):
/// <c>GET /api/v1/foods</c>, <c>GET /api/v1/foods/{id}</c>, and
/// <c>GET /api/v1/foods/barcode/{code}</c>.
/// </summary>
/// <remarks>
/// These tests focus on the endpoint-layer concerns that are <em>not</em> already covered by the
/// service-layer suite (<c>NutritionLookupServiceTests</c> / <c>FoodPersistenceTests</c>): the
/// authorization guard on every route, query-parameter validation, and the database-backed
/// not-found path for fetch-by-id. They are deliberately network-free — the validation failures
/// short-circuit before <c>INutritionLookupService</c> is invoked, and the unknown-id lookup reads
/// the (empty) in-memory database — so they never reach Open Food Facts. The cache-first / search /
/// barcode happy paths through the lookup service are exercised in depth by the service tests.
/// </remarks>
public sealed class FoodsEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string SearchRoute = "/api/v1/foods";
    private const string ByIdRoute = "/api/v1/foods/{0}";
    private const string ByBarcodeRoute = "/api/v1/foods/barcode/{0}";

    private readonly HttpClient _client;

    public FoodsEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // A minimal valid create-custom-food request body (per-100 g nutrition, no extra servings).
    private static object CustomFoodBody(string name) => new
    {
        name,
        nutrition = new
        {
            energyKcal = 120m,
            proteinG = 8m,
            carbohydrateG = 4m,
            fatG = 6m,
        },
    };

    // ── Auth guard: every food route requires a bearer token ─────────────────────

    [Fact]
    public async Task SearchFoods_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{SearchRoute}?q=apple");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFoodById_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(string.Format(ByIdRoute, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFoodByBarcode_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(string.Format(ByBarcodeRoute, "5000159484695"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Search validation (short-circuits before the lookup service is called) ────

    [Fact]
    public async Task SearchFoods_MissingQuery_Returns400WithFieldError()
    {
        var token = TokenFor("foods_search_missing_q");

        var response = await Get(token, SearchRoute);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("q", out _));
    }

    [Fact]
    public async Task SearchFoods_BlankQuery_Returns400()
    {
        var token = TokenFor("foods_search_blank_q");

        var response = await Get(token, $"{SearchRoute}?q=%20%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("q", out _));
    }

    [Fact]
    public async Task SearchFoods_PageSizeTooLarge_Returns400WithFieldError()
    {
        var token = TokenFor("foods_search_bad_pagesize");

        var response = await Get(token, $"{SearchRoute}?q=apple&pageSize=999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    // ── Fetch-by-id: unknown id => 404 ProblemDetails (reads the empty test DB) ───

    [Fact]
    public async Task GetFoodById_UnknownId_Returns404WithProblemDetails()
    {
        var token = TokenFor("foods_byid_unknown");

        var response = await Get(token, string.Format(ByIdRoute, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── Fetch-by-id: custom-food visibility is scoped to the owner (issue #24) ────

    [Fact]
    public async Task GetFoodById_OwnCustomFood_Returns200()
    {
        var ownerToken = TokenFor("foods_byid_own_custom");

        var customId = await CreateCustomFood(ownerToken, "Owner's Protein Shake");

        var response = await Get(ownerToken, string.Format(ByIdRoute, customId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(customId, body.GetProperty("id").GetGuid());
        Assert.Equal("Custom", body.GetProperty("source").GetString());
    }

    [Fact]
    public async Task GetFoodById_OtherUsersCustomFood_Returns404()
    {
        var ownerToken = TokenFor("foods_byid_xuser_owner");
        var attackerToken = TokenFor("foods_byid_xuser_attacker");

        var customId = await CreateCustomFood(ownerToken, "Private Custom Food");

        // The attacker knows the id but must not be able to read another user's custom food:
        // it is indistinguishable from a missing food (404), never leaked.
        var response = await Get(attackerToken, string.Format(ByIdRoute, customId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    /// <summary>
    /// Creates a custom food for the given user via <c>POST /api/v1/me/foods</c> and returns its id.
    /// </summary>
    private async Task<Guid> CreateCustomFood(string token, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/foods")
        {
            Content = JsonContent.Create(CustomFoodBody(name)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
