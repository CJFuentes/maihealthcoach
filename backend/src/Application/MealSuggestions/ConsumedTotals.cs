namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// Optional snapshot of today's consumed nutrition totals, used to subtract from the daily
/// targets when computing the remaining budget for meal suggestions. Every field defaults to
/// <c>0</c>, meaning "nothing consumed yet". The food diary feature (issue #22) will populate
/// these values from logged entries; until then the remaining budget equals the full daily target.
/// </summary>
/// <param name="CaloriesKcal">Calories consumed so far today, in kcal.</param>
/// <param name="ProteinGrams">Protein consumed so far today, in grams.</param>
/// <param name="CarbGrams">Carbohydrates consumed so far today, in grams.</param>
/// <param name="FatGrams">Fat consumed so far today, in grams.</param>
/// <param name="WaterMl">Water consumed so far today, in millilitres.</param>
public sealed record ConsumedTotals(
    int CaloriesKcal = 0,
    int ProteinGrams = 0,
    int CarbGrams = 0,
    int FatGrams = 0,
    int WaterMl = 0);
