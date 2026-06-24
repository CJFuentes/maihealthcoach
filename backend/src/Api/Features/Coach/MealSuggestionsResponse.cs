namespace MAIHealthCoach.Api.Features.Coach;

/// <summary>
/// A single suggested meal or snack in the <c>GET /api/v1/me/coach/meal-suggestions</c> response.
/// Macro fields are nullable because the model may omit them, or the option may be a freeform
/// fallback when the reply could not be parsed as structured data.
/// </summary>
/// <param name="Name">The dish name.</param>
/// <param name="Calories">Approximate calories in kcal, or <see langword="null"/> if unknown.</param>
/// <param name="ProteinGrams">Approximate protein in grams, or <see langword="null"/> if unknown.</param>
/// <param name="CarbGrams">Approximate carbohydrates in grams, or <see langword="null"/> if unknown.</param>
/// <param name="FatGrams">Approximate fat in grams, or <see langword="null"/> if unknown.</param>
/// <param name="Rationale">A short rationale explaining why the option fits the budget and preferences.</param>
public sealed record MealSuggestionOptionResponse(
    string Name,
    int? Calories,
    int? ProteinGrams,
    int? CarbGrams,
    int? FatGrams,
    string Rationale);

/// <summary>
/// Response body for <c>GET /api/v1/me/coach/meal-suggestions</c> (issue #37). Carries the parsed
/// meal options along with the remaining nutrition budget the suggestions were computed against and
/// a client-facing safety disclaimer.
/// </summary>
/// <param name="Options">The suggested meal options. Always at least one item on success.</param>
/// <param name="RemainingCalories">Remaining calorie budget for the rest of today, in kcal.</param>
/// <param name="RemainingProteinGrams">Remaining protein budget, in grams.</param>
/// <param name="RemainingCarbGrams">Remaining carbohydrate budget, in grams.</param>
/// <param name="RemainingFatGrams">Remaining fat budget, in grams.</param>
/// <param name="Disclaimer">Client-facing safety disclaimer to surface beneath the suggestions.</param>
public sealed record MealSuggestionsResponse(
    IReadOnlyList<MealSuggestionOptionResponse> Options,
    int RemainingCalories,
    int RemainingProteinGrams,
    int RemainingCarbGrams,
    int RemainingFatGrams,
    string? Disclaimer);
