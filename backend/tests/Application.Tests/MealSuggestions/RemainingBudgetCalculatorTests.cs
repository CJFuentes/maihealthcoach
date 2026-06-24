using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Application.MealSuggestions;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Application.Tests.MealSuggestions;

/// <summary>
/// Unit tests for <see cref="RemainingBudgetCalculator"/>: remaining budget derivation, override
/// layering, consumed-total clamping, and dietary-preference text formatting.
/// </summary>
public sealed class RemainingBudgetCalculatorTests
{
    private static GoalsCalculatorOutput Targets() =>
        new(
            Bmr: 1700,
            Tdee: 2400,
            CaloriesKcal: 2000,
            ProteinGrams: 160,
            CarbohydrateGrams: 200,
            FatGrams: 55,
            WaterMl: 3000);

    [Fact]
    public void Compute_NoConsumed_RemainingEqualsTargetsAndDailyEqualsTargets()
    {
        var result = RemainingBudgetCalculator.Compute(Targets(), overrides: null, preferences: null, mealType: null);

        Assert.Equal(2000, result.RemainingCalories);
        Assert.Equal(160, result.RemainingProteinGrams);
        Assert.Equal(200, result.RemainingCarbGrams);
        Assert.Equal(55, result.RemainingFatGrams);
        Assert.Equal(3000, result.RemainingWaterMl);

        Assert.Equal(2000, result.DailyCalorieTarget);
        Assert.Equal(160, result.DailyProteinTarget);
        Assert.Equal(200, result.DailyCarbTarget);
        Assert.Equal(55, result.DailyFatTarget);
        Assert.Equal(3000, result.DailyWaterTarget);
    }

    [Fact]
    public void Compute_WithConsumed_SubtractsCorrectly()
    {
        var consumed = new ConsumedTotals(
            CaloriesKcal: 800,
            ProteinGrams: 60,
            CarbGrams: 90,
            FatGrams: 20,
            WaterMl: 1000);

        var result = RemainingBudgetCalculator.Compute(Targets(), overrides: null, preferences: null, mealType: null, consumed);

        Assert.Equal(1200, result.RemainingCalories);
        Assert.Equal(100, result.RemainingProteinGrams);
        Assert.Equal(110, result.RemainingCarbGrams);
        Assert.Equal(35, result.RemainingFatGrams);
        Assert.Equal(2000, result.RemainingWaterMl);
    }

    [Fact]
    public void Compute_ConsumedExceedsTarget_ClampsAtZero()
    {
        var consumed = new ConsumedTotals(
            CaloriesKcal: 5000,
            ProteinGrams: 500,
            CarbGrams: 500,
            FatGrams: 500,
            WaterMl: 9000);

        var result = RemainingBudgetCalculator.Compute(Targets(), overrides: null, preferences: null, mealType: null, consumed);

        Assert.Equal(0, result.RemainingCalories);
        Assert.Equal(0, result.RemainingProteinGrams);
        Assert.Equal(0, result.RemainingCarbGrams);
        Assert.Equal(0, result.RemainingFatGrams);
        Assert.Equal(0, result.RemainingWaterMl);
    }

    [Fact]
    public void Compute_WithOverrides_UsesOverriddenValues()
    {
        var overrides = UserGoalTargets.Create(Guid.NewGuid());
        overrides.SetOverrides(
            caloriesKcal: 1800,
            proteinGrams: 140,
            carbohydrateGrams: 180,
            fatGrams: 50,
            waterMl: 2500);

        var result = RemainingBudgetCalculator.Compute(Targets(), overrides, preferences: null, mealType: null);

        Assert.Equal(1800, result.DailyCalorieTarget);
        Assert.Equal(140, result.DailyProteinTarget);
        Assert.Equal(180, result.DailyCarbTarget);
        Assert.Equal(50, result.DailyFatTarget);
        Assert.Equal(2500, result.DailyWaterTarget);

        Assert.Equal(1800, result.RemainingCalories);
        Assert.Equal(140, result.RemainingProteinGrams);
    }

    [Fact]
    public void Compute_VeganWithAllergies_DietaryTextContainsBoth()
    {
        var preferences = DietaryPreferences.Create(DietType.Vegan, "peanuts");

        var result = RemainingBudgetCalculator.Compute(Targets(), overrides: null, preferences, mealType: null);

        Assert.NotNull(result.DietaryPreferencesText);
        Assert.Contains("Vegan", result.DietaryPreferencesText!);
        Assert.Contains("peanuts", result.DietaryPreferencesText!);
    }

    [Fact]
    public void Compute_NullPreferences_DietaryTextNull()
    {
        var result = RemainingBudgetCalculator.Compute(Targets(), overrides: null, preferences: null, mealType: null);

        Assert.Null(result.DietaryPreferencesText);
    }

    [Fact]
    public void Compute_NoneDietTypeAndEmptyAllergies_DietaryTextNull()
    {
        var preferences = DietaryPreferences.Create(DietType.None, string.Empty);

        var result = RemainingBudgetCalculator.Compute(Targets(), overrides: null, preferences, mealType: null);

        Assert.Null(result.DietaryPreferencesText);
    }
}
