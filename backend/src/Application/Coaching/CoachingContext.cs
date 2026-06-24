namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Optional user-context payload attached to a coaching request. All properties are
/// nullable; absent values are simply omitted from the prompt context sections.
/// Issues #37 (meal suggestions), #38 (nudges), and #39 (chat history) will populate
/// specific fields as they land.
/// </summary>
/// <param name="PrimaryGoal">
/// The user's stated primary goal (e.g. "Lose weight", "Maintain", "Gain muscle").
/// Derived from <c>Domain.UserProfiles.PrimaryGoal</c> by the caller.
/// </param>
/// <param name="DailyCalorieTarget">Daily calorie target in kcal, if known.</param>
/// <param name="DailyProteinTargetGrams">Daily protein target in grams, if known.</param>
/// <param name="TodayCaloriesConsumed">
/// Calories consumed today so far, in kcal. Populated by #37 (meal tracking).
/// </param>
/// <param name="TodayProteinConsumedGrams">Protein consumed today in grams. Populated by #37.</param>
/// <param name="DietaryPreferences">
/// Free-text summary of dietary preferences and restrictions (e.g. "Vegan, tree-nut allergy").
/// </param>
/// <param name="ActivityLevel">
/// The user's activity level description (e.g. "Moderately active"). Used for context.
/// </param>
/// <param name="DailyCarbohydrateTargetGrams">Daily carbohydrate target in grams, if known. Populated by #37.</param>
/// <param name="DailyFatTargetGrams">Daily fat target in grams, if known. Populated by #37.</param>
/// <param name="DailyWaterTargetMl">Daily water target in millilitres, if known. Populated by #37.</param>
/// <param name="TodayCarbsConsumedGrams">Carbohydrates consumed today in grams. Populated by #37.</param>
/// <param name="TodayFatConsumedGrams">Fat consumed today in grams. Populated by #37.</param>
/// <param name="TodayWaterConsumedMl">Water consumed today in millilitres. Populated by #37.</param>
public sealed record CoachingContext(
    string? PrimaryGoal = null,
    int? DailyCalorieTarget = null,
    int? DailyProteinTargetGrams = null,
    int? TodayCaloriesConsumed = null,
    int? TodayProteinConsumedGrams = null,
    string? DietaryPreferences = null,
    string? ActivityLevel = null,
    int? DailyCarbohydrateTargetGrams = null,
    int? DailyFatTargetGrams = null,
    int? DailyWaterTargetMl = null,
    int? TodayCarbsConsumedGrams = null,
    int? TodayFatConsumedGrams = null,
    int? TodayWaterConsumedMl = null)
{
    /// <summary>An empty context with no fields populated. Use when no context is available.</summary>
    public static readonly CoachingContext Empty = new();
}
