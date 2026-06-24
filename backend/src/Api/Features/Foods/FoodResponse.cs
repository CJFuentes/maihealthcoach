namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Nutritional content of a food on a per-100 g basis. The four macro fields are always present
/// (a zero is meaningful, e.g. water is 0/0/0/0); the micro fields are <see langword="null"/> when
/// the source did not provide them.
/// </summary>
/// <param name="EnergyKcal">Food energy in kilocalories per 100 g.</param>
/// <param name="ProteinG">Protein in grams per 100 g.</param>
/// <param name="CarbohydrateG">Total carbohydrate in grams per 100 g.</param>
/// <param name="FatG">Total fat in grams per 100 g.</param>
/// <param name="SugarsG">Of-which sugars in grams per 100 g, or <see langword="null"/> if unknown.</param>
/// <param name="FiberG">Dietary fibre in grams per 100 g, or <see langword="null"/> if unknown.</param>
/// <param name="SaturatedFatG">Of-which saturates in grams per 100 g, or <see langword="null"/> if unknown.</param>
/// <param name="SodiumMg">Sodium in milligrams per 100 g, or <see langword="null"/> if unknown.</param>
public record NutritionResponse(
    decimal EnergyKcal,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    decimal? SugarsG,
    decimal? FiberG,
    decimal? SaturatedFatG,
    decimal? SodiumMg
);

/// <summary>
/// A named portion of a food (e.g. "1 cup", "100 g"). Its <see cref="GramsEquivalent"/> scales the
/// food's per-100 g nutrition to the portion.
/// </summary>
/// <param name="Id">Serving internal identifier.</param>
/// <param name="Label">Human-readable label for the portion, e.g. "1 cup" or "100 g".</param>
/// <param name="Quantity">Numeric quantity in the serving's <paramref name="Unit"/>.</param>
/// <param name="Unit">Unit of the <paramref name="Quantity"/>, e.g. "cup", "slice", "g".</param>
/// <param name="GramsEquivalent">Mass of this serving in grams.</param>
/// <param name="IsDefault">Whether this is the food's default serving for logging.</param>
public record ServingSizeResponse(
    Guid Id,
    string Label,
    decimal Quantity,
    string Unit,
    decimal GramsEquivalent,
    bool IsDefault
);

/// <summary>
/// Response body for <c>GET /api/v1/foods/{id}</c>, <c>GET /api/v1/foods/barcode/{code}</c>, and
/// each search hit. <see cref="Source"/> is serialised as the enum string name (e.g. "OpenFoodFacts").
/// </summary>
/// <param name="Id">Food internal identifier.</param>
/// <param name="Name">Display name of the food, e.g. "Greek Yogurt".</param>
/// <param name="Brand">Brand or manufacturer, or <see langword="null"/> when unbranded/unknown.</param>
/// <param name="Barcode">GTIN/EAN barcode as a string (preserves leading zeros), or <see langword="null"/>.</param>
/// <param name="Source">Provenance of the data as a string (e.g. "OpenFoodFacts" or "Custom").</param>
/// <param name="SourceReference">External catalogue id/code, or <see langword="null"/>.</param>
/// <param name="LastSyncedAt">UTC instant the external source was last synced, or <see langword="null"/>.</param>
/// <param name="NutritionPer100g">Per-100 g nutrition facts.</param>
/// <param name="ServingSizes">Serving portions, the default first.</param>
public record FoodResponse(
    Guid Id,
    string Name,
    string? Brand,
    string? Barcode,
    string Source,
    string? SourceReference,
    DateTimeOffset? LastSyncedAt,
    NutritionResponse NutritionPer100g,
    IReadOnlyList<ServingSizeResponse> ServingSizes
);

/// <summary>
/// A single entry in a <see cref="FoodSearchResponse.Items"/> array: a mapped food plus its rank in
/// the upstream Open Food Facts result ordering.
/// </summary>
/// <param name="Rank">1-based ordinal within the upstream result page (1 = most relevant).</param>
/// <param name="Food">The mapped food for this candidate.</param>
public record FoodSearchItem(int Rank, FoodResponse Food);

/// <summary>
/// Response body for <c>GET /api/v1/foods</c> (text search). A paged list of ranked matches.
/// </summary>
/// <param name="Query">The free-text query that produced these results.</param>
/// <param name="Page">The 1-based page of results this represents.</param>
/// <param name="PageSize">The effective page size applied to <paramref name="Items"/>.</param>
/// <param name="Count">Number of items in this page (equals <c>Items.Count</c>).</param>
/// <param name="Items">The ranked matches for this page.</param>
public record FoodSearchResponse(
    string Query,
    int Page,
    int PageSize,
    int Count,
    IReadOnlyList<FoodSearchItem> Items
);
