using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Registers the per-user custom-food, favorites, and recents endpoints on the supplied versioned
/// route builder (issue #24).
/// <list type="bullet">
///   <item><description><c>POST   /me/foods</c>              — create a custom food owned by the caller.</description></item>
///   <item><description><c>GET    /me/foods</c>              — list the caller's custom foods.</description></item>
///   <item><description><c>PUT    /me/foods/{id}</c>         — edit one of the caller's custom foods.</description></item>
///   <item><description><c>DELETE /me/foods/{id}</c>         — delete one of the caller's custom foods.</description></item>
///   <item><description><c>PUT    /me/foods/{id}/favorite</c>   — favorite a visible food (idempotent).</description></item>
///   <item><description><c>DELETE /me/foods/{id}/favorite</c>   — unfavorite a food (idempotent).</description></item>
///   <item><description><c>GET    /me/foods/favorites</c>    — list the caller's favorited foods.</description></item>
///   <item><description><c>GET    /me/foods/recents</c>      — list the caller's most-recently-logged foods.</description></item>
/// </list>
/// All endpoints require authorization and scope every query to the current user's id. Accessing or
/// editing another user's custom food returns 404 (not 403) so existence is never leaked across
/// users; shared Open Food Facts foods have a null owner and so are never editable/deletable here.
/// </summary>
internal static class MyFoodsEndpoints
{
    // Default and bounds for the recents list size. Out-of-range values are clamped (not rejected)
    // to keep the optional query parameter forgiving, per the "default in code" binding guidance.
    private const int DefaultRecentsLimit = 10;
    private const int MinRecentsLimit = 1;
    private const int MaxRecentsLimit = 50;

    /// <summary>
    /// Maps the custom-food, favorites, and recents routes onto a <c>/me/foods</c> group and
    /// requires authorization on all of them (issue #24).
    /// </summary>
    internal static RouteGroupBuilder MapMyFoodsEndpoints(this RouteGroupBuilder group)
    {
        var myFoods = group.MapGroup("/me/foods").RequireAuthorization();

        myFoods.MapPost("/", CreateCustomFoodAsync)
            .WithName("CreateCustomFood");

        myFoods.MapGet("/", ListMyFoodsAsync)
            .WithName("ListMyFoods");

        myFoods.MapPut("/{id:guid}", UpdateCustomFoodAsync)
            .WithName("UpdateCustomFood");

        myFoods.MapDelete("/{id:guid}", DeleteCustomFoodAsync)
            .WithName("DeleteCustomFood");

        myFoods.MapPut("/{id:guid}/favorite", FavoriteFoodAsync)
            .WithName("FavoriteFood");

        myFoods.MapDelete("/{id:guid}/favorite", UnfavoriteFoodAsync)
            .WithName("UnfavoriteFood");

        myFoods.MapGet("/favorites", ListFavoritesAsync)
            .WithName("ListFavoriteFoods");

        myFoods.MapGet("/recents", ListRecentsAsync)
            .WithName("ListRecentFoods");

        return group;
    }

    // ── POST /api/v1/me/foods ─────────────────────────────────────────────────────

    private static async Task<IResult> CreateCustomFoodAsync(
        CreateCustomFoodRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = CustomFoodValidator.ValidateCreate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var nutrition = CustomFoodValidator.ToNutritionFacts(request.Nutrition);
        var food = FoodItem.CreateCustom(user.Id, request.Name.Trim(), nutrition, NormalizeBrand(request.Brand));

        if (request.Servings is { Count: > 0 })
        {
            food.ReplaceCustomServings(CustomFoodValidator.ToServingTuples(request.Servings));
        }

        db.FoodItems.Add(food);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/foods/{food.Id}", FoodMapper.ToResponse(food));
    }

    // ── GET /api/v1/me/foods ──────────────────────────────────────────────────────

