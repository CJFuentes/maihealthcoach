namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// A single suggested meal or snack. Macro fields are nullable because the model may omit them,
/// or the suggestion may be a freeform fallback when the reply could not be parsed as structured data.
/// </summary>
/// <param name="Name">The dish name (e.g. "Greek yogurt parfait with berries").</param>
/// <param name="Calories">Approximate calories, in kcal, or <see langword="null"/> if unknown.</param>
/// <param name="ProteinGrams">Approximate protein, in grams, or <see langword="null"/> if unknown.</param>
/// <param name="CarbGrams">Approximate carbohydrates, in grams, or <see langword="null"/> if unknown.</param>
/// <param name="FatGrams">Approximate fat, in grams, or <see langword="null"/> if unknown.</param>
/// <param name="Rationale">A short rationale explaining why the option fits the budget and preferences.</param>
public sealed record MealOption(
    string Name,
    int? Calories,
    int? ProteinGrams,
    int? CarbGrams,
    int? FatGrams,
    string Rationale);
