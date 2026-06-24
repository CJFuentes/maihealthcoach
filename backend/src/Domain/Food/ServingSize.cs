using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Food;

/// <summary>
/// A named portion of a <see cref="FoodItem"/> (e.g. "1 cup", "1 slice", "100 g"). Each serving
/// records its <see cref="GramsEquivalent"/> so the food's per-100 g <see cref="NutritionFacts"/>
/// can be scaled to the portion. Extends <see cref="EntityBase"/> for its own stable <c>Id</c>
/// and audit timestamps.
/// </summary>
/// <remarks>
/// Servings are owned by the <see cref="FoodItem"/> aggregate: create and mutate them only
/// through <see cref="FoodItem.AddServingSize"/>, never directly. Every food carries a canonical
/// "100 g" serving (<see cref="GramsEquivalent"/> = 100) that mirrors the per-100 g nutrition basis.
/// </remarks>
public sealed class ServingSize : EntityBase
{
    /// <summary>Foreign key referencing the owning <see cref="FoodItem.Id"/>.</summary>
    public Guid FoodItemId { get; private set; }

    /// <summary>Human-readable label for the portion, e.g. "1 cup" or "100 g".</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>Numeric quantity in the serving's <see cref="Unit"/> (e.g. <c>1</c> for "1 cup").</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Unit of the <see cref="Quantity"/>, e.g. "cup", "slice", "g".</summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>
    /// Mass of this serving in grams. Used to scale the food's per-100 g
    /// <see cref="NutritionFacts"/> to the portion via <see cref="NutritionFacts.ScaleToGrams"/>.
    /// </summary>
    public decimal GramsEquivalent { get; private set; }

    /// <summary>
    /// Whether this is the food's default serving for logging. At most one serving per food is
    /// the default; the invariant is maintained by <see cref="FoodItem.AddServingSize"/>.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private ServingSize() { }

    /// <summary>
    /// Creates a new <see cref="ServingSize"/> for the given food. Called only by the
    /// <see cref="FoodItem"/> aggregate so the parent FK and audit timestamps are set coherently.
    /// </summary>
    internal static ServingSize Create(
        Guid foodItemId,
        string label,
        decimal quantity,
        string unit,
        decimal gramsEquivalent,
        bool isDefault,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gramsEquivalent);

        return new ServingSize
        {
            FoodItemId = foodItemId,
            Label = label,
            Quantity = quantity,
            Unit = unit,
            GramsEquivalent = gramsEquivalent,
            IsDefault = isDefault,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    /// <summary>Clears the default flag. Used by the aggregate to keep a single default serving.</summary>
    /// <param name="updatedAt">Timestamp of the enclosing aggregate operation, for coherent audit stamps.</param>
    internal void ClearDefault(DateTimeOffset updatedAt)
    {
        if (IsDefault)
        {
            IsDefault = false;
            UpdatedAt = updatedAt;
        }
    }

    /// <summary>Sets the default flag. Used by the aggregate to promote a serving to the default.</summary>
    /// <param name="updatedAt">Timestamp of the enclosing aggregate operation, for coherent audit stamps.</param>
    internal void SetAsDefault(DateTimeOffset updatedAt)
    {
        if (!IsDefault)
        {
            IsDefault = true;
            UpdatedAt = updatedAt;
        }
    }
}