    private static async Task<IResult> ListMyFoodsAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var foods = await db.FoodItems
            .AsNoTracking()
            .Include(f => f.ServingSizes)
            .Where(f => f.CreatedByUserId == user.Id)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(ToFoodList(foods));
    }

    // ── PUT /api/v1/me/foods/{id} ─────────────────────────────────────────────────

    private static async Task<IResult> UpdateCustomFoodAsync(
        Guid id,
        UpdateCustomFoodRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = CustomFoodValidator.ValidateUpdate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Scope to the caller's own custom foods. OFF foods have a null owner and so never match,
        // and another user's custom food is indistinguishable from a missing one (both 404).
        var food = await db.FoodItems
            .Include(f => f.ServingSizes)
            .FirstOrDefaultAsync(f => f.Id == id && f.CreatedByUserId == user.Id, ct);

        if (food is null)
        {
            return NotFound("Food not found.", $"No custom food with id '{id}' exists for this user.");
        }

        var nutrition = CustomFoodValidator.ToNutritionFacts(request.Nutrition);
        food.UpdateCustomDetails(request.Name.Trim(), NormalizeBrand(request.Brand), nutrition);

        // Replace servings only when the client supplies a set; omitting leaves servings as-is.
        if (request.Servings is { Count: > 0 })
        {
            food.ReplaceCustomServings(CustomFoodValidator.ToServingTuples(request.Servings));
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(FoodMapper.ToResponse(food));
    }

    // ── DELETE /api/v1/me/foods/{id} ──────────────────────────────────────────────

    private static async Task<IResult> DeleteCustomFoodAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var food = await db.FoodItems
            .FirstOrDefaultAsync(f => f.Id == id && f.CreatedByUserId == user.Id, ct);

        if (food is null)
        {
            return NotFound("Food not found.", $"No custom food with id '{id}' exists for this user.");
        }

        // Diary entries reference foods under a Restrict FK, so a referenced custom food cannot be
        // hard-deleted without violating that constraint. Surface it as a 409 rather than a 500.
        var inUse = await db.DiaryEntries.AnyAsync(e => e.FoodItemId == id, ct);
        if (inUse)
        {
            return Results.Problem(
                title: "Food is in use.",
                detail: "This custom food is referenced by one or more diary entries and cannot be deleted.",
                statusCode: StatusCodes.Status409Conflict);
        }

        // ServingSizes cascade-delete from FoodItems (FK_ServingSizes_FoodItems_FoodItemId, Cascade)
        // and UserFavoriteFoods cascade-delete too, so removing the food row is sufficient.
        db.FoodItems.Remove(food);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ── PUT /api/v1/me/foods/{id}/favorite ────────────────────────────────────────

    private static async Task<IResult> FavoriteFoodAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // The food must be visible to the user: a shared OFF food (null owner) or the user's own
        // custom food. Don't leak the existence of other users' custom foods (404 otherwise).
        var visible = await db.FoodItems
            .AnyAsync(f => f.Id == id && (f.CreatedByUserId == null || f.CreatedByUserId == user.Id), ct);

        if (!visible)
        {
            return NotFound("Food not found.", $"No food with id '{id}' is available to favorite.");
        }

        // Fast path: favoriting an already-favorited food is a no-op success and avoids a throw in
        // the common case.
        var alreadyFavorited = await db.UserFavoriteFoods
            .AnyAsync(f => f.UserId == user.Id && f.FoodItemId == id, ct);

        if (alreadyFavorited)
        {
            return Results.NoContent();
        }

        // The fast-path check + insert is not atomic: two concurrent requests can both pass the
        // AnyAsync check then both insert, tripping the unique (UserId, FoodItemId) index. Catch the
        // resulting DbUpdateException and return 204 so the operation stays idempotent/race-safe.
        try
        {
            db.UserFavoriteFoods.Add(UserFavoriteFood.Create(user.Id, id));
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) // concurrent insert hit the unique (UserId, FoodItemId) index — already favorited
        {
            // Idempotent: another request created the same favorite between our check and insert.
            // Catch the broad DbUpdateException (rather than a provider-specific code like Npgsql's
            // 23505) to stay provider-agnostic — works for both Npgsql and the SQLite test harness.
            // The failed Add stays tracked, but this handler returns immediately and the request-
            // scoped DbContext is disposed at end of request, so the context is never reused.
            return Results.NoContent();
        }

        return Results.NoContent();
    }

    // ── DELETE /api/v1/me/foods/{id}/favorite ─────────────────────────────────────

    private static async Task<IResult> UnfavoriteFoodAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var favorite = await db.UserFavoriteFoods
            .FirstOrDefaultAsync(f => f.UserId == user.Id && f.FoodItemId == id, ct);

        // Idempotent: removing a non-existent favorite still succeeds with 204.
        if (favorite is not null)
        {
            db.UserFavoriteFoods.Remove(favorite);
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    // ── GET /api/v1/me/foods/favorites ────────────────────────────────────────────

    private static async Task<IResult> ListFavoritesAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // EF cannot eager-load (Include) through a join, so read the favorited food ids in
        // most-recent-first order, load those foods with their servings, then re-order in memory
        // to preserve the favorite ordering.
        var favoriteFoodIds = await db.UserFavoriteFoods
            .AsNoTracking()
            .Where(f => f.UserId == user.Id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.FoodItemId)
            .ToListAsync(ct);

        var foods = await LoadFoodsByIdsAsync(db, favoriteFoodIds, ct);
        var ordered = OrderByIds(favoriteFoodIds, foods);

        return Results.Ok(ToFoodList(ordered));
    }

    // ── GET /api/v1/me/foods/recents?limit= ───────────────────────────────────────

    private static async Task<IResult> ListRecentsAsync(
        int? limit,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        // limit is bound as int? to avoid the minimal-API "required value-type query param" gotcha.
        // Clamp silently into [1, 50] (default 10) rather than 400 — a forgiving optional parameter.
        var resolvedLimit = Math.Clamp(limit ?? DefaultRecentsLimit, MinRecentsLimit, MaxRecentsLimit);

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Derive recents from the diary: the distinct foods the user logged, most-recent-first.
        // Trade-off: we pull the user's logged food ids ordered by CreatedAt and distinct in memory
        // rather than a provider-specific GROUP BY ... MAX, so this translates identically on both
        // Npgsql and the SQLite test harness. Acceptable for v1 volumes.
        var loggedFoodIds = await db.DiaryEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.FoodItemId)
            .ToListAsync(ct);

        var recentFoodIds = loggedFoodIds
            .Distinct()
            .Take(resolvedLimit)
            .ToList();

        var foods = await LoadFoodsByIdsAsync(db, recentFoodIds, ct);
        var ordered = OrderByIds(recentFoodIds, foods);

        return Results.Ok(ToFoodList(ordered));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static IResult NotFound(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    private static string? NormalizeBrand(string? brand) =>
        string.IsNullOrWhiteSpace(brand) ? null : brand.Trim();

    private static async Task<List<FoodItem>> LoadFoodsByIdsAsync(
        AppDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.FoodItems
            .AsNoTracking()
            .Include(f => f.ServingSizes)
            .Where(f => ids.Contains(f.Id))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Re-orders the loaded foods to match the supplied id order, dropping any id without a loaded
    /// food (defensive — should not happen under the cascade/restrict FK rules).
    /// </summary>
    private static List<FoodItem> OrderByIds(IReadOnlyList<Guid> orderedIds, List<FoodItem> foods)
    {
        var byId = foods.ToDictionary(f => f.Id);
        var ordered = new List<FoodItem>(orderedIds.Count);
        foreach (var id in orderedIds)
        {
            if (byId.TryGetValue(id, out var food))
            {
                ordered.Add(food);
            }
        }

        return ordered;
    }

    private static FoodListResponse ToFoodList(IReadOnlyList<FoodItem> foods)
    {
        var items = foods.Select(FoodMapper.ToResponse).ToList();
        return new FoodListResponse(items.Count, items);
    }
}
