using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Food;

/// <summary>
/// A food or product with nutritional data, sourced either from Open Food Facts or created by a
/// user (see <see cref="FoodSource"/>). Aggregate root for the food domain: it owns its per-100 g
/// <see cref="NutritionFacts"/> and its collection of <see cref="ServingSize"/> portions.
/// </summary>
/// <remarks>
/// The canonical reference for nutrition is per 100 g (<see cref="NutritionPer100g"/>). Every food
/// is created with a canonical "100 g" serving so a portion always exists to scale against; further
/// portions are added with <see cref="AddServingSize"/>. Provenance is tracked via
/// <see cref="Source"/>, <see cref="SourceReference"/> (the external catalogue id/code) and
/// <see cref="LastSyncedAt"/> (when the external data was last refreshed; <see langword="null"/>
/// for user-created foods).
/// </remarks>
public sealed class FoodItem : EntityBase
{
    /// <summary>Display name of the food, e.g. "Greek Yogurt". Required.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Brand or manufacturer, or <see langword="null"/> when unbranded/unknown.</summary>
    public string? Brand { get; private set; }

    /// <summary>
    /// Barcode (GTIN-8/12/13/14, i.e. UPC/EAN) as a string to preserve leading zeros, or
    /// <see langword="null"/> when the food has no barcode (typical for user-created foods).
    /// Indexed for lookup; not unique (regional variants and re-used GTINs legitimately collide).
    /// </summary>
    public string? Barcode { get; private set; }

    /// <summary>Provenance of this food's data. See <see cref="FoodSource"/>.</summary>
    public FoodSource Source { get; private set; }

    /// <summary>
    /// External catalogue identifier this food was sourced from (e.g. the Open Food Facts product
    /// code), or <see langword="null"/> for user-created foods.
    /// </summary>
    public string? SourceReference { get; private set; }

    /// <summary>
    /// UTC instant the external source data was last synchronised, or <see langword="null"/> if
    /// never synced (e.g. user-created foods). Distinct from <see cref="EntityBase.UpdatedAt"/>,
    /// which tracks any local row modification.
    /// </summary>
    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>
    /// Nutritional content on a per-100 g basis. Always present (required owned value).
    /// Initialized by EF on materialization; never <see langword="null"/> on a persisted instance.
    /// </summary>
    public NutritionFacts NutritionPer100g { get; private set; } = null!;

    // Backing field so the collection is never null (avoids CS8618 / NullReferenceException).
    private readonly List<ServingSize> _servingSizes = new();

    /// <summary>
    /// Serving portions for this food. Read-only externally; mutated only through
    /// <see cref="AddServingSize"/>.
    /// </summary>
    public IReadOnlyCollection<ServingSize> ServingSizes => _servingSizes;

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private FoodItem() { }

    /// <summary>
    /// Creates a new <see cref="FoodItem"/> with its per-100 g nutrition and a canonical
    /// "100 g" serving. The canonical serving is created with <c>IsDefault = false</c>; use
    /// <see cref="AddServingSize"/> with <c>isDefault: true</c> to designate a default portion.
    /// </summary>
    /// <param name="name">Display name. Required.</param>
    /// <param name="source">Provenance of the data.</param>
    /// <param name="nutritionPer100g">Per-100 g nutrition facts. Required.</param>
    /// <param name="brand">Brand/manufacturer, or <see langword="null"/>.</param>
    /// <param name="barcode">GTIN/EAN barcode, or <see langword="null"/>.</param>
    /// <param name="sourceReference">External catalogue id/code, or <see langword="null"/>.</param>
    /// <param name="lastSyncedAt">Last external-sync instant, or <see langword="null"/>.</param>
    public static FoodItem Create(
        string name,
        FoodSource source,
        NutritionFacts nutritionPer100g,
        string? brand = null,
        string? barcode = null,
        string? sourceReference = null,
        DateTimeOffset? lastSyncedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nutritionPer100g);

        var now = DateTimeOffset.UtcNow;
        var food = new FoodItem
        {
            Name = name,
            Source = source,
            NutritionPer100g = nutritionPer100g,
            Brand = brand,
            Barcode = barcode,
            SourceReference = sourceReference,
            LastSyncedAt = lastSyncedAt,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Canonical per-100 g serving: mirrors the nutrition basis so a portion always exists.
        food._servingSizes.Add(ServingSize.Create(
            food.Id, "100 g", quantity: 100m, unit: "g", gramsEquivalent: 100m, isDefault: false, createdAt: now));

        return food;
    }

    /// <summary>
    /// Adds a serving portion to the food. When <paramref name="isDefault"/> is
    /// <see langword="true"/>, any existing default serving is demoted so at most one serving is
    /// the default.
    /// </summary>
    /// <returns>The newly created <see cref="ServingSize"/>.</returns>
    public ServingSize AddServingSize(
        string label,
        decimal quantity,
        string unit,
        decimal gramsEquivalent,
        bool isDefault = false)
    {
        var now = DateTimeOffset.UtcNow;

        if (isDefault)
        {
            foreach (var existing in _servingSizes)
            {
                existing.ClearDefault(now);
            }
        }

        var serving = ServingSize.Create(Id, label, quantity, unit, gramsEquivalent, isDefault, now);
        _servingSizes.Add(serving);
        UpdatedAt = now;
        return serving;
    }
}
