namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Per-100 g nutrition payload supplied when creating or editing a custom food (issue #24). The
/// four macro fields are required (a zero is meaningful, e.g. water is 0/0/0/0); the micro fields
/// are optional and omitted/<see langword="null"/> when unknown.
/// </summary>
/// <param name="EnergyKcal">Food energy in kilocalories per 100 g. Required, non-negative.</param>
/// <param name="ProteinG">Protein in grams per 100 g. Required, non-negative.</param>
/// <param name="CarbohydrateG">Total carbohydrate in grams per 100 g. Required, non-negative.</param>
/// <param name="FatG">Total fat in grams per 100 g. Required, non-negative.</param>
/// <param name="SugarsG">Of-which sugars in grams per 100 g, or <see langword="null"/> if unknown.</param>
/// <param name="FiberG">Dietary fibre in grams per 100 g, or <see langword="null"/> if unknown.</param>
/// <param name="SaturatedFatG">Of-which saturates in grams per 100 g, or <see langword="null"/> if unknown.</param>
/// <param name="SodiumMg">Sodium in milligrams per 100 g, or <see langword="null"/> if unknown.</param>
public record NutritionRequest(
    decimal EnergyKcal,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    decimal? SugarsG,
    decimal? FiberG,
    decimal? SaturatedFatG,
    decimal? SodiumMg);

/// <summary>
/// A non-canonical serving portion supplied when creating or editing a custom food (issue #24).
/// The canonical "100 g" serving is always present automatically and need not be supplied.
/// </summary>
/// <param name="Label">Human-readable label for the portion, e.g. "1 cup". Required, non-blank.</param>
/// <param name="Quantity">Numeric quantity in the serving's <paramref name="Unit"/>. Must be &gt; 0.</param>
/// <param name="Unit">Unit of the <paramref name="Quantity"/>, e.g. "cup". Required, non-blank.</param>
/// <param name="GramsEquivalent">Mass of this serving in grams. Must be &gt; 0.</param>
/// <param name="IsDefault">Whether this serving is the food's default for logging.</param>
public record ServingSizeRequest(
    string Label,
    decimal Quantity,
    string Unit,
    decimal GramsEquivalent,
    bool IsDefault);

/// <summary>
/// Request body for <c>POST /api/v1/me/foods</c> — create a custom food owned by the caller
/// (issue #24). When <paramref name="Servings"/> is omitted, the food carries only the canonical
/// "100 g" serving.
/// </summary>
/// <param name="Name">Display name. Required, non-blank, max 256 chars.</param>
/// <param name="Brand">Brand/manufacturer, or <see langword="null"/>. Max 256 chars when present.</param>
/// <param name="Nutrition">Per-100 g nutrition facts. Required.</param>
/// <param name="Servings">Optional non-canonical servings to add.</param>
public record CreateCustomFoodRequest(
    string Name,
    string? Brand,
    NutritionRequest Nutrition,
    IReadOnlyList<ServingSizeRequest>? Servings);

/// <summary>
/// Request body for <c>PUT /api/v1/me/foods/{id}</c> — edit one of the caller's custom foods
/// (issue #24). When <paramref name="Servings"/> is omitted, the existing servings are left
/// unchanged; when supplied, the non-canonical servings are replaced wholesale.
/// </summary>
/// <param name="Name">Replacement display name. Required, non-blank, max 256 chars.</param>
/// <param name="Brand">Replacement brand/manufacturer, or <see langword="null"/>. Max 256 chars.</param>
/// <param name="Nutrition">Replacement per-100 g nutrition facts. Required.</param>
/// <param name="Servings">Optional replacement non-canonical servings.</param>
public record UpdateCustomFoodRequest(
    string Name,
    string? Brand,
    NutritionRequest Nutrition,
    IReadOnlyList<ServingSizeRequest>? Servings);

/// <summary>
/// Response wrapper for the custom-foods, favorites, and recents listings (issue #24): a count
/// plus the mapped <see cref="FoodResponse"/> items in their intended display order.
/// </summary>
/// <param name="Count">Number of items in <paramref name="Items"/>.</param>
/// <param name="Items">The mapped foods, ordered for display (most-recent-first where applicable).</param>
public record FoodListResponse(
    int Count,
    IReadOnlyList<FoodResponse> Items);
