using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Application.Tests.Goals;

/// <summary>
/// Unit tests for <see cref="GoalsCalculator"/>. Each test instantiates the calculator
/// directly (no DI container) and asserts against pre-computed reference values derived
/// step-by-step in the comments above each test.
/// </summary>
public sealed class GoalsCalculatorTests
{
    private readonly GoalsCalculator _sut = new();

    // ── Reference Example 1: Male, 30 y, 80 kg, 178 cm, ModeratelyActive, Lose ────
    // BMR  = 10*80 + 6.25*178 − 5*30 + 5 = 800 + 1112.5 − 150 + 5 = 1767.5 → 1768
    // TDEE = round(1767.5 * 1.55) = round(2739.625) = 2740
    // CalTarget = max(1200, 2740 − 500) = 2240
    // Fat  = round(2240*0.25) = 560 kcal budget → round(560/9) = round(62.22) = 62 g → 62*9 = 558 kcal
    // Prot = round(80*2.0) = 160 g → 640 kcal
    // Carb = 2240 − 640 − 558 = 1042 kcal → round(1042/4) = round(260.5) = 261 g
    // Water = round(80*35) = 2800 + 350 = 3150 ml
    [Fact]
    public void Compute_Male30_80kg_178cm_ModeratelyActive_Lose_ReturnsReferenceValues()
    {
        var input = new GoalsCalculatorInput(
            WeightKg: 80.0,
            HeightCm: 178.0,
            AgeYears: 30,
            BiologicalSex: BiologicalSex.Male,
            ActivityLevel: ActivityLevel.ModeratelyActive,
            PrimaryGoal: PrimaryGoal.Lose);

        var result = _sut.Compute(input);

        Assert.Equal(1768, result.Bmr);
        Assert.Equal(2740, result.Tdee);
        Assert.Equal(2240, result.CaloriesKcal);
        Assert.Equal(160, result.ProteinGrams);
        Assert.Equal(261, result.CarbohydrateGrams);
        Assert.Equal(62, result.FatGrams);
        Assert.Equal(3150, result.WaterMl);
    }

    // ── Reference Example 2: Female, 45 y, 65 kg, 163 cm, Sedentary, Maintain ────
    // BMR  = 10*65 + 6.25*163 − 5*45 − 161 = 650 + 1018.75 − 225 − 161 = 1282.75 → 1283
    // TDEE = round(1282.75 * 1.2) = round(1539.3) = 1539
    // CalTarget = max(1200, 1539 + 0) = 1539
    // Fat  = round(1539*0.25) = round(384.75) = 385 kcal budget → round(385/9) = round(42.78) = 43 g → 43*9 = 387 kcal
    // Prot = round(65*2.0) = 130 g → 520 kcal
    // Carb = 1539 − 520 − 387 = 632 kcal → round(632/4) = 158 g
    // Water = round(65*35) = 2275 + 0 = 2275 ml
    [Fact]
    public void Compute_Female45_65kg_163cm_Sedentary_Maintain_ReturnsReferenceValues()
    {
        var input = new GoalsCalculatorInput(
            WeightKg: 65.0,
            HeightCm: 163.0,
            AgeYears: 45,
            BiologicalSex: BiologicalSex.Female,
            ActivityLevel: ActivityLevel.Sedentary,
            PrimaryGoal: PrimaryGoal.Maintain);

        var result = _sut.Compute(input);

        Assert.Equal(1283, result.Bmr);
        Assert.Equal(1539, result.Tdee);
        Assert.Equal(1539, result.CaloriesKcal);
        Assert.Equal(130, result.ProteinGrams);
        Assert.Equal(158, result.CarbohydrateGrams);
        Assert.Equal(43, result.FatGrams);
        Assert.Equal(2275, result.WaterMl);
    }

    // ── Calorie floor: low-TDEE female on a Lose goal clamps to 1200 ─────────────
    // 40 kg, 155 cm, 22 y, Female, Sedentary, Lose
    // BMR  = 10*40 + 6.25*155 − 5*22 − 161 = 400 + 968.75 − 110 − 161 = 1097.75 → 1098
    // TDEE = round(1097.75 * 1.2) = round(1317.3) = 1317
    // Adjusted = 1317 − 500 = 817 → floor → 1200
    [Fact]
    public void Compute_LowTdee_LoseGoal_ClampsToCalorieFloor()
    {
        var input = new GoalsCalculatorInput(
            WeightKg: 40.0,
            HeightCm: 155.0,
            AgeYears: 22,
            BiologicalSex: BiologicalSex.Female,
            ActivityLevel: ActivityLevel.Sedentary,
            PrimaryGoal: PrimaryGoal.Lose);

        var result = _sut.Compute(input);

        Assert.Equal(1200, result.CaloriesKcal);
    }

    // ── Gain goal adds a 300 kcal surplus ────────────────────────────────────────
    // Male, 70 kg, 175 cm, 25 y, LightlyActive, Gain
    // BMR  = 10*70 + 6.25*175 − 5*25 + 5 = 700 + 1093.75 − 125 + 5 = 1673.75 → 1674
    // TDEE = round(1673.75 * 1.375) = round(2301.40625) = 2301
    // CalTarget = 2301 + 300 = 2601
    [Fact]
    public void Compute_Male_LightlyActive_Gain_AddsSurplus()
    {
        var input = new GoalsCalculatorInput(
            WeightKg: 70.0,
            HeightCm: 175.0,
            AgeYears: 25,
            BiologicalSex: BiologicalSex.Male,
            ActivityLevel: ActivityLevel.LightlyActive,
            PrimaryGoal: PrimaryGoal.Gain);

        var result = _sut.Compute(input);

        Assert.Equal(1674, result.Bmr);
        Assert.Equal(2301, result.Tdee);
        Assert.Equal(2601, result.CaloriesKcal);
    }

