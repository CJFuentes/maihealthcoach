namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// The fully-resolved input to <see cref="IMealSuggestionService"/>: the remaining nutrition
/// budget for the rest of today, the user's dietary constraints, an optional meal type, and the
/// underlying daily targets (carried so the coaching context can report consumed-so-far figures).
/// </summary>
/// <param name="RemainingCalories">Remaining calorie budget for the rest of today, in kcal (clamped at 0).</param>
/// <param name="RemainingProteinGrams">Remaining protein budget, in grams (clamped at 0).</param>
/// <param name="RemainingCarbGrams">Remaining carbohydrate budget, in grams (clamped at 0).</param>
/// <param name="RemainingFatGrams">Remaining fat budget, in grams (clamped at 0).</param>
/// <param name="RemainingWaterMl">Remaining water budget, in millilitres (clamped at 0).</param>
/// <param name="DietaryPreferencesText">Free-text dietary restrictions and allergies, or <see langword="null"/> when none.</param>
/// <param name="MealType">Optional meal type the user is asking for (e.g. "Dinner"), or <see langword="null"/>.</param>
/// <param name="DailyCalorieTarget">The effective daily calorie target, in kcal.</param>
/// <param name="DailyProteinTarget">The effective daily protein target, in grams.</param>
/// <param name="DailyCarbTarget">The effective daily carbohydrate target, in grams.</param>
/// <param name="DailyFatTarget">The effective daily fat target, in grams.</param>
/// <param name="DailyWaterTarget">The effective daily water target, in millilitres.</param>
public sealed record MealSuggestionRequest(
    int RemainingCalories,
    int RemainingProteinGrams,
    int RemainingCarbGrams,
    int RemainingFatGrams,
    int RemainingWaterMl,
    string? DietaryPreferencesText,
    string? MealType,
    int DailyCalorieTarget,
    int DailyProteinTarget,
    int DailyCarbTarget,
    int DailyFatTarget,
    int DailyWaterTarget);
