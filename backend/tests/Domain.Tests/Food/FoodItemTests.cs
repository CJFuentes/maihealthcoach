using System.Linq;
using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Domain.Tests.Food;

/// <summary>
/// Pure-domain tests for the <see cref="FoodItem"/> aggregate root — no EF, no database. These
/// guard the creation invariants (canonical 100 g serving, required name and nutrition) and the
/// single-default-serving invariant maintained by <see cref="FoodItem.AddServingSize"/>.
/// </summary>
public sealed class FoodItemTests
{
    private static NutritionFacts SampleNutrition() =>
        NutritionFacts.Create(energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m);

    [Fact]
    public void Create_SeedsExactlyOneCanonicalHundredGramServing()
    {
        var food = FoodItem.Create("Apple", FoodSource.Custom, SampleNutrition());

        var serving = Assert.Single(food.ServingSizes);
        Assert.Equal("100 g", serving.Label);
        Assert.Equal(100m, serving.GramsEquivalent);
        Assert.Equal("g", serving.Unit);
        Assert.False(serving.IsDefault);
    }

    [Fact]
    public void Create_SetsCoreFields()
    {
        var nutrition = SampleNutrition();

        var food = FoodItem.Create(
            "Greek Yogurt",
            FoodSource.OpenFoodFacts,
            nutrition,
            brand: "Fage",
            barcode: "5201054000000",
            sourceReference: "off-12345",
            lastSyncedAt: DateTimeOffset.UtcNow);

        Assert.Equal("Greek Yogurt", food.Name);
        Assert.Equal(FoodSource.OpenFoodFacts, food.Source);
        Assert.Same(nutrition, food.NutritionPer100g);
        Assert.Equal("Fage", food.Brand);
        Assert.Equal("5201054000000", food.Barcode);
        Assert.Equal("off-12345", food.SourceReference);
        Assert.NotNull(food.LastSyncedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(
            () => FoodItem.Create(name, FoodSource.Custom, SampleNutrition()));
    }

    [Fact]
    public void Create_WithNullNutrition_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => FoodItem.Create("Apple", FoodSource.Custom, null!));
    }

    [Fact]
    public void AddServingSize_AppendsToServingCollection()
    {
        var food = FoodItem.Create("Apple", FoodSource.Custom, SampleNutrition());

        food.AddServingSize("1 medium", quantity: 1m, unit: "fruit", gramsEquivalent: 182m);

        Assert.Equal(2, food.ServingSizes.Count);
        Assert.Contains(food.ServingSizes, s => s.Label == "1 medium" && s.GramsEquivalent == 182m);
    }

    [Fact]
    public void AddServingSize_SupportsMultiplePortions()
    {
        var food = FoodItem.Create("Apple", FoodSource.Custom, SampleNutrition());

        food.AddServingSize("1 small", quantity: 1m, unit: "fruit", gramsEquivalent: 149m);
        food.AddServingSize("1 medium", quantity: 1m, unit: "fruit", gramsEquivalent: 182m);
        food.AddServingSize("1 large", quantity: 1m, unit: "fruit", gramsEquivalent: 223m);

        // 3 added + 1 canonical seeded serving.
        Assert.Equal(4, food.ServingSizes.Count);
    }

    [Fact]
    public void AddServingSize_WithIsDefault_MarksTheNewServingDefault()
    {
        var food = FoodItem.Create("Apple", FoodSource.Custom, SampleNutrition());

        var added = food.AddServingSize(
            "1 medium", quantity: 1m, unit: "fruit", gramsEquivalent: 182m, isDefault: true);

        Assert.True(added.IsDefault);
        Assert.Single(food.ServingSizes, s => s.IsDefault);
    }

    [Fact]
    public void AddServingSize_WithIsDefault_DemotesTheCanonicalHundredGramServing()
    {
        var food = FoodItem.Create("Apple", FoodSource.Custom, SampleNutrition());

        food.AddServingSize(
            "1 medium", quantity: 1m, unit: "fruit", gramsEquivalent: 182m, isDefault: true);

        var canonical = food.ServingSizes.Single(s => s.Label == "100 g");
        Assert.False(canonical.IsDefault);
    }

    [Fact]
    public void AddServingSize_WithIsDefaultTwice_KeepsOnlyTheMostRecentAsDefault()
    {
        var food = FoodItem.Create("Apple", FoodSource.Custom, SampleNutrition());

        food.AddServingSize(
            "1 small", quantity: 1m, unit: "fruit", gramsEquivalent: 149m, isDefault: true);
        var second = food.AddServingSize(
            "1 large", quantity: 1m, unit: "fruit", gramsEquivalent: 223m, isDefault: true);

        var defaults = food.ServingSizes.Where(s => s.IsDefault).ToList();
        var onlyDefault = Assert.Single(defaults);
        Assert.Same(second, onlyDefault);
        Assert.Equal("1 large", onlyDefault.Label);
    }
}