    // ── ExtraActive: 1.9 multiplier and the maximum 700 ml water bump ─────────────
    // Male, 80 kg, 178 cm, 30 y, ExtraActive, Maintain
    // BMR  = 1767.5 → 1768
    // TDEE = round(1767.5 * 1.9) = round(3358.25) = 3358
    // Water base = round(80*35) = 2800; bump = 700 → 3500
    [Fact]
    public void Compute_ExtraActive_UsesMaxMultiplierAndWaterBump()
    {
        var input = new GoalsCalculatorInput(
            WeightKg: 80.0,
            HeightCm: 178.0,
            AgeYears: 30,
            BiologicalSex: BiologicalSex.Male,
            ActivityLevel: ActivityLevel.ExtraActive,
            PrimaryGoal: PrimaryGoal.Maintain);

        var result = _sut.Compute(input);

        Assert.Equal(1768, result.Bmr);
        Assert.Equal(3358, result.Tdee);
        Assert.Equal(3358, result.CaloriesKcal);
        Assert.Equal(3500, result.WaterMl);
    }

    // ── VeryActive: 1.725 multiplier and 500 ml water bump ───────────────────────
    // Male, 75 kg, 170 cm, 35 y, VeryActive, Maintain
    // BMR  = 10*75 + 6.25*170 − 5*35 + 5 = 750 + 1062.5 − 175 + 5 = 1642.5 → 1643
    // TDEE = round(1642.5 * 1.725) = round(2833.3125) = 2833
    // Water base = round(75*35) = 2625; bump = 500 → 3125
    [Fact]
    public void Compute_VeryActive_UsesCorrectMultiplierAndWaterBump()
    {
        var input = new GoalsCalculatorInput(
            WeightKg: 75.0,
            HeightCm: 170.0,
            AgeYears: 35,
            BiologicalSex: BiologicalSex.Male,
            ActivityLevel: ActivityLevel.VeryActive,
            PrimaryGoal: PrimaryGoal.Maintain);

        var result = _sut.Compute(input);

        Assert.Equal(1643, result.Bmr);
        Assert.Equal(2833, result.Tdee);
        Assert.Equal(3125, result.WaterMl);
    }

    // ── Sex changes the Mifflin-St Jeor constant (+5 male vs −161 female) ─────────
    // Same biometrics, only sex differs: 70 kg, 175 cm, 30 y, Sedentary.
    // Male BMR   = 10*70 + 6.25*175 − 5*30 + 5   = 700 + 1093.75 − 150 + 5   = 1648.75 → 1649
    // Female BMR = 10*70 + 6.25*175 − 5*30 − 161 = 700 + 1093.75 − 150 − 161 = 1482.75 → 1483
    // Difference = 166 (the +5 vs −161 gap).
    [Fact]
    public void Compute_SameBiometrics_MaleVsFemale_BmrDiffersByConstant()
    {
        var male = _sut.Compute(new GoalsCalculatorInput(
            70.0, 175.0, 30, BiologicalSex.Male, ActivityLevel.Sedentary, PrimaryGoal.Maintain));
        var female = _sut.Compute(new GoalsCalculatorInput(
            70.0, 175.0, 30, BiologicalSex.Female, ActivityLevel.Sedentary, PrimaryGoal.Maintain));

        Assert.Equal(1649, male.Bmr);
        Assert.Equal(1483, female.Bmr);
        Assert.Equal(166, male.Bmr - female.Bmr);
    }

    // ── Macro grams reconcile to the calorie target within final-gram rounding ───
    // Because carbs are derived from the gram-converted (not budget) energy of protein/fat,
    // proteinGrams*4 + fatGrams*9 + carbGrams*4 must equal caloriesKcal to within the single
    // carb-gram rounding step (≤ 2 kcal).
    [Theory]
    [InlineData(80.0, 178.0, 30, BiologicalSex.Male, ActivityLevel.ModeratelyActive, PrimaryGoal.Lose)]
    [InlineData(65.0, 163.0, 45, BiologicalSex.Female, ActivityLevel.Sedentary, PrimaryGoal.Maintain)]
    [InlineData(70.0, 175.0, 25, BiologicalSex.Male, ActivityLevel.LightlyActive, PrimaryGoal.Gain)]
    [InlineData(95.0, 185.0, 28, BiologicalSex.Male, ActivityLevel.VeryActive, PrimaryGoal.Maintain)]
    public void Compute_MacroGrams_ReconcileToCalorieTarget(
        double weightKg, double heightCm, int age,
        BiologicalSex sex, ActivityLevel activity, PrimaryGoal goal)
    {
        var result = _sut.Compute(new GoalsCalculatorInput(weightKg, heightCm, age, sex, activity, goal));

        var macroKcal = (result.ProteinGrams * 4) + (result.CarbohydrateGrams * 4) + (result.FatGrams * 9);

        Assert.True(
            Math.Abs(macroKcal - result.CaloriesKcal) <= 2,
            $"Macro kcal {macroKcal} should reconcile to calorie target {result.CaloriesKcal} (±2).");
    }

    [Fact]
    public void Compute_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.Compute(null!));
    }
}
