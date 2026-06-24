using MAIHealthCoach.Application.Food;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Registers the food search &amp; detail endpoints on the supplied versioned route builder (issue #21).
/// <list type="bullet">
///   <item><description><c>GET /foods</c> — text search over Open Food Facts via <see cref="INutritionLookupService"/>.</description></item>
///   <item><description><c>GET /foods/{id}</c> — fetch a shared food (OFF cache row) or the caller's own custom food by id; 404 otherwise (another user's custom food is never surfaced).</description></item>
///   <item><description><c>GET /foods/barcode/{code}</c> — cache-first barcode lookup via <see cref="INutritionLookupService"/>.</description></item>
/// </list>
/// All endpoints require authorization. Open Food Facts is never called directly — every lookup goes
/// through <see cref="INutritionLookupService"/>, and the service's server-side <c>ErrorDetail</c> is
/// never surfaced to clients.
/// </summary>
internal static class FoodsEndpoints
{
    // Mirrors the OFF service's default page size; used to echo PageSize when the client supplies none.
    private const int DefaultPageSize = 20;

    private const string ServiceUnavailableTitle = "Food search is temporarily unavailable.";
    private const string ServiceUnavailableDetail =
        "The upstream food database could not be reached. Please try again shortly.";

    /// <summary>
    /// Maps <c>GET /foods</c>, <c>GET /foods/{id}</c>, and <c>GET /foods/barcode/{code}</c> onto
    /// <paramref name="group"/> and requires authorization on all of them (issue #21).
    /// </summary>
    internal static RouteGroupBuilder MapFoodsEndpoints(this RouteGroupBuilder group)
    {
        var foods = group.MapGroup("/foods").RequireAuthorization();

        foods.MapGet("/", SearchFoodsAsync)
            .WithName("SearchFoods");

        foods.MapGet("/{id:guid}", GetFoodByIdAsync)
            .WithName("GetFoodById");

        foods.MapGet("/barcode/{code}", GetFoodByBarcodeAsync)
            .WithName("GetFoodByBarcode");

        return group;
    }

    // ── GET /api/v1/foods ─────────────────────────────────────────────────────────

    private static async Task<IResult> SearchFoodsAsync(
        string? q,
        int? page,
        int? pageSize,
        INutritionLookupService lookup,
        CancellationToken ct)
    {
        // page is optional: a non-nullable `int` query parameter is treated as REQUIRED by minimal
        // APIs (omitting it fails binding), so bind it as int? and default the omitted case to the
        // first page. An explicitly-supplied page < 1 still fails validation below.
        var resolvedPage = page ?? 1;

        var errors = FoodSearchValidator.Validate(q, resolvedPage, pageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // q is non-null/non-blank here (validator guarantees it).
        var query = q!.Trim();

        var result = await lookup.SearchAsync(query, resolvedPage, ct);

        if (result.Status == NutritionLookupStatus.ServiceUnavailable)
        {
            return Results.Problem(
                title: ServiceUnavailableTitle,
                detail: ServiceUnavailableDetail,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var effectivePageSize = pageSize ?? DefaultPageSize;

        var items = result.Matches
            .OrderBy(m => m.Rank)
            .Take(effectivePageSize)
            .Select(m => new FoodSearchItem(m.Rank, FoodMapper.ToResponse(m.Food)))
            .ToList();

        var response = new FoodSearchResponse(
            Query: query,
            Page: result.Page,
            PageSize: effectivePageSize,
            Count: items.Count,
            Items: items);

        return Results.Ok(response);
    }

    // ── GET /api/v1/foods/{id} ────────────────────────────────────────────────────

    /// <summary>
    /// Fetches a persisted food by id, scoped to what the caller may see: a shared OFF food
    /// (null owner) or the caller's own custom food. Another user's custom food yields 404
    /// (indistinguishable from missing) so private custom foods are never leaked across users.
    /// </summary>
    private static async Task<IResult> GetFoodByIdAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Visibility filter: shared OFF foods (null owner) or the caller's own custom food. Another
        // user's custom food never matches, so it returns 404 rather than leaking its existence.
        var food = await db.FoodItems
            .AsNoTracking()
            .Include(f => f.ServingSizes)
            .FirstOrDefaultAsync(f => f.Id == id && (f.CreatedByUserId == null || f.CreatedByUserId == user.Id), ct);

        if (food is null)
        {
            return Results.Problem(
                title: "Food not found.",
                detail: $"No food with id '{id}' exists.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(FoodMapper.ToResponse(food));
    }

    // ── GET /api/v1/foods/barcode/{code} ──────────────────────────────────────────

    private static async Task<IResult> GetFoodByBarcodeAsync(
        string code,
        INutritionLookupService lookup,
        CancellationToken ct)
    {
        // A blank barcode has no upstream match; treat it as not-found to mirror the service's
        // blank-input semantics rather than calling the service with whitespace.
        if (string.IsNullOrWhiteSpace(code))
        {
            return BarcodeNotFound(code);
        }

        var result = await lookup.LookupByBarcodeAsync(code, ct);

        return result.Status switch
        {
            NutritionLookupStatus.Found => Results.Ok(FoodMapper.ToResponse(result.Food!)),
            NutritionLookupStatus.NotFound => BarcodeNotFound(code),
            _ => Results.Problem(
                title: ServiceUnavailableTitle,
                detail: ServiceUnavailableDetail,
                statusCode: StatusCodes.Status503ServiceUnavailable),
        };
    }

    private static IResult BarcodeNotFound(string code) =>
        Results.Problem(
            title: "Food not found.",
            detail: $"No food found for barcode '{code}'.",
            statusCode: StatusCodes.Status404NotFound);
}
