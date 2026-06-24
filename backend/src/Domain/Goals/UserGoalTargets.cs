using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Goals;

/// <summary>
/// Persists manual goal-target overrides for a user (issue #17). All override fields are
/// nullable; a <see langword="null"/> value means "use the computed value derived from the
/// profile". The row is created lazily on the first override <c>PUT</c> and is 1:1 with
/// <c>User</c> (enforced by a unique index on <see cref="UserId"/>).
/// </summary>
/// <remarks>
/// The goals themselves are always recomputed from the profile on each request; this entity
/// only carries the layered-on overrides. <see cref="SetOverrides"/> replaces the entire
/// override state atomically, matching HTTP <c>PUT</c> (replace, not patch) semantics — so a
/// request that omits a field clears that field's override.
/// </remarks>
public sealed class UserGoalTargets : EntityBase
{
    /// <summary>Foreign key referencing <c>Users.Id</c>. Unique — one override row per user.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Overridden daily calorie target in kcal, or <see langword="null"/> to use computed.</summary>
    public int? CaloriesKcal { get; private set; }

    /// <summary>Overridden daily protein target in grams, or <see langword="null"/> to use computed.</summary>
    public int? ProteinGrams { get; private set; }

    /// <summary>Overridden daily carbohydrate target in grams, or <see langword="null"/> to use computed.</summary>
    public int? CarbohydrateGrams { get; private set; }

    /// <summary>Overridden daily fat target in grams, or <see langword="null"/> to use computed.</summary>
    public int? FatGrams { get; private set; }

    /// <summary>Overridden daily water target in millilitres, or <see langword="null"/> to use computed.</summary>
    public int? WaterMl { get; private set; }

    /// <summary>
    /// UTC instant of the most recent call to <see cref="SetOverrides"/>, or
    /// <see langword="null"/> if overrides have never been set for this user.
    /// </summary>
    public DateTimeOffset? LastOverriddenAt { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private UserGoalTargets() { }

    /// <summary>
    /// Creates a new <see cref="UserGoalTargets"/> row for the given user with all override
    /// fields initially <see langword="null"/> (computed values apply).
    /// </summary>
    /// <param name="userId">The <c>Users.Id</c> of the owning user. Required.</param>
    public static UserGoalTargets Create(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserGoalTargets
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Replaces the entire override state. Any non-<see langword="null"/> parameter sets that
    /// override; passing <see langword="null"/> clears that override (reverts to the computed
    /// value). <see cref="EntityBase.UpdatedAt"/> and <see cref="LastOverriddenAt"/> are both
    /// bumped to <see cref="DateTimeOffset.UtcNow"/> on every call.
    /// </summary>
    public void SetOverrides(
        int? caloriesKcal,
        int? proteinGrams,
        int? carbohydrateGrams,
        int? fatGrams,
        int? waterMl)
    {
        CaloriesKcal = caloriesKcal;
        ProteinGrams = proteinGrams;
        CarbohydrateGrams = carbohydrateGrams;
        FatGrams = fatGrams;
        WaterMl = waterMl;

        var now = DateTimeOffset.UtcNow;
        LastOverriddenAt = now;
        UpdatedAt = now;
    }
}
