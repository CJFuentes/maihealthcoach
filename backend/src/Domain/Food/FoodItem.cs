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
    /// Nullable foreign key to the <c>User</c> who created this custom food, or
    /// <see langword="null"/> for shared Open Food Facts-sourced foods. Custom foods
    /// (<see cref="FoodSource.Custom"/>) always have a non-null value; this is the
    /// ownership/privacy boundary that scopes a custom food to its creator (issue #24).
    /// Ownership is immutable — there is no method to reassign it.
    /// </summary>
    public Guid? CreatedByUserId { get; private set; }

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
    /// Creates a user-authored custom food (<see cref="FoodSource.Custom"/>) owned by
    /// <paramref name="createdByUserId"/>, with its per-100 g nutrition and a canonical
    /// "100 g" serving. Mirrors <see cref="Create"/> but stamps ownership and leaves all
    /// external-catalogue fields (<see cref="Barcode"/>, <see cref="SourceReference"/>,
    /// <see cref="LastSyncedAt"/>) <see langword="null"/> (issue #24).
    /// </summary>
    /// <param name="createdByUserId">Owner user id. Required and must be non-empty.</param>
    /// <param name="name">Display name. Required.</param>
    /// <param name="nutritionPer100g">Per-100 g nutrition facts. Required.</param>
    /// <param name="brand">Brand/manufacturer, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null/blank, or <paramref name="createdByUserId"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="nutritionPer100g"/> is null.</exception>
    public static FoodItem CreateCustom(
        Guid createdByUserId,
        string name,
        NutritionFacts nutritionPer100g,
        string? brand = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nutritionPer100g);
        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("A custom food must have a non-empty owner id.", nameof(createdByUserId));
        }

        var now = DateTimeOffset.UtcNow;
        var food = new FoodItem
        {
            Name = name,
            Source = FoodSource.Custom,
            CreatedByUserId = createdByUserId,
            NutritionPer100g = nutritionPer100g,
            Brand = brand,
            Barcode = null,
            SourceReference = null,
            LastSyncedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // Canonical per-100 g serving: mirrors the nutrition basis so a portion always exists.
        food._servingSizes.Add(ServingSize.Create(
            food.Id, "100 g", quantity: 100m, unit: "g", gramsEquivalent: 100m, isDefault: false, createdAt: now));

        return food;
    }

    /// <summary>
    /// Edits a custom food's identifying fields and per-100 g nutrition. Does not touch the
    /// serving collection — use <see cref="ReplaceCustomServings"/> for that (issue #24).
    /// </summary>
    /// <param name="name">Replacement display name. Required.</param>
    /// <param name="brand">Replacement brand/manufacturer, or <see langword="null"/>.</param>
    /// <param name="nutritionPer100g">Replacement per-100 g nutrition facts. Required.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null/blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="nutritionPer100g"/> is null.</exception>
    public void UpdateCustomDetails(string name, string? brand, NutritionFacts nutritionPer100g)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nutritionPer100g);

        Name = name;
        Brand = brand;
        NutritionPer100g = nutritionPer100g;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Replaces the non-canonical servings of a custom food with the supplied set: keeps exactly
    /// the canonical "100 g" serving (recreating it if missing), drops all others, then re-adds
    /// each provided serving via <see cref="AddServingSize"/> (which demotes prior defaults so at
    /// most one serving is default). If, after re-adding, no serving is default, the canonical
    /// serving is promoted so a default portion always exists (issue #24).
    /// </summary>
    /// <remarks>
    /// Each provided serving is validated by <see cref="ServingSize.Create"/> (quantity and
    /// grams-equivalent must be &gt; 0, label/unit non-blank), so invalid input throws. The API
    /// layer must validate the request BEFORE calling this so it returns 400, not 500.
    /// </remarks>
    /// <param name="servings">
    /// The replacement non-canonical servings as
    /// <c>(Label, Quantity, Unit, GramsEquivalent, IsDefault)</c> tuples.
    /// </param>
    public void ReplaceCustomServings(
        IReadOnlyCollection<(string Label, decimal Quantity, string Unit, decimal GramsEquivalent, bool IsDefault)> servings)
    {
        ArgumentNullException.ThrowIfNull(servings);

        var now = DateTimeOffset.UtcNow;

        // Identify the canonical 100 g serving (Unit == "g" && GramsEquivalent == 100).
        var canonical = _servingSizes.FirstOrDefault(IsCanonical);

        // Defensive: a well-formed custom food always carries a canonical serving, but if it is
        // somehow missing recreate it so the "exactly one canonical 100 g serving" invariant holds.
        if (canonical is null)
        {
            canonical = ServingSize.Create(Id, "100 g", quantity: 100m, unit: "g", gramsEquivalent: 100m, isDefault: false, createdAt: now);
            _servingSizes.Add(canonical);
        }

        // Drop every serving that is not THE canonical one (also removes duplicate canonical
        // servings, keeping only the first), so we re-add from a clean canonical base.
        _servingSizes.RemoveAll(s => !ReferenceEquals(s, canonical));

        // Re-add each provided serving. AddServingSize demotes any existing default first, so the
        // last serving flagged default wins and the canonical serving is cleared if it was default.
        foreach (var (label, quantity, unit, gramsEquivalent, isDefault) in servings)
        {
            AddServingSize(label, quantity, unit, gramsEquivalent, isDefault);
        }

        // Ensure exactly one default. If nothing is default (e.g. no provided serving was flagged),
        // promote the canonical serving so a default portion always exists.
        if (!_servingSizes.Any(s => s.IsDefault))
        {
            canonical.SetAsDefault(now);
        }

        UpdatedAt = now;
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

    /// <summary>
    /// Refreshes this cached food in place from a fresh Open Food Facts fetch, keeping the
    /// existing <see cref="EntityBase.Id"/> stable so future diary references remain valid
    /// (issue #22). Updates the identifying/nutrition fields and reconciles the serving
    /// collection so it carries exactly the canonical "100 g" serving plus, when supplied, the
    /// single OFF-derived serving as the default.
    /// </summary>
    /// <remarks>
    /// This method owns the food's invariants for a sync refresh: it never deletes-and-recreates
    /// the row, never duplicates the canonical 100 g serving, never leaves a serving with a zero
    /// grams-equivalent, and always leaves exactly one serving marked default. <see cref="Source"/>
    /// is left unchanged — cache rows are always <see cref="FoodSource.OpenFoodFacts"/>.
    /// Only already-mapped state is touched, so no EF migration is required.
    /// </remarks>
    /// <param name="name">Refreshed display name. Required.</param>
    /// <param name="brand">Refreshed brand/manufacturer, or <see langword="null"/>.</param>
    /// <param name="nutritionPer100g">Refreshed per-100 g nutrition facts. Required.</param>
    /// <param name="offServing">
    /// The single OFF-derived serving to set as default, or <see langword="null"/> when OFF
    /// provided no usable serving (the canonical 100 g serving then becomes the default).
    /// </param>
    /// <param name="sourceReference">Refreshed external catalogue id/code, or <see langword="null"/>.</param>
    /// <param name="syncedAt">The instant of this external sync; recorded as <see cref="LastSyncedAt"/>.</param>
    public void RefreshFromSource(
        string name,
        string? brand,
        NutritionFacts nutritionPer100g,
        OffServing? offServing,
        string? sourceReference,
        DateTimeOffset syncedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nutritionPer100g);

        var now = DateTimeOffset.UtcNow;

        Name = name;
        Brand = brand;
        NutritionPer100g = nutritionPer100g;
        SourceReference = sourceReference;
        LastSyncedAt = syncedAt;
        UpdatedAt = now;

        ReconcileServings(offServing, now);
    }

    /// <summary>
    /// Rebuilds the serving collection around the canonical 100 g serving: keeps exactly one
    /// canonical serving, drops all non-canonical servings, optionally adds the OFF serving as
    /// the default, and guarantees exactly one default serving.
    /// </summary>
    private void ReconcileServings(OffServing? offServing, DateTimeOffset now)
    {
        // Identify the canonical 100 g serving (Unit == "g" && GramsEquivalent == 100).
        var canonical = _servingSizes.FirstOrDefault(IsCanonical);

        // Defensive: a well-formed food always carries a canonical serving, but if it is somehow
        // missing (e.g. the row was loaded without its servings), recreate it so the invariant
        // "exactly one canonical 100 g serving" always holds after a refresh.
        if (canonical is null)
        {
            canonical = ServingSize.Create(Id, "100 g", quantity: 100m, unit: "g", gramsEquivalent: 100m, isDefault: false, createdAt: now);
            _servingSizes.Add(canonical);
        }

        // Remove every serving that is not THE canonical one (this also drops duplicate
        // canonical servings, keeping only the first), so we start from a clean canonical base.
        _servingSizes.RemoveAll(s => !ReferenceEquals(s, canonical));

        // Add the OFF serving as the default when it carries usable grams. AddServingSize demotes
        // any existing default first, so the canonical serving (if it was default) is cleared.
        if (offServing is { GramsEquivalent: > 0m })
        {
            AddServingSize(offServing.Label, offServing.Quantity, offServing.Unit, offServing.GramsEquivalent, isDefault: true);
        }

        // Ensure exactly one default. If nothing is default (e.g. OFF gave no serving), promote
        // the canonical serving so a default portion always exists.
        if (canonical is not null && !_servingSizes.Any(s => s.IsDefault))
        {
            canonical.SetAsDefault(now);
        }
    }

    private static bool IsCanonical(ServingSize serving) =>
        serving.GramsEquivalent == 100m
        && string.Equals(serving.Unit, "g", StringComparison.Ordinal);
}
