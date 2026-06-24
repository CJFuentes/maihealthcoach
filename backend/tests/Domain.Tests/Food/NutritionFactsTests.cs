using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Domain.Tests.Food;

/// <summary>
/// Pure-domain tests for <see cref="NutritionFacts"/> — no EF, no database. These cover the
/// per-100 g validation invariants (non-negative macros/micros) and the proportional scaling
/// behaviour, including null-propagation for unknown micros, that callers rely on to derive
/// portion nutrition.
/// </summary>
public sealed class NutritionFactsTests
{
    [Fact]
    public void Create_WithValidValues_SetsAllFields()
    {
        var facts = NutritionFacts.Create(
            energyKcal: 52m,
            proteinG: 0.3m,
            carbohydrateG: 14m,
            fatG: 0.2m,
            sugarsG: 10m,
            fiberG: 2.4m,
            saturatedFatG: 0.05m,
            sodiumMg: 1m);

        Assert.Equal(52m, facts.EnergyKcal);
        Assert.Equal(0.3m, facts.ProteinG);
        Assert.Equal(14m, facts.CarbohydrateG);
        Assert.Equal(0.2m, facts.FatG);
        Assert.Equal(10m, facts.SugarsG);
        Assert.Equal(2.4m, facts.FiberG);
        Assert.Equal(0.05m, facts.SaturatedFatG);
        Assert.Equal(1m, facts.SodiumMg);
    }

    [Fact]
    public void Create_WithoutMicros_LeavesMicrosNull()
    {
        var facts = NutritionFacts.Create(energyKcal: 0m, proteinG: 0m, carbohydrateG: 0m, fatG: 0m);

        Assert.Null(facts.SugarsG);
        Assert.Null(facts.FiberG);
        Assert.Null(facts.SaturatedFatG);
        Assert.Null(facts.SodiumMg);
    }

    [Fact]
    public void Create_WithNegativeEnergy_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionFacts.Create(energyKcal: -1m, proteinG: 0m, carbohydrateG: 0m, fatG: 0m));
    }

    [Fact]
    public void Create_WithNegativeProtein_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionFacts.Create(energyKcal: 0m, proteinG: -1m, carbohydrateG: 0m, fatG: 0m));
    }

    [Fact]
    public void Create_WithNegativeCarbohydrate_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionFacts.Create(energyKcal: 0m, proteinG: 0m, carbohydrateG: -1m, fatG: 0m));
    }

    [Fact]
    public void Create_WithNegativeFat_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionFacts.Create(energyKcal: 0m, proteinG: 0m, carbohydrateG: 0m, fatG: -1m));
    }

    [Fact]
    public void Create_WithNegativeOptionalMicro_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => NutritionFacts.Create(
                energyKcal: 0m, proteinG: 0m, carbohydrateG: 0m, fatG: 0m, sodiumMg: -1m));
    }

    [Fact]
    public void ScaleToGrams_By250_MultipliesMacrosBy2Point5()
    {
        var per100g = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m);

        var scaled = per100g.ScaleToGrams(250m);

        Assert.Equal(130m, scaled.EnergyKcal);
        Assert.Equal(0.75m, scaled.ProteinG);
        Assert.Equal(35m, scaled.CarbohydrateG);
        Assert.Equal(0.5m, scaled.FatG);
    }

    [Fact]
    public void ScaleToGrams_By250_ScalesNonNullMicros()
    {
        var per100g = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0m, carbohydrateG: 14m, fatG: 0m,
            sugarsG: 10m, sodiumMg: 4m);

        var scaled = per100g.ScaleToGrams(250m);

        Assert.Equal(25m, scaled.SugarsG);
        Assert.Equal(10m, scaled.SodiumMg);
    }

    [Fact]
    public void ScaleToGrams_KeepsNullMicrosNull()
    {
        var per100g = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m);

        var scaled = per100g.ScaleToGrams(250m);

        Assert.Null(scaled.SugarsG);
        Assert.Null(scaled.FiberG);
        Assert.Null(scaled.SaturatedFatG);
        Assert.Null(scaled.SodiumMg);
    }

    [Fact]
    public void ScaleToGrams_ByZero_YieldsAllZeroMacros()
    {
        var per100g = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m,
            sugarsG: 10m);

        var scaled = per100g.ScaleToGrams(0m);

        Assert.Equal(0m, scaled.EnergyKcal);
        Assert.Equal(0m, scaled.ProteinG);
        Assert.Equal(0m, scaled.CarbohydrateG);
        Assert.Equal(0m, scaled.FatG);
        Assert.Equal(0m, scaled.SugarsG);
    }

    [Fact]
    public void ScaleToGrams_DoesNotMutateOriginal()
    {
        var per100g = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m);

        _ = per100g.ScaleToGrams(250m);

        Assert.Equal(52m, per100g.EnergyKcal);
        Assert.Equal(14m, per100g.CarbohydrateG);
    }

    [Fact]
    public void ScaleToGrams_WithNegativeGrams_Throws()
    {
        var per100g = NutritionFacts.Create(
            energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m);

        Assert.Throws<ArgumentOutOfRangeException>(() => per100g.ScaleToGrams(-1m));
    }
}
