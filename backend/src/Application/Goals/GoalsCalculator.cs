using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Application.Goals;

/// <summary>
/// Computes personalized daily nutrition and hydration targets from a user's biometric
/// profile. The service is stateless and has no external dependencies, so it can be
/// unit-tested by constructing it directly.
/// </summary>
/// <remarks>
/// <para><strong>BMR — Mifflin-St Jeor (1990)</strong></para>
/// <para>Male:   BMR = 10 × kg + 6.25 × cm − 5 × age + 5</para>
/// <para>Female: BMR = 10 × kg + 6.25 × cm − 5 × age − 161</para>
///
/// <para><strong>TDEE — activity multipliers</strong></para>
/// <list type="table">
///   <item><term>Sedentary</term><description>1.200</description></item>
///   <item><term>LightlyActive</term><description>1.375</description></item>
///   <item><term>ModeratelyActive</term><description>1.550</description></item>
///   <item><term>VeryActive</term><description>1.725</description></item>
///   <item><term>ExtraActive</term><description>1.900</description></item>
/// </list>
/// <para>TDEE multiplies the <em>unrounded</em> BMR before rounding, so the reported BMR and
/// TDEE are each rounded once from the precise intermediate value.</para>
///
/// <para><strong>Calorie target adjustment by goal</strong></para>
/// <list type="table">
///   <item><term>Lose</term><description>TDEE − 500 kcal/day</description></item>
///   <item><term>Maintain</term><description>TDEE (no adjustment)</description></item>
///   <item><term>Gain</term><description>TDEE + 300 kcal/day (lean-bulk surplus)</description></item>
/// </list>
/// <para>Calorie floor: 1200 kcal/day, applied <em>after</em> the goal adjustment, as a safe
/// nutritional minimum.</para>
///
/// <para><strong>Macro split</strong></para>
/// <para>Protein: 2.0 g per kg body weight. Fat: 25 % of the calorie target. Carbohydrates:
/// the remaining kcal. Energy density: protein 4 kcal/g, carbohydrate 4 kcal/g, fat 9 kcal/g.
/// Carbohydrates are derived last from the remaining kcal to avoid compound rounding drift.</para>
///
/// <para><strong>Water target</strong></para>
/// <para>Base: 35 ml per kg body weight, plus an activity bump — Sedentary +0, LightlyActive
/// +150, ModeratelyActive +350, VeryActive +500, ExtraActive +700 ml.</para>
/// </remarks>
public sealed class GoalsCalculator
{
    // ── Activity multipliers ─────────────────────────────────────────────────────
    private const double SedentaryMultiplier = 1.200;
    private const double LightlyActiveMultiplier = 1.375;
    private const double ModeratelyActiveMultiplier = 1.550;
    private const double VeryActiveMultiplier = 1.725;
    private const double ExtraActiveMultiplier = 1.900;

    // ── Calorie goal adjustments ──────────────────────────────────────────────────
    private const int LoseAdjustmentKcal = -500;
    private const int MaintainAdjustmentKcal = 0;
    private const int GainAdjustmentKcal = 300;
    private const int CalorieFloorKcal = 1200;

    // ── Macro constants ───────────────────────────────────────────────────────────
    private const double ProteinGramsPerKg = 2.0;
    private const double FatFractionOfKcal = 0.25;
    private const int ProteinKcalPerGram = 4;
    private const double CarbKcalPerGram = 4.0;
    private const double FatKcalPerGram = 9.0;

    // ── Water constants ───────────────────────────────────────────────────────────
    private const double WaterMlPerKg = 35.0;
    private const int SedentaryWaterBumpMl = 0;
    private const int LightlyActiveWaterBumpMl = 150;
    private const int ModeratelyActiveWaterBumpMl = 350;
    private const int VeryActiveWaterBumpMl = 500;
    private const int ExtraActiveWaterBumpMl = 700;

