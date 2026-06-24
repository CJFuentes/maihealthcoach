using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// Builds a <see cref="MealSuggestionRequest"/> from computed daily targets, optional manual
/// overrides, dietary preferences, and an optional snapshot of today's consumed totals. The
/// effective daily target for each nutrient is the override when present, otherwise the computed
/// value; the remaining budget is that effective target minus what has been consumed, clamped at 0.
/// </summary>
public static class RemainingBudgetCalculator
{
    /// <summary>
    /// Computes the remaining nutrition budget for the rest of the day.
    /// </summary>
    /// <param name="targets">Computed daily targets from the goals calculator. Required.</param>
    /// <param name="overrides">Manual goal overrides, or <see langword="null"/> when none are set.</param>
    /// <param name="preferences">Dietary preferences, or <see langword="null"/> when none are recorded.</param>
    /// <param name="mealType">Optional meal type the user is asking for (e.g. "Dinner").</param>
    /// <param name="consumed">Optional snapshot of today's consumed totals; defaults to nothing consumed.</param>
    /// <returns>A fully-resolved <see cref="MealSuggestionRequest"/>.</returns>
    public static MealSuggestionRequest Compute(
        GoalsCalculatorOutput targets,
        UserGoalTargets? overrides,
        DietaryPreferences? preferences,
        string? mealType,
        ConsumedTotals? consumed = null)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var effectiveCalories = overrides?.CaloriesKcal ?? targets.CaloriesKcal;
        var effectiveProtein = overrides?.ProteinGrams ?? targets.ProteinGrams;
        var effectiveCarbs = overrides?.CarbohydrateGrams ?? targets.CarbohydrateGrams;
        var effectiveFat = overrides?.FatGrams ?? targets.FatGrams;
        var effectiveWater = overrides?.WaterMl ?? targets.WaterMl;

        consumed ??= new ConsumedTotals();

        return new MealSuggestionRequest(
            RemainingCalories: Math.Max(0, effectiveCalories - consumed.CaloriesKcal),
            RemainingProteinGrams: Math.Max(0, effectiveProtein - consumed.ProteinGrams),
            RemainingCarbGrams: Math.Max(0, effectiveCarbs - consumed.CarbGrams),
            RemainingFatGrams: Math.Max(0, effectiveFat - consumed.FatGrams),
            RemainingWaterMl: Math.Max(0, effectiveWater - consumed.WaterMl),
            DietaryPreferencesText: FormatDietaryText(preferences),
            MealType: mealType,
            DailyCalorieTarget: effectiveCalories,
            DailyProteinTarget: effectiveProtein,
            DailyCarbTarget: effectiveCarbs,
            DailyFatTarget: effectiveFat,
            DailyWaterTarget: effectiveWater);
    }

    // Collapses the structured dietary preferences into a single free-text constraint string, or
    // null when nothing meaningful is recorded (DietType.None and no allergies).
    private static string? FormatDietaryText(DietaryPreferences? p)
    {
        if (p is null)
        {
            return null;
        }

        var parts = new List<string>();

        if (p.DietType.HasValue && p.DietType.Value != DietType.None)
        {
            parts.Add(p.DietType.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(p.Allergies))
        {
            parts.Add($"allergies: {p.Allergies}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
