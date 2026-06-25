using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Exercise;

/// <summary>
/// An activity in the exercise catalog — a named physical activity with a MET value used to
/// estimate calories burned (issue #33). Aggregate root for the exercise domain.
/// </summary>
/// <remarks>
/// Shared/seeded activities have a <see langword="null"/> <see cref="CreatedByUserId"/> and are
/// visible to all users. Custom activities (user-created) have a non-null
/// <see cref="CreatedByUserId"/> and are visible only to their creator — the same ownership and
/// privacy boundary used by custom foods (issue #24).
/// <para>
/// Calories burned from an activity are not stored here; they are computed on demand by the
/// <c>CaloriesBurnedCalculator</c> application service using this activity's
/// <see cref="MetValue"/>, the user's current body weight, and the exercise duration. The
/// exercise log (issue #34) and its UI (issue #35) build on this catalog.
/// </para>
/// </remarks>
public sealed class ExerciseActivity : EntityBase
{
    /// <summary>Display name of the activity, e.g. "Running (6 mph / 10 min/mile)". Required.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Broad activity category. Stored as a readable string in the database.</summary>
    public ExerciseCategory Category { get; private set; }

    /// <summary>
    /// Metabolic Equivalent of Task (MET). Represents the ratio of metabolic rate during the
    /// activity to the resting metabolic rate (1.0 MET ≈ 1 kcal per kg per hour at rest). Always
    /// positive. Persisted with precision (4, 2), so values range from 0.01 to 99.99.
    /// </summary>
    public decimal MetValue { get; private set; }

    /// <summary>
    /// Nullable foreign key to the <c>User</c> who created this custom activity, or
    /// <see langword="null"/> for shared seeded activities. Custom activities
    /// (non-null <see cref="CreatedByUserId"/>) are visible only to their creator.
    /// Ownership is immutable — there is no method to reassign it.
    /// </summary>
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private ExerciseActivity() { }

    /// <summary>
    /// Creates a new shared (seeded) <see cref="ExerciseActivity"/> with a null owner. The
    /// internal key and audit timestamps are assigned here so the entity is fully initialized
    /// before it is added to the change tracker.
    /// </summary>
    /// <param name="name">Display name. Required, non-blank; trimmed before storage.</param>
    /// <param name="category">Activity category.</param>
    /// <param name="metValue">MET value. Must be greater than zero.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="metValue"/> is zero or negative.
    /// </exception>
    public static ExerciseActivity Create(string name, ExerciseCategory category, decimal metValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metValue);

        var now = DateTimeOffset.UtcNow;
        return new ExerciseActivity
        {
            Name = name.Trim(),
            Category = category,
            MetValue = metValue,
            CreatedByUserId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Creates a user-authored custom <see cref="ExerciseActivity"/> owned by
    /// <paramref name="createdByUserId"/>. Mirrors <see cref="Create"/> but stamps ownership so
    /// the activity is scoped to its creator (issue #33).
    /// </summary>
    /// <param name="createdByUserId">Owner user id. Must be non-empty.</param>
    /// <param name="name">Display name. Required, non-blank; trimmed before storage.</param>
    /// <param name="category">Activity category.</param>
    /// <param name="metValue">MET value. Must be greater than zero.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null/blank, or <paramref name="createdByUserId"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="metValue"/> is zero or negative.
    /// </exception>
    public static ExerciseActivity CreateCustom(
        Guid createdByUserId,
        string name,
        ExerciseCategory category,
        decimal metValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(metValue);

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "A custom exercise activity must have a non-empty owner id.",
                nameof(createdByUserId));
        }

        var now = DateTimeOffset.UtcNow;
        return new ExerciseActivity
        {
            Name = name.Trim(),
            Category = category,
            MetValue = metValue,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