    /// <summary>
    /// Computes daily nutrition and hydration targets from the supplied biometric snapshot.
    /// </summary>
    /// <param name="input">Validated, complete biometric data.</param>
    /// <returns>A <see cref="GoalsCalculatorOutput"/> with all computed targets.</returns>
    public GoalsCalculatorOutput Compute(GoalsCalculatorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // ── BMR (Mifflin-St Jeor) ──────────────────────────────────────────────
        var rawBmr = input.BiologicalSex == BiologicalSex.Male
            ? (10.0 * input.WeightKg) + (6.25 * input.HeightCm) - (5.0 * input.AgeYears) + 5.0
            : (10.0 * input.WeightKg) + (6.25 * input.HeightCm) - (5.0 * input.AgeYears) - 161.0;

        var bmr = (int)Math.Round(rawBmr, MidpointRounding.AwayFromZero);

        // ── TDEE ───────────────────────────────────────────────────────────────
        var activityMultiplier = input.ActivityLevel switch
        {
            ActivityLevel.Sedentary => SedentaryMultiplier,
            ActivityLevel.LightlyActive => LightlyActiveMultiplier,
            ActivityLevel.ModeratelyActive => ModeratelyActiveMultiplier,
            ActivityLevel.VeryActive => VeryActiveMultiplier,
            ActivityLevel.ExtraActive => ExtraActiveMultiplier,
            _ => throw new ArgumentOutOfRangeException(
                     nameof(input), input.ActivityLevel, "Unknown ActivityLevel."),
        };

        var rawTdee = rawBmr * activityMultiplier;
        var tdee = (int)Math.Round(rawTdee, MidpointRounding.AwayFromZero);

        // ── Calorie target ─────────────────────────────────────────────────────
        var goalAdjustment = input.PrimaryGoal switch
        {
            PrimaryGoal.Lose => LoseAdjustmentKcal,
            PrimaryGoal.Maintain => MaintainAdjustmentKcal,
            PrimaryGoal.Gain => GainAdjustmentKcal,
            _ => throw new ArgumentOutOfRangeException(
                     nameof(input), input.PrimaryGoal, "Unknown PrimaryGoal."),
        };

        var caloriesKcal = Math.Max(CalorieFloorKcal, tdee + goalAdjustment);

        // ── Macros (fat by %, protein by g/kg, carbs as the remainder) ─────────
        // Carbs are derived from the *gram-converted* energy of protein and fat (not the
        // raw % budget) so the three macro grams reconcile back to the calorie target:
        // proteinGrams*4 + fatGrams*9 + carbGrams*4 ≈ caloriesKcal (within final-gram rounding).
        var fatKcalBudget = (int)Math.Round(caloriesKcal * FatFractionOfKcal, MidpointRounding.AwayFromZero);
        var fatGrams = (int)Math.Round(fatKcalBudget / FatKcalPerGram, MidpointRounding.AwayFromZero);
        var fatKcal = fatGrams * (int)FatKcalPerGram;

        var proteinGrams = (int)Math.Round(input.WeightKg * ProteinGramsPerKg, MidpointRounding.AwayFromZero);
        var proteinKcal = proteinGrams * ProteinKcalPerGram;

        var carbKcal = caloriesKcal - proteinKcal - fatKcal;
        var carbGrams = carbKcal > 0
            ? (int)Math.Round(carbKcal / CarbKcalPerGram, MidpointRounding.AwayFromZero)
            : 0;

        // ── Water ──────────────────────────────────────────────────────────────
        var waterBase = (int)Math.Round(input.WeightKg * WaterMlPerKg, MidpointRounding.AwayFromZero);
        var activityWaterBump = input.ActivityLevel switch
        {
            ActivityLevel.Sedentary => SedentaryWaterBumpMl,
            ActivityLevel.LightlyActive => LightlyActiveWaterBumpMl,
            ActivityLevel.ModeratelyActive => ModeratelyActiveWaterBumpMl,
            ActivityLevel.VeryActive => VeryActiveWaterBumpMl,
            ActivityLevel.ExtraActive => ExtraActiveWaterBumpMl,
            _ => throw new ArgumentOutOfRangeException(
                     nameof(input), input.ActivityLevel, "Unknown ActivityLevel for water bump."),
        };
        var waterMl = waterBase + activityWaterBump;

        return new GoalsCalculatorOutput(
            Bmr: bmr,
            Tdee: tdee,
            CaloriesKcal: caloriesKcal,
            ProteinGrams: proteinGrams,
            CarbohydrateGrams: carbGrams,
            FatGrams: fatGrams,
            WaterMl: waterMl
        );
    }
}
