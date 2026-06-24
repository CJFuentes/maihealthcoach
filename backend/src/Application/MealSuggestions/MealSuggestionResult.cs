using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// The outcome of a meal-suggestion request. On success, <see cref="Options"/> carries the parsed
/// meal ideas and <see cref="Disclaimer"/> the client-facing safety disclaimer. On failure,
/// <see cref="FallbackMessage"/> carries a user-friendly message and <see cref="ErrorCategory"/>
/// the underlying coaching failure category. The remaining-budget figures are echoed on both paths
/// so the API can surface them regardless of outcome.
/// </summary>
/// <param name="IsSuccess"><see langword="true"/> when suggestions were produced successfully.</param>
/// <param name="Options">The parsed meal options; empty on failure.</param>
/// <param name="Disclaimer">Client-facing safety disclaimer on success; <see langword="null"/> on failure.</param>
/// <param name="FallbackMessage">User-friendly fallback message on failure; <see langword="null"/> on success.</param>
/// <param name="ErrorCategory">The coaching failure category; <see cref="CoachErrorCategory.None"/> on success.</param>
/// <param name="RemainingCalories">Remaining calorie budget for the rest of today, in kcal.</param>
/// <param name="RemainingProteinGrams">Remaining protein budget, in grams.</param>
/// <param name="RemainingCarbGrams">Remaining carbohydrate budget, in grams.</param>
/// <param name="RemainingFatGrams">Remaining fat budget, in grams.</param>
public sealed record MealSuggestionResult(
    bool IsSuccess,
    IReadOnlyList<MealOption> Options,
    string? Disclaimer,
    string? FallbackMessage,
    CoachErrorCategory ErrorCategory,
    int RemainingCalories,
    int RemainingProteinGrams,
    int RemainingCarbGrams,
    int RemainingFatGrams)
{
    /// <summary>Creates a successful result carrying the parsed options and remaining budget.</summary>
    public static MealSuggestionResult Success(
        IReadOnlyList<MealOption> options,
        string? disclaimer,
        int remainingCalories,
        int remainingProtein,
        int remainingCarbs,
        int remainingFat) =>
        new(true, options, disclaimer, null, CoachErrorCategory.None, remainingCalories, remainingProtein, remainingCarbs, remainingFat);

    /// <summary>Creates a failure result carrying the fallback message, error category, and remaining budget.</summary>
    public static MealSuggestionResult Failure(
        CoachErrorCategory category,
        string fallbackMessage,
        int remainingCalories,
        int remainingProtein,
        int remainingCarbs,
        int remainingFat) =>
        new(false, [], null, fallbackMessage, category, remainingCalories, remainingProtein, remainingCarbs, remainingFat);
}
