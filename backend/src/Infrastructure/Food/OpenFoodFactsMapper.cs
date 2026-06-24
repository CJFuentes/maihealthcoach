using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Infrastructure.Food;

/// <summary>
/// A mapped Open Food Facts product, ready to be turned into a <see cref="FoodItem"/> by the
/// service. Carries the identifying fields, the per-100 g <see cref="NutritionFacts"/>, and an
/// optional OFF-derived default <see cref="Serving"/> (<see langword="null"/> when OFF provided no
/// usable serving distinct from the canonical 100 g portion).
/// </summary>
internal sealed record MappedFood(
    string Name,
    string? Brand,
    string? Barcode,
    string? SourceReference,
    NutritionFacts Nutrition,
    OffServing? Serving);

/// <summary>
/// Translates tolerant <see cref="OffProduct"/> DTOs into the strict food domain. Skips products
/// that lack a usable name or energy value, parses nutriment values that may be numbers or strings,
/// converts salt to sodium when only salt is present, and clamps every value to the database column
/// precision before constructing <see cref="NutritionFacts"/> (whose factory rejects negatives).
/// </summary>
internal static partial class OpenFoodFactsMapper
{
    // Column-precision ceilings (see FoodItemConfiguration): energy numeric(7,2), macros
    // numeric(6,2), sodium numeric(8,2). Values are clamped to these before NutritionFacts.Create.
    private const decimal EnergyMax = 99999.99m;
    private const decimal MacroMax = 9999.99m;
    private const decimal SodiumMax = 999999.99m;

    // Sodium = salt / 2.5 (salt is sodium chloride). Salt is grams/100 g; sodium stored as mg/100 g.
    private const decimal SaltToSodiumDivisor = 2.5m;
    private const decimal GramsToMilligrams = 1000m;

    // Nutriment keys, matched ordinally (no culture-sensitive casing).
    private const string EnergyKcalKey = "energy-kcal_100g";
    private const string ProteinsKey = "proteins_100g";
    private const string CarbohydratesKey = "carbohydrates_100g";
    private const string FatKey = "fat_100g";
    private const string SugarsKey = "sugars_100g";
    private const string FiberKey = "fiber_100g";
    private const string SaturatedFatKey = "saturated-fat_100g";
    private const string SodiumKey = "sodium_100g";
    private const string SaltKey = "salt_100g";

    /// <summary>
    /// Attempts to map a product. Returns <see langword="false"/> (skip) when the product has no
    /// usable name or no usable energy value; otherwise builds <paramref name="mapped"/>.
    /// </summary>
    public static bool TryMapProduct(OffProduct product, out MappedFood mapped)
    {
        mapped = null!;

        var name = product.ProductName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!TryGetDecimal(product.Nutriments, EnergyKcalKey, out var energy))
        {
            return false;
        }

        var nutrition = NutritionFacts.Create(
            energyKcal: Clamp(energy, EnergyMax),
            proteinG: ClampMacro(product.Nutriments, ProteinsKey),
            carbohydrateG: ClampMacro(product.Nutriments, CarbohydratesKey),
            fatG: ClampMacro(product.Nutriments, FatKey),
            sugarsG: ClampMacroOrNull(product.Nutriments, SugarsKey),
            fiberG: ClampMacroOrNull(product.Nutriments, FiberKey),
            saturatedFatG: ClampMacroOrNull(product.Nutriments, SaturatedFatKey),
            sodiumMg: ResolveSodiumMg(product.Nutriments));

        var brand = NormalizeBrand(product.Brands);
        var barcode = NormalizeBarcode(product.Code);
        var serving = ParseServing(product);

