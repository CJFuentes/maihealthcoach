namespace MAIHealthCoach.Domain.Food;

/// <summary>
/// Immutable value object capturing the nutritional content of a food on a
/// <b>per-100-gram basis</b>. Persisted as columns on the owning <c>FoodItems</c> table via
/// EF Core <c>OwnsOne</c> (see <c>FoodItemConfiguration</c>); it has no identity of its own.
/// </summary>
/// <remarks>
/// The per-100 g basis is the canonical reference used throughout the food domain: a
/// <see cref="ServingSize"/> records its grams-equivalent so the nutrition for an arbitrary
/// portion can be derived with <see cref="ScaleToGrams"/>. The four macro fields
/// (<see cref="EnergyKcal"/>, <see cref="ProteinG"/>, <see cref="CarbohydrateG"/>,
/// <see cref="FatG"/>) are always present — a zero value is meaningful (e.g. water is 0/0/0/0),
/// distinct from "unknown". The micro fields are optional and <see langword="null"/> when the
/// source did not provide them.
/// </remarks>
public sealed class NutritionFacts
{
    /// <summary>Food energy in kilocalories per 100 g. Non-negative.</summary>
    public decimal EnergyKcal { get; private set; }

    /// <summary>Protein in grams per 100 g. Non-negative.</summary>
    public decimal ProteinG { get; private set; }

    /// <summary>Total carbohydrate in grams per 100 g. Non-negative.</summary>
    public decimal CarbohydrateG { get; private set; }

    /// <summary>Total fat in grams per 100 g. Non-negative.</summary>
    public decimal FatG { get; private set; }

    /// <summary>Of-which sugars in grams per 100 g, or <see langword="null"/> if unknown.</summary>
    public decimal? SugarsG { get; private set; }

    /// <summary>Dietary fibre in grams per 100 g, or <see langword="null"/> if unknown.</summary>
    public decimal? FiberG { get; private set; }

    /// <summary>Of-which saturates in grams per 100 g, or <see langword="null"/> if unknown.</summary>
    public decimal? SaturatedFatG { get; private set; }

    /// <summary>Sodium in milligrams per 100 g, or <see langword="null"/> if unknown.</summary>
    public decimal? SodiumMg { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private NutritionFacts() { }

    /// <summary>
    /// Creates a <see cref="NutritionFacts"/> value on a per-100 g basis. All provided values
    /// must be non-negative; the four macro values are required, the micro values are optional.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Any supplied value is negative.</exception>
    public static NutritionFacts Create(
        decimal energyKcal,
        decimal proteinG,
        decimal carbohydrateG,
        decimal fatG,
        decimal? sugarsG = null,
        decimal? fiberG = null,
        decimal? saturatedFatG = null,
        decimal? sodiumMg = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(energyKcal);
        ArgumentOutOfRangeException.ThrowIfNegative(proteinG);
        ArgumentOutOfRangeException.ThrowIfNegative(carbohydrateG);
        ArgumentOutOfRangeException.ThrowIfNegative(fatG);
        ThrowIfNegativeWhenPresent(sugarsG, nameof(sugarsG));
        ThrowIfNegativeWhenPresent(fiberG, nameof(fiberG));
        ThrowIfNegativeWhenPresent(saturatedFatG, nameof(saturatedFatG));
        ThrowIfNegativeWhenPresent(sodiumMg, nameof(sodiumMg));

        return new NutritionFacts
        {
            EnergyKcal = energyKcal,
            ProteinG = proteinG,
            CarbohydrateG = carbohydrateG,
            FatG = fatG,
            SugarsG = sugarsG,
            FiberG = fiberG,
            SaturatedFatG = saturatedFatG,
            SodiumMg = sodiumMg,
        };
    }

    /// <summary>
    /// Scales this per-100 g value to the absolute nutrition for an arbitrary mass.
    /// Because the instance is on a per-100 g basis, the scale factor is
    /// <paramref name="grams"/> / 100. Unknown (<see langword="null"/>) micros remain unknown.
    /// </summary>
    /// <param name="grams">Mass in grams to scale to. Must be non-negative.</param>
    /// <returns>A new <see cref="NutritionFacts"/> holding the scaled, absolute values.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="grams"/> is negative.</exception>
    public NutritionFacts ScaleToGrams(decimal grams)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(grams);

        var factor = grams / 100m;

        return new NutritionFacts
        {
            EnergyKcal = EnergyKcal * factor,
            ProteinG = ProteinG * factor,
            CarbohydrateG = CarbohydrateG * factor,
            FatG = FatG * factor,
            SugarsG = SugarsG * factor,
            FiberG = FiberG * factor,
            SaturatedFatG = SaturatedFatG * factor,
            SodiumMg = SodiumMg * factor,
        };
    }

    private static void ThrowIfNegativeWhenPresent(decimal? value, string paramName)
    {
        if (value is { } v)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(v, paramName);
        }
    }
}
