using System.Text.Json;
using MAIHealthCoach.Infrastructure.Food;

namespace MAIHealthCoach.Infrastructure.Tests.Food;

/// <summary>
/// Tolerance tests for <see cref="OpenFoodFactsMapper"/>. Open Food Facts is a crowd-sourced source
/// whose records are frequently partial or malformed: nutriment values arrive as strings, micros are
/// missing, names/energy are absent, only salt (not sodium) is present, values are negative or
/// absurdly large, and servings are degenerate. The mapper must never throw and must produce sane,
/// column-safe values. Each product is built by deserializing realistic OFF JSON into the tolerant
/// <see cref="OffProduct"/> DTO, mirroring the real client path.
/// </summary>
public sealed class OpenFoodFactsMapperTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static OffProduct Product(string productJson) =>
        JsonSerializer.Deserialize<OffProduct>(productJson, SerializerOptions)!;

    [Fact]
    public void TryMapProduct_NutrimentValuesAsStrings_ParseAsNumbers()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Stringy Cereal",
              "nutriments": {
                "energy-kcal_100g": "250",
                "proteins_100g": "8.5",
                "carbohydrates_100g": "70",
                "fat_100g": "3"
              }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Equal(250m, mapped.Nutrition.EnergyKcal);
        Assert.Equal(8.5m, mapped.Nutrition.ProteinG);
        Assert.Equal(70m, mapped.Nutrition.CarbohydrateG);
        Assert.Equal(3m, mapped.Nutrition.FatG);
    }

    [Fact]
    public void TryMapProduct_MissingOptionalMicros_AreNull()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Plain Food",
              "nutriments": {
                "energy-kcal_100g": 100,
                "proteins_100g": 5,
                "carbohydrates_100g": 10,
                "fat_100g": 2
              }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Null(mapped.Nutrition.SugarsG);
        Assert.Null(mapped.Nutrition.FiberG);
        Assert.Null(mapped.Nutrition.SaturatedFatG);
        Assert.Null(mapped.Nutrition.SodiumMg);
    }

    [Fact]
    public void TryMapProduct_NoUsableName_IsSkipped()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "   ",
              "nutriments": { "energy-kcal_100g": 100, "proteins_100g": 1, "carbohydrates_100g": 1, "fat_100g": 1 }
            }
            """);

        Assert.False(OpenFoodFactsMapper.TryMapProduct(product, out _));
    }

    [Fact]
    public void TryMapProduct_NoEnergy_IsSkipped()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "No Energy Food",
              "nutriments": { "proteins_100g": 8 }
            }
            """);

        Assert.False(OpenFoodFactsMapper.TryMapProduct(product, out _));
    }

    [Fact]
    public void TryMapProduct_SaltPresentButNoSodium_DerivesSodium()
    {
        // salt 1.0 g/100 g -> sodium = 1.0 * 1000 / 2.5 = 400 mg/100 g.
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Salty Snack",
              "nutriments": {
                "energy-kcal_100g": 500,
                "proteins_100g": 5,
                "carbohydrates_100g": 50,
                "fat_100g": 30,
                "salt_100g": 1.0
              }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Equal(400m, mapped.Nutrition.SodiumMg);
    }

    [Fact]
    public void TryMapProduct_SodiumPresent_PrefersSodiumOverSalt()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Both Salt And Sodium",
              "nutriments": {
                "energy-kcal_100g": 500,
                "proteins_100g": 5,
                "carbohydrates_100g": 50,
                "fat_100g": 30,
                "sodium_100g": 0.2,
                "salt_100g": 1.0
              }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        // Uses sodium_100g (0.2 g -> 200 mg), not the salt-derived 400 mg.
        Assert.Equal(200m, mapped.Nutrition.SodiumMg);
    }

    [Fact]
    public void TryMapProduct_NegativeNutriment_IsClampedToZeroWithoutThrowing()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Weird Data",
              "nutriments": {
                "energy-kcal_100g": 100,
                "proteins_100g": -5,
                "carbohydrates_100g": 10,
                "fat_100g": 2
              }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Equal(0m, mapped.Nutrition.ProteinG); // negative clamped to 0, no throw
    }

    [Fact]
    public void TryMapProduct_HugeValueExceedingColumnPrecision_IsClampedWithoutThrowing()
    {
        // Energy column is numeric(7,2): max 99999.99. A 9-digit value must clamp, not overflow.
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Overflow Food",
              "nutriments": {
                "energy-kcal_100g": 123456789,
                "proteins_100g": 1,
                "carbohydrates_100g": 1,
                "fat_100g": 1
              }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Equal(99999.99m, mapped.Nutrition.EnergyKcal);
    }

    [Fact]
    public void TryMapProduct_ZeroGramServing_ProducesNoServing()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Zero Serving Food",
              "serving_size": "0 g",
              "serving_quantity": 0,
              "nutriments": { "energy-kcal_100g": 100, "proteins_100g": 1, "carbohydrates_100g": 1, "fat_100g": 1 }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Null(mapped.Serving); // a zero-gram serving is dropped, not a zero-gram ServingSize
    }

    [Fact]
    public void TryMapProduct_HundredGramServing_IsDroppedAsCanonicalDuplicate()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Hundred Gram Serving",
              "serving_size": "100 g",
              "serving_quantity": 100,
              "nutriments": { "energy-kcal_100g": 100, "proteins_100g": 1, "carbohydrates_100g": 1, "fat_100g": 1 }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Null(mapped.Serving); // duplicates the canonical 100 g portion
    }

    [Fact]
    public void TryMapProduct_NumericServing_IsMappedWithPositiveGrams()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Granola",
              "serving_size": "45 g",
              "serving_quantity": 45,
              "nutriments": { "energy-kcal_100g": 400, "proteins_100g": 9, "carbohydrates_100g": 60, "fat_100g": 12 }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.NotNull(mapped.Serving);
        Assert.Equal(45m, mapped.Serving!.GramsEquivalent);
        Assert.Equal("45 g", mapped.Serving.Label);
    }

    [Fact]
    public void TryMapProduct_CommaSeparatedBrands_TakesTheFirstBrand()
    {
        var product = Product(
            """
            {
              "code": "111",
              "product_name": "Multi-Brand Food",
              "brands": "Fage, Total, Premium",
              "nutriments": { "energy-kcal_100g": 100, "proteins_100g": 1, "carbohydrates_100g": 1, "fat_100g": 1 }
            }
            """);

        Assert.True(OpenFoodFactsMapper.TryMapProduct(product, out var mapped));
        Assert.Equal("Fage", mapped.Brand);
    }
}
