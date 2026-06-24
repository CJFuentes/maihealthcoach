using System.Text;
using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// Default <see cref="IMealSuggestionService"/> implementation. Composes a coaching prompt from the
/// remaining-budget request, delegates to <see cref="ICoachService"/>, and parses the reply via
/// <see cref="MealSuggestionParser"/>. Internal because its DI registration lives in the same
/// assembly; the interface and supporting types are public for cross-assembly testing.
/// </summary>
internal sealed class MealSuggestionService : IMealSuggestionService
{
    private readonly ICoachService _coachService;

    public MealSuggestionService(ICoachService coachService)
    {
        _coachService = coachService;
    }

    /// <inheritdoc />
    public async Task<MealSuggestionResult> SuggestAsync(MealSuggestionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userMessage = BuildUserMessage(request);

        var context = new CoachingContext(
            PrimaryGoal: null,
            DailyCalorieTarget: request.DailyCalorieTarget,
            DailyProteinTargetGrams: request.DailyProteinTarget,
            TodayCaloriesConsumed: Math.Max(0, request.DailyCalorieTarget - request.RemainingCalories),
            TodayProteinConsumedGrams: Math.Max(0, request.DailyProteinTarget - request.RemainingProteinGrams),
            DietaryPreferences: request.DietaryPreferencesText,
            ActivityLevel: null,
            DailyCarbohydrateTargetGrams: request.DailyCarbTarget,
            DailyFatTargetGrams: request.DailyFatTarget,
            DailyWaterTargetMl: request.DailyWaterTarget,
            TodayCarbsConsumedGrams: Math.Max(0, request.DailyCarbTarget - request.RemainingCarbGrams),
            TodayFatConsumedGrams: Math.Max(0, request.DailyFatTarget - request.RemainingFatGrams),
            TodayWaterConsumedMl: Math.Max(0, request.DailyWaterTarget - request.RemainingWaterMl));

        var coachRequest = new CoachRequest(userMessage, context, CoachModelTier.Default);

        var coachResult = await _coachService.AskAsync(coachRequest, cancellationToken);

        if (!coachResult.IsSuccess)
        {
            return MealSuggestionResult.Failure(
                coachResult.ErrorCategory,
                coachResult.FallbackMessage ?? "Meal suggestions are unavailable right now.",
                request.RemainingCalories,
                request.RemainingProteinGrams,
                request.RemainingCarbGrams,
                request.RemainingFatGrams);
        }

        var options = MealSuggestionParser.Parse(coachResult.ReplyText!);

        return MealSuggestionResult.Success(
            options,
            coachResult.Disclaimer,
            request.RemainingCalories,
            request.RemainingProteinGrams,
            request.RemainingCarbGrams,
            request.RemainingFatGrams);
    }

    // Builds the user-turn message. Dietary restrictions are surfaced as hard constraints up front;
    // the model is then asked for exactly three options in a strict JSON array shape so the reply
    // can be parsed deterministically by MealSuggestionParser.
    private static string BuildUserMessage(MealSuggestionRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("I need meal suggestions that fit my remaining nutrition budget for the day.");

        if (!string.IsNullOrWhiteSpace(request.MealType))
        {
            sb.AppendLine($"I am looking for {request.MealType} ideas.");
        }

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.DietaryPreferencesText))
        {
            sb.AppendLine("IMPORTANT HARD CONSTRAINTS — you must not suggest anything that violates these:");
            sb.AppendLine($"- Dietary restrictions and allergies: {request.DietaryPreferencesText}. These are ABSOLUTE constraints. Never include any ingredient, dish, or preparation that conflicts with them, even as an example.");
            sb.AppendLine();
        }

        sb.AppendLine("Remaining budget for the rest of today:");
        sb.AppendLine($"- Calories: {request.RemainingCalories} kcal");
        sb.AppendLine($"- Protein: {request.RemainingProteinGrams} g");
        sb.AppendLine($"- Carbohydrates: {request.RemainingCarbGrams} g");
        sb.AppendLine($"- Fat: {request.RemainingFatGrams} g");
        sb.AppendLine();

        sb.AppendLine("Please suggest 3 specific meal or snack ideas that fit within this remaining budget. For each option, provide:");
        sb.AppendLine("- A specific name (e.g. \"Greek yogurt parfait with berries\")");
        sb.AppendLine("- Approximate macros: calories (kcal), protein (g), carbohydrates (g), fat (g)");
        sb.AppendLine("- A one-sentence rationale explaining why it fits the budget and preferences");
        sb.AppendLine();

        sb.AppendLine("Respond ONLY with a JSON array in this exact format (no markdown code fences, no extra text before or after the array):");
        sb.AppendLine("[");
        sb.AppendLine("  {\"name\": \"...\", \"calories\": 0, \"proteinGrams\": 0, \"carbGrams\": 0, \"fatGrams\": 0, \"rationale\": \"...\"}");
        sb.AppendLine("]");

        return sb.ToString();
    }
}
