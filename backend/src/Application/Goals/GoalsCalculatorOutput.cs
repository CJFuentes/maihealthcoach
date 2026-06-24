namespace MAIHealthCoach.Application.Goals;

/// <summary>
/// Immutable result of the goals calculation. All values are positive integers representing
/// daily targets. Units: kcal for calories, grams for macros, millilitres for water.
/// </summary>
/// <param name="Bmr">Basal Metabolic Rate in kcal/day (Mifflin-St Jeor).</param>
/// <param name="Tdee">Total Daily Energy Expenditure = BMR × activity multiplier.</param>
/// <param name="CaloriesKcal">Daily calorie target adjusted by goal, floored at the safe minimum.</param>
/// <param name="ProteinGrams">Daily protein target in grams.</param>
/// <param name="CarbohydrateGrams">Daily carbohydrate target in grams.</param>
/// <param name="FatGrams">Daily fat target in grams.</param>
/// <param name="WaterMl">Daily water intake target in millilitres.</param>
public record GoalsCalculatorOutput(
    int Bmr,
    int Tdee,
    int CaloriesKcal,
    int ProteinGrams,
    int CarbohydrateGrams,
    int FatGrams,
    int WaterMl
);
