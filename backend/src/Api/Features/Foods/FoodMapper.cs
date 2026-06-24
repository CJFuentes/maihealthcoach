using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Maps the <see cref="FoodItem"/> aggregate (and its owned <see cref="NutritionFacts"/> /
/// <see cref="ServingSize"/> portions) to the public <see cref="FoodResponse"/> DTO. Enum values
/// are emitted as their string names; servings are ordered deterministically with the default
/// portion first.
/// </summary>
internal static class FoodMapper
{
    /// <summary>
    /// Maps a <see cref="FoodItem"/> to its <see cref="FoodResponse"/>. Servings are ordered with
    /// the default first, then by <see cref="ServingSize.GramsEquivalent"/> and <c>Label</c> for a
    /// stable, deterministic order (the canonical 100 g serving is always present).
    /// </summary>
    internal static FoodResponse ToResponse(FoodItem food)
    {
        var servings = food.ServingSizes
            .OrderByDescending(s => s.IsDefault)
            .ThenBy(s => s.GramsEquivalent)
            .ThenBy(s => s.Label, StringComparer.Ordinal)
            .Select(s => new ServingSizeResponse(
                Id: s.Id,
                Label: s.Label,
                Quantity: s.Quantity,
                Unit: s.Unit,
                GramsEquivalent: s.GramsEquivalent,
                IsDefault: s.IsDefault))
            .ToList();

        return new FoodResponse(
            Id: food.Id,
            Name: food.Name,
            Brand: food.Brand,
            Barcode: food.Barcode,
            Source: food.Source.ToString(),
            SourceReference: food.SourceReference,
            LastSyncedAt: food.LastSyncedAt,
            NutritionPer100g: ToNutritionResponse(food.NutritionPer100g),
            ServingSizes: servings);
    }

    private static NutritionResponse ToNutritionResponse(NutritionFacts nutrition) =>
        new(
            EnergyKcal: nutrition.EnergyKcal,
            ProteinG: nutrition.ProteinG,
            CarbohydrateG: nutrition.CarbohydrateG,
            FatG: nutrition.FatG,
            SugarsG: nutrition.SugarsG,
            FiberG: nutrition.FiberG,
            SaturatedFatG: nutrition.SaturatedFatG,
            SodiumMg: nutrition.SodiumMg);
}