        mapped = new MappedFood(name, brand, barcode, barcode, nutrition, serving);
        return true;
    }

    /// <summary>
    /// Reads a nutriment value tolerating both JSON number and string kinds. Returns
    /// <see langword="false"/> when the key is absent or the value cannot be parsed as a decimal.
    /// </summary>
    public static bool TryGetDecimal(
        Dictionary<string, JsonElement>? nutriments,
        string key,
        out decimal value)
    {
        value = 0m;

        if (nutriments is null || !nutriments.TryGetValue(key, out var element))
        {
            return false;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);
            case JsonValueKind.String:
                var raw = element.GetString();
                return decimal.TryParse(
                    raw,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
            default:
                return false;
        }
    }

    private static decimal ClampMacro(Dictionary<string, JsonElement>? nutriments, string key) =>
        TryGetDecimal(nutriments, key, out var v) ? Clamp(v, MacroMax) : 0m;

    private static decimal? ClampMacroOrNull(Dictionary<string, JsonElement>? nutriments, string key) =>
        TryGetDecimal(nutriments, key, out var v) ? Clamp(v, MacroMax) : null;

    /// <summary>
    /// Resolves sodium in mg/100 g. Prefers the explicit sodium field (grams/100 g -> mg); falls
    /// back to deriving sodium from salt (salt = sodium * 2.5). Returns <see langword="null"/> when
    /// neither is present.
    /// </summary>
    private static decimal? ResolveSodiumMg(Dictionary<string, JsonElement>? nutriments)
    {
        if (TryGetDecimal(nutriments, SodiumKey, out var sodiumG))
        {
            return Clamp(sodiumG * GramsToMilligrams, SodiumMax);
        }

        if (TryGetDecimal(nutriments, SaltKey, out var saltG))
        {
            return Clamp(saltG * GramsToMilligrams / SaltToSodiumDivisor, SodiumMax);
        }

        return null;
    }

    /// <summary>Clamps to [0, max] and rounds to 2 decimals so the value fits its column scale.</summary>
    private static decimal Clamp(decimal value, decimal max)
    {
        if (value < 0m)
        {
            value = 0m;
        }
        else if (value > max)
        {
            value = max;
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeBrand(string? brands)
    {
        if (string.IsNullOrWhiteSpace(brands))
        {
            return null;
        }

        // OFF brands is often comma-separated; take the first as the primary brand.
        var first = brands.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return first.Length > 0 ? first[0] : null;
    }

    private static string? NormalizeBarcode(string? code)
    {
        var trimmed = code?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <summary>
    /// Derives an OFF serving from <see cref="OffProduct.ServingSize"/> and
    /// <see cref="OffProduct.ServingQuantity"/>, preferring grams. Returns <see langword="null"/>
    /// when no positive grams-equivalent can be resolved, or when the serving would merely
    /// duplicate the canonical 100 g serving — never a zero quantity (which the domain rejects).
    /// </summary>
    private static OffServing? ParseServing(OffProduct product)
    {
        var grams = ResolveServingGrams(product);
        if (grams is not { } g || g <= 0m)
        {
            return null;
        }

        // A 100 g serving duplicates the canonical portion; let the canonical serving cover it.
        if (g == 100m)
        {
            return null;
        }

        var (quantity, unit) = ResolveQuantityAndUnit(product, g);
        var label = string.IsNullOrWhiteSpace(product.ServingSize)
            ? "1 serving"
            : product.ServingSize.Trim();

        return new OffServing(label, quantity, unit, g);
    }

    /// <summary>
    /// Resolves the serving mass in grams: prefers <c>serving_quantity</c> (a numeric grams value
    /// in OFF), then parses a number+unit out of <c>serving_size</c>, treating "g" as grams.
    /// </summary>
    private static decimal? ResolveServingGrams(OffProduct product)
    {
        if (TryGetServingQuantity(product.ServingQuantity, out var quantity) && quantity > 0m)
        {
            return Math.Round(quantity, 2, MidpointRounding.AwayFromZero);
        }

        var match = ServingSizeRegex().Match(product.ServingSize ?? string.Empty);
        if (match.Success
            && decimal.TryParse(
                match.Groups["num"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
            && parsed > 0m)
        {
            var unit = match.Groups["unit"].Value;
            // Only grams are a reliable mass; treat a unitless/gram value as grams.
            if (string.IsNullOrEmpty(unit) || string.Equals(unit, "g", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Round(parsed, 2, MidpointRounding.AwayFromZero);
            }
        }

        return null;
    }

    private static (decimal Quantity, string Unit) ResolveQuantityAndUnit(OffProduct product, decimal grams)
    {
        var match = ServingSizeRegex().Match(product.ServingSize ?? string.Empty);
        if (match.Success
            && decimal.TryParse(
                match.Groups["num"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed)
            && parsed > 0m)
        {
            var unit = match.Groups["unit"].Value;
            if (!string.IsNullOrEmpty(unit))
            {
                return (Math.Round(parsed, 2, MidpointRounding.AwayFromZero), unit);
            }
        }

        // Fall back to expressing the serving directly in grams.
        return (grams, "g");
    }

    private static bool TryGetServingQuantity(JsonElement element, out decimal value)
    {
        value = 0m;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);
            case JsonValueKind.String:
                return decimal.TryParse(
                    element.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
            default:
                return false;
        }
    }

    // Extracts a leading number and an optional alphabetic unit, e.g. "30 g", "240ml",
    // "1 cup (250 g)" -> num=1, unit=cup. Allows a decimal separator and optional whitespace.
    [GeneratedRegex(@"(?<num>\d+(?:[.,]\d+)?)\s*(?<unit>[a-zA-Z]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex ServingSizeRegex();
}
