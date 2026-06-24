using System;
using System.Linq;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// EF Core round-trip tests for the <see cref="FoodItem"/> aggregate over a real relational
/// provider (SQLite in-memory). They exercise the mapping in <c>FoodItemConfiguration</c> and
/// <c>ServingSizeConfiguration</c>: the owned per-100 g <see cref="NutritionFacts"/> (including
/// the all-zero-macro null-collapse guard), serving-size persistence, the non-unique barcode
/// index, and the presence of the lookup indexes.
/// </summary>
/// <remarks>
/// Each test builds its own context over a freshly opened in-memory connection. Reading back uses
/// a second <see cref="AppDbContext"/> over the SAME open connection so the assertions reflect a
/// genuine materialization from the database rather than the change tracker's identity cache.
/// </remarks>
public sealed class FoodPersistenceTests
{
    private static AppDbContext NewContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void FoodItem_WithFullNutritionAndServings_RoundTripsAllScalarsAndOwnedValues()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();

            var nutrition = NutritionFacts.Create(
                energyKcal: 59m, proteinG: 10m, carbohydrateG: 3.6m, fatG: 0.4m,
                sugarsG: 3.2m, fiberG: 0m, saturatedFatG: 0.1m, sodiumMg: 36m);

            var food = FoodItem.Create(
                "Greek Yogurt",
                FoodSource.OpenFoodFacts,
                nutrition,
                brand: "Fage",
                barcode: "5201054000000",
                sourceReference: "off-12345",
                lastSyncedAt: new DateTimeOffset(2026, 6, 1, 8, 30, 0, TimeSpan.Zero));

            food.AddServingSize("1 cup", quantity: 1m, unit: "cup", gramsEquivalent: 245m, isDefault: true);

