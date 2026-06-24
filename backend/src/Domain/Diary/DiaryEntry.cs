using MAIHealthCoach.Domain.Common;
using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Domain.Diary;

/// <summary>
/// A single food log entry in a user's daily food diary (issue #22). Links a
/// <see cref="UserId">user</see> to a <see cref="FoodItem"/> for a specific
/// <see cref="Date"/>, <see cref="MealType">meal slot</see>, <see cref="ServingSize"/>, and
/// <see cref="Quantity"/>.
/// </summary>
/// <remarks>
/// Nutrition is <b>recomputed at read time</b> from the referenced
/// <see cref="FoodItem.NutritionPer100g"/> scaled by
/// <c><see cref="ServingSize.GramsEquivalent"/> * <see cref="Quantity"/></c> — it is never
/// snapshotted onto the row. The food domain keeps a food's <see cref="EntityBase.Id"/> stable
/// across cache refreshes (see <see cref="FoodItem.RefreshFromSource"/>), so the reference stays
/// valid and history reflects the latest nutrition rather than going stale.
/// <para>
/// <see cref="FoodItemId"/> is immutable after creation: changing which food is logged is a
/// delete-and-add, not an edit. This preserves the invariant that <see cref="ServingSizeId"/>
/// always belongs to <see cref="FoodItemId"/>. The <see cref="FoodItem"/> and
/// <see cref="ServingSize"/> navigations exist purely so EF can eager-load related data via
/// <c>Include</c>; they are not part of the domain write contract.
/// </para>
/// </remarks>
public sealed class DiaryEntry : EntityBase
{
    /// <summary>Foreign key referencing the owning user's <c>Users.Id</c>.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Foreign key referencing the logged food's <c>FoodItems.Id</c>.</summary>
    public Guid FoodItemId { get; private set; }

    /// <summary>
    /// Foreign key referencing the chosen serving's <c>ServingSizes.Id</c>. The serving must
    /// belong to <see cref="FoodItemId"/> (enforced in the API layer before persisting).
    /// </summary>
    public Guid ServingSizeId { get; private set; }

    /// <summary>
    /// Number of servings consumed. Always positive. Persisted with precision (9, 3) to support
    /// fractional quantities (e.g. 1.5 cups).
    /// </summary>
    public decimal Quantity { get; private set; }

    /// <summary>The meal slot within the day.</summary>
    public MealType MealType { get; private set; }

    /// <summary>The calendar date of the log entry. Future dates are permitted.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>
    /// EF navigation to the logged food. Populated only when explicitly <c>Include</c>d;
    /// not part of the domain contract. Suppressed-null because EF sets it on materialization.
    /// </summary>
    public FoodItem FoodItem { get; private set; } = null!;

    /// <summary>
    /// EF navigation to the chosen serving. Populated only when explicitly <c>Include</c>d;
    /// not part of the domain contract. Suppressed-null because EF sets it on materialization.
    /// </summary>
    public ServingSize ServingSize { get; private set; } = null!;

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private DiaryEntry() { }

    /// <summary>
    /// Creates a new <see cref="DiaryEntry"/> for the given user, food, serving, quantity, meal,
    /// and date. The internal key and audit timestamps are assigned here so the entity is fully
    /// initialized before it is added to the change tracker.
    /// </summary>
    /// <param name="userId">The owning user's internal <c>Users.Id</c>.</param>
    /// <param name="foodItemId">The logged food's <c>FoodItems.Id</c>.</param>
    /// <param name="servingSizeId">The chosen serving's <c>ServingSizes.Id</c>.</param>
    /// <param name="quantity">Number of servings. Must be positive.</param>
    /// <param name="mealType">The meal slot.</param>
    /// <param name="date">The calendar date of the entry.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="quantity"/> is zero or negative.
    /// </exception>
    public static DiaryEntry Create(
        Guid userId,
        Guid foodItemId,
        Guid servingSizeId,
        decimal quantity,
        MealType mealType,
        DateOnly date)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var now = DateTimeOffset.UtcNow;
        return new DiaryEntry
        {
            UserId = userId,
            FoodItemId = foodItemId,
            ServingSizeId = servingSizeId,
            Quantity = quantity,
            MealType = mealType,
            Date = date,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Updates the mutable fields of this entry. Changing <paramref name="date"/> or
    /// <paramref name="mealType"/> implements the "copy/move to another day or meal" behaviour.
    /// <see cref="FoodItemId"/> is intentionally not editable here.
    /// </summary>
    /// <param name="servingSizeId">
    /// Replacement serving. Must belong to <see cref="FoodItemId"/> (validated in the API layer).
    /// </param>
    /// <param name="quantity">Replacement quantity. Must be positive.</param>
    /// <param name="mealType">Replacement meal slot.</param>
    /// <param name="date">Replacement date.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="quantity"/> is zero or negative.
    /// </exception>
    public void Update(
        Guid servingSizeId,
        decimal quantity,
        MealType mealType,
        DateOnly date)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        ServingSizeId = servingSizeId;
        Quantity = quantity;
        MealType = mealType;
        Date = date;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
