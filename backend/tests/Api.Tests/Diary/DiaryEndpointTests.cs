using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Diary;

/// <summary>
/// Integration tests for the food diary endpoints (issue #22):
/// <c>POST /api/v1/me/diary</c>, <c>GET /api/v1/me/diary?date=</c>,
/// <c>PUT /api/v1/me/diary/{id}</c>, and <c>DELETE /api/v1/me/diary/{id}</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Foods are
/// seeded directly into the shared in-memory database via a service scope — the diary references
/// foods by id, exactly as the production barcode/search flow lands them in the cache. Each test
/// uses a unique <c>sub</c> claim so provisioned users never collide on the shared database.
/// </remarks>
public sealed class DiaryEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string DiaryRoute = "/api/v1/me/diary";

    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DiaryEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard: every diary route requires a bearer token ────────────────────

    [Fact]
    public async Task AddEntry_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DiaryRoute)
        {
            Content = JsonContent.Create(new { foodItemId = Guid.NewGuid() }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListDay_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync($"{DiaryRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EditEntry_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{DiaryRoute}/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new { quantity = 1 }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEntry_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"{DiaryRoute}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST happy path: entry saved + returned with computed nutrition ──────────

    [Fact]
    public async Task AddEntry_ValidRequest_Returns201WithComputedNutrition()
    {
        var token = TokenFor("diary_add_valid");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();

        // 2 cups (245 g each) = 490 g. Yogurt is 59 kcal / 10 g protein per 100 g.
        var response = await Post(token, new
        {
            foodItemId = foodId,
            servingSizeId = cupServingId,
            quantity = 2,
            mealType = "Breakfast",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(foodId, body.GetProperty("foodItemId").GetGuid());
        Assert.Equal("Greek Yogurt", body.GetProperty("foodName").GetString());
        Assert.Equal("Breakfast", body.GetProperty("mealType").GetString());
        Assert.Equal("2026-06-24", body.GetProperty("date").GetString());

        var nutrition = body.GetProperty("nutrition");
        // 59 kcal/100g * 490g = 289.1 kcal; 10 g/100g * 490g = 49 g protein.
        Assert.Equal(289.1m, nutrition.GetProperty("energyKcal").GetDecimal());
        Assert.Equal(49.0m, nutrition.GetProperty("proteinG").GetDecimal());

        // Unused for assertions but proves the gram serving was seeded distinctly.
        Assert.NotEqual(cupServingId, gramServingId);
    }

    [Fact]
    public async Task AddEntry_FractionalQuantity_ScalesNutrition()
    {
        var token = TokenFor("diary_add_fractional");
        var (foodId, gramServingId, _) = SeedGreekYogurt();

        // 50 g of the 100 g canonical serving: quantity 0.5 * 100 g = 50 g.
        var response = await Post(token, new
        {
            foodItemId = foodId,
            servingSizeId = gramServingId,
            quantity = 0.5,
            mealType = "Snack",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var nutrition = body.GetProperty("nutrition");
        // 59 kcal/100g * 50g = 29.5 kcal.
        Assert.Equal(29.5m, nutrition.GetProperty("energyKcal").GetDecimal());
    }

    // ── POST validation / FK errors ──────────────────────────────────────────────

    [Fact]
    public async Task AddEntry_QuantityZero_Returns400WithFieldError()
    {
        var token = TokenFor("diary_add_qty_zero");
        var (foodId, gramServingId, _) = SeedGreekYogurt();

        var response = await Post(token, new
        {
            foodItemId = foodId,
            servingSizeId = gramServingId,
            quantity = 0,
            mealType = "Lunch",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("quantity", out _));
    }

    [Fact]
    public async Task AddEntry_InvalidMealType_Returns400WithFieldError()
    {
        var token = TokenFor("diary_add_bad_meal");
        var (foodId, gramServingId, _) = SeedGreekYogurt();

        var response = await Post(token, new
        {
            foodItemId = foodId,
            servingSizeId = gramServingId,
            quantity = 1,
            mealType = "Brunch",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("mealType", out _));
    }

    [Fact]
    public async Task AddEntry_InvalidDate_Returns400WithFieldError()
    {
        var token = TokenFor("diary_add_bad_date");
        var (foodId, gramServingId, _) = SeedGreekYogurt();

        var response = await Post(token, new
        {
            foodItemId = foodId,
            servingSizeId = gramServingId,
            quantity = 1,
            mealType = "Dinner",
            date = "24-06-2026",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    [Fact]
    public async Task AddEntry_UnknownFood_Returns404()
    {
        var token = TokenFor("diary_add_unknown_food");

        var response = await Post(token, new
        {
            foodItemId = Guid.NewGuid(),
            servingSizeId = Guid.NewGuid(),
            quantity = 1,
            mealType = "Lunch",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddEntry_ServingNotBelongingToFood_Returns404()
    {
        var token = TokenFor("diary_add_wrong_serving");
        var (foodId, _, _) = SeedGreekYogurt();

        var response = await Post(token, new
        {
            foodItemId = foodId,
            servingSizeId = Guid.NewGuid(), // not a serving of this food
            quantity = 1,
            mealType = "Lunch",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET list-by-date grouped by meal ─────────────────────────────────────────

    [Fact]
    public async Task ListDay_MissingDateParam_Returns400()
    {
        var token = TokenFor("diary_list_no_date");

        var response = await Get(token, DiaryRoute);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("date", out _));
    }

    [Fact]
    public async Task ListDay_ReturnsEntriesGroupedByMeal()
    {
        var token = TokenFor("diary_list_grouped");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();

        await Post(token, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24"));
        await Post(token, NewEntry(foodId, gramServingId, 2, "Breakfast", "2026-06-24"));
        await Post(token, NewEntry(foodId, cupServingId, 1, "Dinner", "2026-06-24"));
        // A different day must NOT appear in this day's listing.
        await Post(token, NewEntry(foodId, cupServingId, 1, "Lunch", "2026-06-25"));

        var response = await Get(token, $"{DiaryRoute}?date=2026-06-24");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-24", body.GetProperty("date").GetString());

        var meals = body.GetProperty("meals");
        Assert.Equal(2, meals.GetArrayLength()); // Breakfast + Dinner only (no Lunch on this day)

        // Canonical meal order: Breakfast before Dinner.
        Assert.Equal("Breakfast", meals[0].GetProperty("mealType").GetString());
        Assert.Equal(2, meals[0].GetProperty("entries").GetArrayLength());
        Assert.Equal("Dinner", meals[1].GetProperty("mealType").GetString());
        Assert.Equal(1, meals[1].GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task ListDay_EmptyDay_ReturnsEmptyMeals()
    {
        var token = TokenFor("diary_list_empty");

        var response = await Get(token, $"{DiaryRoute}?date=2026-01-01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("meals").GetArrayLength());
    }

    // ── PUT edit (incl. copy-to-another-day via date change) ─────────────────────

    [Fact]
    public async Task EditEntry_ChangesQuantityMealAndDate_Returns200WithRecomputedNutrition()
    {
        var token = TokenFor("diary_edit");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();

        var created = await Post(token, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Move to a different day + meal, change to 100 g serving x 3 = 300 g.
        var response = await Put(token, $"{DiaryRoute}/{id}", new
        {
            servingSizeId = gramServingId,
            quantity = 3,
            mealType = "Dinner",
            date = "2026-06-30",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Dinner", body.GetProperty("mealType").GetString());
        Assert.Equal("2026-06-30", body.GetProperty("date").GetString());
        // 59 kcal/100g * 300g = 177 kcal.
        Assert.Equal(177m, body.GetProperty("nutrition").GetProperty("energyKcal").GetDecimal());

        // Original day no longer has the entry; new day does.
        var oldDay = await (await Get(token, $"{DiaryRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, oldDay.GetProperty("meals").GetArrayLength());
        var newDay = await (await Get(token, $"{DiaryRoute}?date=2026-06-30")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, newDay.GetProperty("meals").GetArrayLength());
    }

    [Fact]
    public async Task EditEntry_UnknownId_Returns404()
    {
        var token = TokenFor("diary_edit_unknown");

        var response = await Put(token, $"{DiaryRoute}/{Guid.NewGuid()}", new
        {
            servingSizeId = Guid.NewGuid(),
            quantity = 1,
            mealType = "Lunch",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditEntry_QuantityZero_Returns400()
    {
        var token = TokenFor("diary_edit_qty_zero");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();

        var created = await Post(token, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await Put(token, $"{DiaryRoute}/{id}", new
        {
            servingSizeId = gramServingId,
            quantity = 0,
            mealType = "Breakfast",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── DELETE ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEntry_Existing_Returns204ThenGone()
    {
        var token = TokenFor("diary_delete");
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        var created = await Post(token, NewEntry(foodId, cupServingId, 1, "Lunch", "2026-06-24"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await Delete(token, $"{DiaryRoute}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var day = await (await Get(token, $"{DiaryRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, day.GetProperty("meals").GetArrayLength());
    }

    [Fact]
    public async Task DeleteEntry_UnknownId_Returns404()
    {
        var token = TokenFor("diary_delete_unknown");

        var response = await Delete(token, $"{DiaryRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Cross-user isolation: another user's entry is invisible (404) ────────────

    [Fact]
    public async Task EditEntry_OtherUsersEntry_Returns404()
    {
        var ownerToken = TokenFor("diary_xuser_owner_edit");
        var attackerToken = TokenFor("diary_xuser_attacker_edit");
        var (foodId, gramServingId, cupServingId) = SeedGreekYogurt();

        var created = await Post(ownerToken, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-06-24"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await Put(attackerToken, $"{DiaryRoute}/{id}", new
        {
            servingSizeId = gramServingId,
            quantity = 5,
            mealType = "Dinner",
            date = "2026-06-24",
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEntry_OtherUsersEntry_Returns404AndEntrySurvives()
    {
        var ownerToken = TokenFor("diary_xuser_owner_del");
        var attackerToken = TokenFor("diary_xuser_attacker_del");
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        var created = await Post(ownerToken, NewEntry(foodId, cupServingId, 1, "Lunch", "2026-06-24"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var attackerResponse = await Delete(attackerToken, $"{DiaryRoute}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, attackerResponse.StatusCode);

        // The owner can still see their entry — it was not deleted.
        var ownerDay = await (await Get(ownerToken, $"{DiaryRoute}?date=2026-06-24")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, ownerDay.GetProperty("meals").GetArrayLength());
    }

    [Fact]
    public async Task ListDay_OnlyReturnsCurrentUsersEntries()
    {
        var userAToken = TokenFor("diary_xuser_list_a");
        var userBToken = TokenFor("diary_xuser_list_b");
        var (foodId, _, cupServingId) = SeedGreekYogurt();

        await Post(userAToken, NewEntry(foodId, cupServingId, 1, "Breakfast", "2026-07-01"));
        await Post(userBToken, NewEntry(foodId, cupServingId, 1, "Dinner", "2026-07-01"));

        var aDay = await (await Get(userAToken, $"{DiaryRoute}?date=2026-07-01")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, aDay.GetProperty("meals").GetArrayLength());
        Assert.Equal("Breakfast", aDay.GetProperty("meals")[0].GetProperty("mealType").GetString());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

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

    private async Task<HttpResponseMessage> Post(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DiaryRoute)
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
