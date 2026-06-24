namespace MAIHealthCoach.Api.Features.Goals;

/// <summary>
/// Request body for <c>PUT /api/v1/me/goals/overrides</c>. Every field is optional. Supplying
/// a value sets that override; supplying <see langword="null"/> (or omitting the field) clears
/// that override and reverts it to the computed value. An empty JSON object <c>{}</c> therefore
/// clears all overrides — matching HTTP <c>PUT</c> (replace, not patch) semantics.
/// </summary>
/// <param name="CaloriesKcal">Override for the daily calorie target in kcal.</param>
/// <param name="ProteinGrams">Override for the daily protein target in grams.</param>
/// <param name="CarbohydrateGrams">Override for the daily carbohydrate target in grams.</param>
/// <param name="FatGrams">Override for the daily fat target in grams.</param>
/// <param name="WaterMl">Override for the daily water target in millilitres.</param>
public record SetGoalOverridesRequest(
    int? CaloriesKcal,
    int? ProteinGrams,
    int? CarbohydrateGrams,
    int? FatGrams,
    int? WaterMl
);