            ctx.FoodItems.Add(food);
            ctx.SaveChanges();
        }

        using (var ctx = NewContext(connection))
        {
            var food = ctx.FoodItems
                .Include(f => f.ServingSizes)
                .Single(f => f.Name == "Greek Yogurt");

            Assert.Equal(FoodSource.OpenFoodFacts, food.Source);
            Assert.Equal("Fage", food.Brand);
            Assert.Equal("5201054000000", food.Barcode);
            Assert.Equal("off-12345", food.SourceReference);
            Assert.Equal(new DateTimeOffset(2026, 6, 1, 8, 30, 0, TimeSpan.Zero), food.LastSyncedAt);

            Assert.NotNull(food.NutritionPer100g);
            Assert.Equal(59m, food.NutritionPer100g.EnergyKcal);
            Assert.Equal(10m, food.NutritionPer100g.ProteinG);
            Assert.Equal(3.6m, food.NutritionPer100g.CarbohydrateG);
            Assert.Equal(0.4m, food.NutritionPer100g.FatG);
            Assert.Equal(3.2m, food.NutritionPer100g.SugarsG);
            Assert.Equal(0m, food.NutritionPer100g.FiberG);
            Assert.Equal(0.1m, food.NutritionPer100g.SaturatedFatG);
            Assert.Equal(36m, food.NutritionPer100g.SodiumMg);

            // Canonical 100 g serving plus the added "1 cup".
            Assert.Equal(2, food.ServingSizes.Count);
            var cup = food.ServingSizes.Single(s => s.Label == "1 cup");
            Assert.Equal(245m, cup.GramsEquivalent);
            Assert.Equal("cup", cup.Unit);
            Assert.True(cup.IsDefault);
        }
    }

    [Fact]
    public void FoodItem_WithZeroMacrosAndNullMicros_RoundTripsWithNonNullNutrition()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();

            var water = FoodItem.Create(
                "Water",
                FoodSource.Custom,
                NutritionFacts.Create(energyKcal: 0m, proteinG: 0m, carbohydrateG: 0m, fatG: 0m));

            ctx.FoodItems.Add(water);
            ctx.SaveChanges();
        }

        using (var ctx = NewContext(connection))
        {
            var water = ctx.FoodItems.Single(f => f.Name == "Water");

            // Guards the owned-VO null-collapse: an all-zero/all-null owned value must NOT
            // materialize as a null navigation.
            Assert.NotNull(water.NutritionPer100g);
            Assert.Equal(0m, water.NutritionPer100g.EnergyKcal);
            Assert.Equal(0m, water.NutritionPer100g.ProteinG);
            Assert.Equal(0m, water.NutritionPer100g.CarbohydrateG);
            Assert.Equal(0m, water.NutritionPer100g.FatG);

            Assert.Null(water.NutritionPer100g.SugarsG);
            Assert.Null(water.NutritionPer100g.FiberG);
            Assert.Null(water.NutritionPer100g.SaturatedFatG);
            Assert.Null(water.NutritionPer100g.SodiumMg);

            Assert.Null(water.Barcode);
        }
    }

    [Fact]
    public void FoodItem_WithMultipleServingSizes_PersistsAllOfThem()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();

            var food = FoodItem.Create(
                "Apple",
                FoodSource.Custom,
                NutritionFacts.Create(energyKcal: 52m, proteinG: 0.3m, carbohydrateG: 14m, fatG: 0.2m));

            food.AddServingSize("1 small", quantity: 1m, unit: "fruit", gramsEquivalent: 149m);
            food.AddServingSize("1 medium", quantity: 1m, unit: "fruit", gramsEquivalent: 182m);
            food.AddServingSize("1 large", quantity: 1m, unit: "fruit", gramsEquivalent: 223m);

            ctx.FoodItems.Add(food);
            ctx.SaveChanges();
        }

        using (var ctx = NewContext(connection))
        {
            var food = ctx.FoodItems
                .Include(f => f.ServingSizes)
                .Single(f => f.Name == "Apple");

            // 3 added + the canonical 100 g serving.
            Assert.Equal(4, food.ServingSizes.Count);
            Assert.Contains(food.ServingSizes, s => s.Label == "100 g");
            Assert.Contains(food.ServingSizes, s => s.Label == "1 small" && s.GramsEquivalent == 149m);
            Assert.Contains(food.ServingSizes, s => s.Label == "1 medium" && s.GramsEquivalent == 182m);
            Assert.Contains(food.ServingSizes, s => s.Label == "1 large" && s.GramsEquivalent == 223m);
        }
    }

    [Fact]
    public void TwoFoodItems_WithNullBarcode_CanBothBeSaved()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();

            // Distinct owned NutritionFacts instances per food: the owned value object has no
            // identity of its own, so sharing one instance across two roots is not valid.
            ctx.FoodItems.Add(FoodItem.Create(
                "Homemade Soup",
                FoodSource.Custom,
                NutritionFacts.Create(energyKcal: 10m, proteinG: 1m, carbohydrateG: 1m, fatG: 0m)));
            ctx.FoodItems.Add(FoodItem.Create(
                "Garden Salad",
                FoodSource.Custom,
                NutritionFacts.Create(energyKcal: 15m, proteinG: 1m, carbohydrateG: 3m, fatG: 0m)));

            // The barcode index is non-unique, so two NULL barcodes must not collide.
            ctx.SaveChanges();
        }

        using (var ctx = NewContext(connection))
        {
            var nullBarcodeCount = ctx.FoodItems.Count(f => f.Barcode == null);
            Assert.Equal(2, nullBarcodeCount);
        }
    }

    [Fact]
    public void Model_ExposesBarcodeAndNameLookupIndexes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var ctx = NewContext(connection);
        ctx.Database.EnsureCreated();

        var entityType = ctx.Model.FindEntityType(typeof(FoodItem));
        Assert.NotNull(entityType);

        var indexNames = entityType!
            .GetIndexes()
            .Select(i => i.GetDatabaseName())
            .ToList();

        Assert.Contains("IX_FoodItems_Barcode", indexNames);
        Assert.Contains("IX_FoodItems_Name", indexNames);
    }
}
