using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Diary;

/// <summary>
/// Registers the food diary endpoints on the supplied versioned route builder (issue #22).
/// <list type="bullet">
///   <item><description><c>POST   /me/diary</c>      — add an entry; 201 with computed nutrition.</description></item>
///   <item><description><c>GET    /me/diary?date=</c> — list a day's entries grouped by meal.</description></item>
///   <item><description><c>PUT    /me/diary/{id}</c>  — edit serving/quantity/meal/date (copy-to-day).</description></item>
///   <item><description><c>DELETE /me/diary/{id}</c>  — remove an entry.</description></item>
/// </list>
/// All endpoints require authorization and scope every query to the current user's id. Accessing
/// another user's entry returns 404 (not 403) so entry existence is never leaked across users.
/// </summary>
internal static class DiaryEndpoints
{
    internal static RouteGroupBuilder MapDiaryEndpoints(this RouteGroupBuilder group)
    {
        var diary = group.MapGroup("/me/diary").RequireAuthorization();

        diary.MapPost("/", AddEntryAsync)
            .WithName("AddDiaryEntry");

        diary.MapGet("/", ListDayAsync)
            .WithName("ListDiaryDay");

        diary.MapPut("/{id:guid}", EditEntryAsync)
            .WithName("EditDiaryEntry");

        diary.MapDelete("/{id:guid}", DeleteEntryAsync)
            .WithName("DeleteDiaryEntry");

        return group;
    }

    // ── POST /api/v1/me/diary ─────────────────────────────────────────────────────

    private static async Task<IResult> AddEntryAsync(
        CreateDiaryEntryRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = DiaryEntryValidator.ValidateCreate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Food must exist; load it with its servings so we can validate the chosen serving and
        // compute the response nutrition without a second round-trip.
        var food = await db.FoodItems
            .Include(f => f.ServingSizes)
            .FirstOrDefaultAsync(f => f.Id == request.FoodItemId, ct);

        if (food is null)
        {
            return NotFound("Food not found.", $"No food with id '{request.FoodItemId}' exists.");
        }

        var serving = food.ServingSizes.FirstOrDefault(s => s.Id == request.ServingSizeId);
        if (serving is null)
        {
            return NotFound(
                "Serving size not found.",
                $"No serving size with id '{request.ServingSizeId}' belongs to food '{request.FoodItemId}'.");
        }

        var entry = DiaryEntry.Create(
            userId: user.Id,
            foodItemId: food.Id,
            servingSizeId: serving.Id,
            quantity: request.Quantity,
            mealType: DiaryEntryValidator.ParseMealType(request.MealType),
            date: DiaryEntryValidator.ParseDate(request.Date));

        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        var response = MapToResponse(entry, food, serving);
        return Results.Created($"/api/v1/me/diary/{entry.Id}", response);
    }

    // ── GET /api/v1/me/diary?date=YYYY-MM-DD ─────────────────────────────────────

