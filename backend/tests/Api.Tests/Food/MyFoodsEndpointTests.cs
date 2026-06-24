using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// Integration tests for the per-user custom-food, favorites, and recents endpoints (issue #24):
/// <c>POST/GET/PUT/DELETE /api/v1/me/foods</c>, <c>PUT/DELETE /api/v1/me/foods/{id}/favorite</c>,
/// <c>GET /api/v1/me/foods/favorites</c>, and <c>GET /api/v1/me/foods/recents</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Shared Open
/// Food Facts foods are seeded directly into the in-memory database via a service scope; custom
/// foods are created through the public endpoint. Each test uses a unique <c>sub</c> claim so
/// provisioned users never collide on the shared database, which keeps ownership-isolation and
/// recents-isolation assertions meaningful.
/// </remarks>
public sealed class MyFoodsEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string FoodsRoute = "/api/v1/me/foods";
    private const string DiaryRoute = "/api/v1/me/diary";

    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MyFoodsEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard: every route requires a bearer token ──────────────────────────

    [Fact]
    public async Task CreateFood_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, FoodsRoute)
        {
            Content = JsonContent.Create(NewFoodBody("Oats")),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListFoods_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(FoodsRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFood_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{FoodsRoute}/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(NewFoodBody("Oats")),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFood_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"{FoodsRoute}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FavoriteFood_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{FoodsRoute}/{Guid.NewGuid()}/favorite");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnfavoriteFood_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"{FoodsRoute}/{Guid.NewGuid()}/favorite");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListFavorites_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{FoodsRoute}/favorites");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListRecents_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{FoodsRoute}/recents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── CREATE custom food ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFood_ValidRequest_Returns201WithCustomFoodAndServings()
    {
        var token = TokenFor("foods_create_valid");

        var response = await Post(token, FoodsRoute, new
        {
            name = "Homemade Granola",
            brand = "Mum's Kitchen",
            nutrition = new
            {
                energyKcal = 450m,
                proteinG = 10m,
                carbohydrateG = 60m,
                fatG = 18m,
                sugarsG = 20m,
                fiberG = 7m,
                saturatedFatG = 3m,
                sodiumMg = 25m,
            },
            servings = new[]
            {
                new { label = "1 bowl", quantity = 1m, unit = "bowl", gramsEquivalent = 60m, isDefault = true },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Custom", body.GetProperty("source").GetString());
        Assert.Equal("Homemade Granola", body.GetProperty("name").GetString());
        Assert.Equal("Mum's Kitchen", body.GetProperty("brand").GetString());
        Assert.Equal(450m, body.GetProperty("nutritionPer100g").GetProperty("energyKcal").GetDecimal());

        var servings = body.GetProperty("servingSizes");
        // Canonical 100 g serving plus the provided "1 bowl" serving.
        Assert.True(HasServing(servings, "100 g", 100m));
        Assert.True(HasServing(servings, "1 bowl", 60m));

        // The provided serving is flagged default, so it must be the default portion.
        var bowl = FindServing(servings, "1 bowl");
        Assert.True(bowl.GetProperty("isDefault").GetBoolean());

        // GET /me/foods now lists the created food.
        var listResponse = await Get(token, FoodsRoute);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetProperty("count").GetInt32());
        Assert.Equal("Homemade Granola", list.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateFood_WithoutServings_HasCanonicalServingOnly()
    {
        var token = TokenFor("foods_create_no_servings");

        var response = await Post(token, FoodsRoute, NewFoodBody("Plain Water", energyKcal: 0m, proteinG: 0m, carbohydrateG: 0m, fatG: 0m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var servings = body.GetProperty("servingSizes");
        Assert.Equal(1, servings.GetArrayLength());
        Assert.True(HasServing(servings, "100 g", 100m));
    }

    // ── CREATE validation 400 (runs before domain → never 500) ───────────────────

    [Fact]
    public async Task CreateFood_BlankName_Returns400WithFieldError()
    {
        var token = TokenFor("foods_create_blank_name");

        var response = await Post(token, FoodsRoute, NewFoodBody("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task CreateFood_NegativeEnergy_Returns400WithFieldError()
    {
        var token = TokenFor("foods_create_negative_energy");

        var response = await Post(token, FoodsRoute, NewFoodBody("Bad Food", energyKcal: -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("nutrition.energyKcal", out _));
    }

    [Fact]
    public async Task CreateFood_ServingQuantityZero_Returns400WithFieldError()
    {
        var token = TokenFor("foods_create_serving_qty_zero");

        var response = await Post(token, FoodsRoute, new
        {
            name = "Granola",
            brand = (string?)null,
            nutrition = DefaultNutrition(),
            servings = new[]
            {
                new { label = "1 bowl", quantity = 0m, unit = "bowl", gramsEquivalent = 60m, isDefault = true },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("servings[0].quantity", out _));
    }

    [Fact]
    public async Task CreateFood_ServingGramsZero_Returns400WithFieldError()
    {
        var token = TokenFor("foods_create_serving_grams_zero");

        var response = await Post(token, FoodsRoute, new
        {
            name = "Granola",
            brand = (string?)null,
            nutrition = DefaultNutrition(),
            servings = new[]
            {
                new { label = "1 bowl", quantity = 1m, unit = "bowl", gramsEquivalent = 0m, isDefault = true },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("servings[0].gramsEquivalent", out _));
    }

    // ── OWNERSHIP ISOLATION ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListFoods_DoesNotIncludeOtherUsersCustomFood()
    {
        var userAToken = TokenFor("foods_iso_list_a");
        var userBToken = TokenFor("foods_iso_list_b");

        await Post(userAToken, FoodsRoute, NewFoodBody("A's Secret Recipe"));

        var bList = await (await Get(userBToken, FoodsRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, bList.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task UpdateFood_OtherUsersCustomFood_Returns404()
    {
        var ownerToken = TokenFor("foods_iso_edit_owner");
        var attackerToken = TokenFor("foods_iso_edit_attacker");

        var foodId = await CreateFoodReturningId(ownerToken, "Owner's Food");

        var response = await Put(attackerToken, $"{FoodsRoute}/{foodId}", NewFoodBody("Hijacked"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFood_OtherUsersCustomFood_Returns404AndFoodSurvives()
    {
        var ownerToken = TokenFor("foods_iso_delete_owner");
        var attackerToken = TokenFor("foods_iso_delete_attacker");

        var foodId = await CreateFoodReturningId(ownerToken, "Owner's Food");

        var response = await Delete(attackerToken, $"{FoodsRoute}/{foodId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The owner can still see it — the attacker's delete was a no-op.
        var ownerList = await (await Get(ownerToken, FoodsRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, ownerList.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task UpdateFood_SharedOffFood_Returns404()
    {
        var token = TokenFor("foods_iso_edit_off");
        var (offFoodId, _) = SeedOffFood("Shared Apple");

        // An OFF food has a null owner, so it is never an editable custom food (404, not 403).
        var response = await Put(token, $"{FoodsRoute}/{offFoodId}", NewFoodBody("Tampered Apple"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteFood_SharedOffFood_Returns404()
    {
        var token = TokenFor("foods_iso_delete_off");
        var (offFoodId, _) = SeedOffFood("Shared Apple");

        var response = await Delete(token, $"{FoodsRoute}/{offFoodId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── EDIT ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateFood_Owner_UpdatesNameBrandNutritionAndServings()
    {
        var token = TokenFor("foods_edit_owner");
        var foodId = await CreateFoodReturningId(token, "Original Name");

        var response = await Put(token, $"{FoodsRoute}/{foodId}", new
        {
            name = "Updated Name",
            brand = "New Brand",
            nutrition = new
            {
                energyKcal = 123m,
                proteinG = 9m,
                carbohydrateG = 11m,
                fatG = 4m,
                sugarsG = (decimal?)null,
                fiberG = (decimal?)null,
                saturatedFatG = (decimal?)null,
                sodiumMg = (decimal?)null,
            },
            servings = new[]
            {
                new { label = "1 slice", quantity = 1m, unit = "slice", gramsEquivalent = 30m, isDefault = true },
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", body.GetProperty("name").GetString());
        Assert.Equal("New Brand", body.GetProperty("brand").GetString());
        Assert.Equal(123m, body.GetProperty("nutritionPer100g").GetProperty("energyKcal").GetDecimal());
        Assert.True(HasServing(body.GetProperty("servingSizes"), "1 slice", 30m));

        // Changes are persisted: re-reading via the shared GET /api/v1/foods/{id} reflects them.
        var fetched = await (await Get(token, $"/api/v1/foods/{foodId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Name", fetched.GetProperty("name").GetString());
        Assert.Equal(123m, fetched.GetProperty("nutritionPer100g").GetProperty("energyKcal").GetDecimal());
    }

    [Fact]
    public async Task UpdateFood_UnknownId_Returns404()
    {
        var token = TokenFor("foods_edit_unknown");

        var response = await Put(token, $"{FoodsRoute}/{Guid.NewGuid()}", NewFoodBody("Ghost"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteFood_OwnFoodNotReferenced_Returns204ThenGone()
    {
        var token = TokenFor("foods_delete_unreferenced");
        var foodId = await CreateFoodReturningId(token, "Disposable Food");

        var response = await Delete(token, $"{FoodsRoute}/{foodId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await (await Get(token, FoodsRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, list.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task DeleteFood_ReferencedByDiaryEntry_Returns409()
    {
        var token = TokenFor("foods_delete_referenced");

        // Create a custom food with a default serving, then log it in the diary.
        var created = await Post(token, FoodsRoute, new
        {
            name = "Logged Food",
            brand = (string?)null,
            nutrition = DefaultNutrition(),
            servings = new[]
            {
                new { label = "1 portion", quantity = 1m, unit = "portion", gramsEquivalent = 50m, isDefault = true },
            },
        });
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var foodId = body.GetProperty("id").GetGuid();
        var servingId = FindServing(body.GetProperty("servingSizes"), "1 portion").GetProperty("id").GetGuid();

        var logResponse = await Post(token, DiaryRoute, new
        {
            foodItemId = foodId,
            servingSizeId = servingId,
            quantity = 1,
            mealType = "Lunch",
            date = "2026-06-24",
        });
        Assert.Equal(HttpStatusCode.Created, logResponse.StatusCode);

        // The food is now referenced by a diary entry → deletion is a 409, not a 500.
        var deleteResponse = await Delete(token, $"{FoodsRoute}/{foodId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        // The food still exists.
        var list = await (await Get(token, FoodsRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetProperty("count").GetInt32());
    }

    // ── FAVORITES ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Favorite_VisibleOffFood_IsIdempotentAndListsThenUnfavorites()
    {
        var token = TokenFor("foods_fav_off");
        var (offFoodId, _) = SeedOffFood("Bananas");

        // Favorite (201/204) then list includes it.
        var fav1 = await Put(token, $"{FoodsRoute}/{offFoodId}/favorite", null);
        Assert.Equal(HttpStatusCode.NoContent, fav1.StatusCode);

        var favorites = await (await Get(token, $"{FoodsRoute}/favorites")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, favorites.GetProperty("count").GetInt32());
        Assert.Equal(offFoodId, favorites.GetProperty("items")[0].GetProperty("id").GetGuid());

        // Favorite again is idempotent: still 204, no duplicate row.
        var fav2 = await Put(token, $"{FoodsRoute}/{offFoodId}/favorite", null);
        Assert.Equal(HttpStatusCode.NoContent, fav2.StatusCode);
        var favoritesAgain = await (await Get(token, $"{FoodsRoute}/favorites")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, favoritesAgain.GetProperty("count").GetInt32());

        // Unfavorite removes it from the list.
        var unfav1 = await Delete(token, $"{FoodsRoute}/{offFoodId}/favorite");
        Assert.Equal(HttpStatusCode.NoContent, unfav1.StatusCode);
        var afterUnfav = await (await Get(token, $"{FoodsRoute}/favorites")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, afterUnfav.GetProperty("count").GetInt32());

        // Unfavorite again is idempotent: still 204.
        var unfav2 = await Delete(token, $"{FoodsRoute}/{offFoodId}/favorite");
        Assert.Equal(HttpStatusCode.NoContent, unfav2.StatusCode);
    }

    [Fact]
    public async Task Favorite_OwnCustomFood_Works()
    {
        var token = TokenFor("foods_fav_own_custom");
        var foodId = await CreateFoodReturningId(token, "My Custom Food");

        var response = await Put(token, $"{FoodsRoute}/{foodId}/favorite", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var favorites = await (await Get(token, $"{FoodsRoute}/favorites")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, favorites.GetProperty("count").GetInt32());
        Assert.Equal(foodId, favorites.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Favorite_OtherUsersCustomFood_Returns404()
    {
        var ownerToken = TokenFor("foods_fav_xuser_owner");
        var attackerToken = TokenFor("foods_fav_xuser_attacker");

        var foodId = await CreateFoodReturningId(ownerToken, "Owner Only");

        var response = await Put(attackerToken, $"{FoodsRoute}/{foodId}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Favorite_NonExistentFood_Returns404()
    {
        var token = TokenFor("foods_fav_missing");

        var response = await Put(token, $"{FoodsRoute}/{Guid.NewGuid()}/favorite", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── RECENTS ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recents_ReturnsDistinctFoodsMostRecentFirst()
    {
        var token = TokenFor("foods_recents_distinct");
        var (appleId, appleServing) = SeedOffFood("Apple");
        var (breadId, breadServing) = SeedOffFood("Bread");
        var (milkId, milkServing) = SeedOffFood("Milk");

        // Log apple, then bread, then apple again, then milk. Apple is logged twice.
        await LogFood(token, appleId, appleServing, "2026-06-24");
        await LogFood(token, breadId, breadServing, "2026-06-24");
        await LogFood(token, appleId, appleServing, "2026-06-24");
        await LogFood(token, milkId, milkServing, "2026-06-24");

        var recents = await (await Get(token, $"{FoodsRoute}/recents")).Content.ReadFromJsonAsync<JsonElement>();

        var ids = recents.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        // Distinct (apple appears once) and most-recent-first: milk, apple, bread.
        Assert.Equal(3, ids.Count);
        Assert.Equal(milkId, ids[0]);
        Assert.Equal(appleId, ids[1]);
        Assert.Equal(breadId, ids[2]);
    }

    [Fact]
    public async Task Recents_OnlyReturnsCurrentUsersLoggedFoods()
    {
        var userAToken = TokenFor("foods_recents_iso_a");
        var userBToken = TokenFor("foods_recents_iso_b");
        var (foodAId, foodAServing) = SeedOffFood("A Food");
        var (foodBId, foodBServing) = SeedOffFood("B Food");

        await LogFood(userAToken, foodAId, foodAServing, "2026-06-24");
        await LogFood(userBToken, foodBId, foodBServing, "2026-06-24");

        var aRecents = await (await Get(userAToken, $"{FoodsRoute}/recents")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, aRecents.GetProperty("count").GetInt32());
        Assert.Equal(foodAId, aRecents.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Recents_LimitQueryParam_CapsResults()
    {
        var token = TokenFor("foods_recents_limit");
        var (oatsId, oatsServing) = SeedOffFood("Oats");
        var (eggsId, eggsServing) = SeedOffFood("Eggs");

        await LogFood(token, oatsId, oatsServing, "2026-06-24");
        await LogFood(token, eggsId, eggsServing, "2026-06-24");

        var recents = await (await Get(token, $"{FoodsRoute}/recents?limit=1")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, recents.GetProperty("count").GetInt32());
        // Most-recently-logged food (eggs) is the single capped result.
        Assert.Equal(eggsId, recents.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Recents_OmittingLimit_Succeeds()
    {
        var token = TokenFor("foods_recents_no_limit");
        var (foodId, foodServing) = SeedOffFood("Yogurt");
        await LogFood(token, foodId, foodServing, "2026-06-24");

        // The optional int? query param must not 400/500 when omitted (the nullable-param gotcha).
        var response = await Get(token, $"{FoodsRoute}/recents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Recents_NoLoggedFoods_ReturnsEmptyList()
    {
        var token = TokenFor("foods_recents_empty");

        var response = await Get(token, $"{FoodsRoute}/recents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("count").GetInt32());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private static object DefaultNutrition() => new
    {
        energyKcal = 200m,
        proteinG = 5m,
        carbohydrateG = 25m,
        fatG = 8m,
        sugarsG = (decimal?)null,
        fiberG = (decimal?)null,
        saturatedFatG = (decimal?)null,
        sodiumMg = (decimal?)null,
    };

    private static object NewFoodBody(
        string name,
        decimal energyKcal = 200m,
        decimal proteinG = 5m,
        decimal carbohydrateG = 25m,
        decimal fatG = 8m) => new
        {
            name,
            brand = (string?)null,
            nutrition = new
            {
                energyKcal,
                proteinG,
                carbohydrateG,
                fatG,
                sugarsG = (decimal?)null,
                fiberG = (decimal?)null,
                saturatedFatG = (decimal?)null,
                sodiumMg = (decimal?)null,
            },
            servings = (object?)null,
        };

    private async Task<Guid> CreateFoodReturningId(string token, string name)
    {
        var response = await Post(token, FoodsRoute, NewFoodBody(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Seeds a shared Open Food Facts food (null owner) with its canonical 100 g serving marked
    /// default, returning (foodId, canonicalServingId) for diary logging.
    /// </summary>
    private (Guid FoodId, Guid ServingId) SeedOffFood(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nutrition = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m);

        var food = FoodItem.Create(name, FoodSource.OpenFoodFacts, nutrition);

        db.FoodItems.Add(food);
        db.SaveChanges();

        var serving = food.ServingSizes.Single(s => s.GramsEquivalent == 100m);
        return (food.Id, serving.Id);
    }

    private async Task LogFood(string token, Guid foodId, Guid servingId, string date)
    {
        var response = await Post(token, DiaryRoute, new
        {
            foodItemId = foodId,
            servingSizeId = servingId,
            quantity = 1,
            mealType = "Snack",
            date,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static bool HasServing(JsonElement servings, string label, decimal grams) =>
        servings.EnumerateArray().Any(s =>
            s.GetProperty("label").GetString() == label
            && s.GetProperty("gramsEquivalent").GetDecimal() == grams);

    private static JsonElement FindServing(JsonElement servings, string label) =>
        servings.EnumerateArray().First(s => s.GetProperty("label").GetString() == label);

    private async Task<HttpResponseMessage> Post(string token, string route, object body)
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

    private async Task<HttpResponseMessage> Put(string token, string route, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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
