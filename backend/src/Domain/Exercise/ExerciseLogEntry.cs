using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Exercise;

/// <summary>
/// A single exercise-session log entry in a user's activity log (issue #34). Links a
/// <see cref="UserId">user</see> to an <see cref="ExerciseActivityId">activity</see> performed for
/// a <see cref="DurationMinutes">duration</see> on a specific <see cref="Date"/>, together with the
/// <see cref="CaloriesBurned">estimated calories burned</see> for that session.
/// </summary>
/// <remarks>
/// <para>
/// The chosen activity is fixed for the lifetime of the entry: <see cref="ExerciseActivityId"/> has
/// no setter in <see cref="Update"/>. Changing the activity is a delete-and-add operation, exactly
/// as the food diary treats <c>DiaryEntry.FoodItemId</c> — this keeps a logged session unambiguous
/// about which catalog activity it refers to.
/// </para>
/// <para>
/// <strong>Calories burned is a deliberate point-in-time snapshot.</strong> It is computed at
/// log/edit time from three inputs — the activity's MET value, the user's body weight
/// <em>at that moment</em>, and the duration — and then frozen onto the entry. It is intentionally
/// <em>not</em> recomputed on read and <em>not</em> recomputed if the catalog activity's MET value
/// is later edited. This is a deliberate deviation from the food diary, whose nutrition is
/// recomputed at read time: diary nutrition depends only on the (stable) food, whereas
/// calories-burned depends on the user's <em>changing</em> body weight. Snapshotting the three
/// inputs at write time is therefore the correct way to keep historical sessions stable — a session
/// logged last month must not silently change because the user weighed themselves today.
/// </para>
/// </remarks>
public sealed class ExerciseLogEntry : EntityBase
{
    /// <summary>Foreign key referencing the owning user's <c>Users.Id</c>.</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Foreign key referencing the logged <c>ExerciseActivities.Id</c>. Immutable after creation:
    /// changing the activity is delete-and-add, so <see cref="Update"/> never touches it.
    /// </summary>
    public Guid ExerciseActivityId { get; private set; }

    /// <summary>Duration of the activity session in minutes. Always positive.</summary>
    public int DurationMinutes { get; private set; }

    /// <summary>
    /// Estimated kilocalories burned for this session — a point-in-time snapshot frozen at
    /// log/edit time from the activity's MET value, the user's body weight at that time, and the
    /// duration. Never recomputed on read or when the catalog activity's MET changes later (see the
    /// type-level remarks). Always positive.
    /// </summary>
    public int CaloriesBurned { get; private set; }

    /// <summary>The calendar date the session was logged against. Future dates are permitted.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>
    /// EF navigation to the logged <see cref="ExerciseActivity"/>. Populated only when explicitly
    /// <c>Include</c>'d by a read query; it is not part of the write contract and is never used to
    /// mutate the activity from this entry.
    /// </summary>
    public ExerciseActivity ExerciseActivity { get; private set; } = null!;

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private ExerciseLogEntry() { }

    /// <summary>
    /// Creates a new <see cref="ExerciseLogEntry"/> for the given user, activity, duration, date,
    /// and pre-computed calories-burned snapshot. The internal key and audit timestamps are assigned
    /// here so the entity is fully initialized before it is added to the change tracker.
    /// </summary>
    /// <param name="userId">The owning user's internal <c>Users.Id</c>.</param>
    /// <param name="exerciseActivityId">The logged activity's <c>ExerciseActivities.Id</c>.</param>
    /// <param name="durationMinutes">Duration of the session in minutes. Must be positive.</param>
    /// <param name="date">The calendar date of the session.</param>
    /// <param name="caloriesBurned">
    /// The estimated calories-burned snapshot computed by the caller from the activity MET, the
    /// user's current body weight, and the duration. Must be positive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="durationMinutes"/> or <paramref name="caloriesBurned"/> is zero or negative.
    /// </exception>
    public static ExerciseLogEntry Create(
        Guid userId,
        Guid exerciseActivityId,
        int durationMinutes,
        DateOnly date,
        int caloriesBurned)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caloriesBurned);

        var now = DateTimeOffset.UtcNow;
        return new ExerciseLogEntry
        {
            UserId = userId,
            ExerciseActivityId = exerciseActivityId,
            DurationMinutes = durationMinutes,
            Date = date,
            CaloriesBurned = caloriesBurned,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Updates the mutable fields of this entry. The logged activity is immutable, so
    /// <see cref="ExerciseActivityId"/> is deliberately not a parameter; the caller recomputes
    /// <paramref name="caloriesBurned"/> from the unchanged activity's MET, the user's current body
    /// weight, and the new duration before calling this method. Changing <paramref name="date"/>
    /// implements the "move to another day" behaviour.
    /// </summary>
    /// <param name="durationMinutes">Replacement duration in minutes. Must be positive.</param>
    /// <param name="date">Replacement date.</param>
    /// <param name="caloriesBurned">Recomputed calories-burned snapshot. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="durationMinutes"/> or <paramref name="caloriesBurned"/> is zero or negative.
    /// </exception>
    public void Update(int durationMinutes, DateOnly date, int caloriesBurned)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationMinutes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caloriesBurned);

        DurationMinutes = durationMinutes;
        Date = date;
        CaloriesBurned = caloriesBurned;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