    private static async Task<IResult> ListDayAsync(
        string? date,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        // Bound as nullable string to dodge the minimal-API "required value-type query param"
        // gotcha; validate the value here and 400 on a missing/malformed date.
        if (!DiaryEntryValidator.TryParseDate(date, out var parsedDate))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["date"] =
                    [
                        $"The 'date' query parameter is required and must be a valid calendar date " +
                        $"in {DiaryEntryValidator.DateFormat} format.",
                    ],
                });
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var entries = await db.DiaryEntries
            .AsNoTracking()
            .Include(e => e.FoodItem)
            .Include(e => e.ServingSize)
            .Where(e => e.UserId == user.Id && e.Date == parsedDate)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        // Group by meal in the canonical enum order; omit meals with no entries.
        var meals = new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner, MealType.Snack }
            .Select(meal => new DiaryMealGroup(
                MealType: meal.ToString(),
                Entries: entries
                    .Where(e => e.MealType == meal)
                    .Select(e => MapToResponse(e, e.FoodItem, e.ServingSize))
                    .ToList()))
            .Where(g => g.Entries.Count > 0)
            .ToList();

        return Results.Ok(new DiaryDayResponse(
            Date: parsedDate.ToString(DiaryEntryValidator.DateFormat),
            Meals: meals));
    }

    // ── PUT /api/v1/me/diary/{id} ─────────────────────────────────────────────────

    private static async Task<IResult> EditEntryAsync(
        Guid id,
        UpdateDiaryEntryRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = DiaryEntryValidator.ValidateUpdate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Scope by UserId so another user's entry is indistinguishable from a missing one (404).
        var entry = await db.DiaryEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id, ct);

        if (entry is null)
        {
            return NotFound(
                "Diary entry not found.",
                $"No diary entry with id '{id}' exists for this user.");
        }

        // The replacement serving must belong to the entry's (immutable) food. Load the food
        // with its servings to validate and to compute the response nutrition.
        var food = await db.FoodItems
            .Include(f => f.ServingSizes)
            .FirstOrDefaultAsync(f => f.Id == entry.FoodItemId, ct);

        if (food is null)
        {
            // Unreachable under the Restrict FK (foods are never hard-deleted), but guard anyway.
            return NotFound(
                "Food not found.",
                "The food referenced by this diary entry no longer exists.");
        }

        var serving = food.ServingSizes.FirstOrDefault(s => s.Id == request.ServingSizeId);
        if (serving is null)
        {
            return NotFound(
                "Serving size not found.",
                $"No serving size with id '{request.ServingSizeId}' belongs to food '{entry.FoodItemId}'.");
        }

        entry.Update(
            servingSizeId: serving.Id,
            quantity: request.Quantity,
            mealType: DiaryEntryValidator.ParseMealType(request.MealType),
            date: DiaryEntryValidator.ParseDate(request.Date));

        await db.SaveChangesAsync(ct);

        return Results.Ok(MapToResponse(entry, food, serving));
    }

    // ── DELETE /api/v1/me/diary/{id} ──────────────────────────────────────────────

    private static async Task<IResult> DeleteEntryAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var entry = await db.DiaryEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id, ct);

        if (entry is null)
        {
            return NotFound(
                "Diary entry not found.",
                $"No diary entry with id '{id}' exists for this user.");
        }

        db.DiaryEntries.Remove(entry);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static IResult NotFound(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// Maps a <see cref="DiaryEntry"/> to its response DTO, computing scaled nutrition from the
    /// supplied food and serving. The food and serving must be pre-loaded; this method does no I/O.
    /// <c>gramsConsumed = serving.GramsEquivalent * entry.Quantity</c>, then
    /// <c>food.NutritionPer100g.ScaleToGrams(gramsConsumed)</c>.
    /// </summary>
    private static DiaryEntryResponse MapToResponse(DiaryEntry entry, FoodItem food, ServingSize serving)
    {
        var gramsConsumed = serving.GramsEquivalent * entry.Quantity;
        var nutrition = food.NutritionPer100g.ScaleToGrams(gramsConsumed);

        return new DiaryEntryResponse(
            Id: entry.Id,
            FoodItemId: entry.FoodItemId,
            FoodName: food.Name,
            FoodBrand: food.Brand,
            ServingSizeId: entry.ServingSizeId,
            ServingLabel: serving.Label,
            ServingGramsEquivalent: serving.GramsEquivalent,
            Quantity: entry.Quantity,
            MealType: entry.MealType.ToString(),
            Date: entry.Date.ToString(DiaryEntryValidator.DateFormat),
            Nutrition: new DiaryEntryNutritionResponse(
                EnergyKcal: nutrition.EnergyKcal,
                ProteinG: nutrition.ProteinG,
                CarbohydrateG: nutrition.CarbohydrateG,
                FatG: nutrition.FatG,
                SugarsG: nutrition.SugarsG,
                FiberG: nutrition.FiberG,
                SaturatedFatG: nutrition.SaturatedFatG,
                SodiumMg: nutrition.SodiumMg),
            CreatedAt: entry.CreatedAt,
            UpdatedAt: entry.UpdatedAt);
    }
}
