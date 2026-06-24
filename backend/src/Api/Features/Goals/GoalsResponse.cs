namespace MAIHealthCoach.Api.Features.Goals;

/// <summary>
/// A single goal target that may be either computed from the profile or manually overridden.
/// </summary>
/// <param name="Value">The effective value to use (the override when set, otherwise the computed value).</param>
/// <param name="Computed">The value the formula produces from the profile, regardless of any override.</param>
/// <param name="IsOverridden"><see langword="true"/> when a manual override is in effect for this field.</param>
public record GoalValue(int Value, int Computed, bool IsOverridden);

/// <summary>
/// Response body for <c>GET /api/v1/me/goals</c> and <c>PUT /api/v1/me/goals/overrides</c>.
/// Each target is a <see cref="GoalValue"/> carrying both the effective and the computed
/// value so clients can surface "overriding X" hints. <see cref="Bmr"/> and <see cref="Tdee"/>
/// are informational and are never overridden.
/// </summary>
/// <param name="Calories">Daily calorie target in kcal.</param>
/// <param name="ProteinGrams">Daily protein target in grams.</param>
/// <param name="CarbohydrateGrams">Daily carbohydrate target in grams.</param>
/// <param name="FatGrams">Daily fat target in grams.</param>
/// <param name="WaterMl">Daily water target in millilitres.</param>
/// <param name="Bmr">Basal Metabolic Rate in kcal/day (informational).</param>
/// <param name="Tdee">Total Daily Energy Expenditure in kcal/day (informational).</param>
/// <param name="LastOverriddenAt">UTC instant of the most recent override PUT, or <see langword="null"/>.</param>
public record GoalsResponse(
    GoalValue Calories,
    GoalValue ProteinGrams,
    GoalValue CarbohydrateGrams,
    GoalValue FatGrams,
    GoalValue WaterMl,
    int Bmr,
    int Tdee,
    DateTimeOffset? LastOverriddenAt
);
