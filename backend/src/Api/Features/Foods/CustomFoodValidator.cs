using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Api.Features.Foods;

/// <summary>
/// Pure-static validator for custom-food create/edit requests (issue #24). Returns a dictionary of
/// field errors keyed by camelCase/dotted field path, compatible with
/// <c>Results.ValidationProblem(errors)</c>. Never throws; an empty dictionary means the request
/// is valid. Numeric bounds mirror the column precisions in <c>FoodItemConfiguration</c> and
/// <c>ServingSizeConfiguration</c> so out-of-range input returns 400 rather than overflowing the
/// database (500). Foreign-key/ownership existence is enforced in the endpoint handlers.
/// </summary>
internal static class CustomFoodValidator
{
    private const int MaxNameLength = 256;
    private const int MaxBrandLength = 256;
    private const int MaxServingLabelLength = 64;
    private const int MaxServingUnitLength = 32;
    private const int MaxServings = 20;

    // Upper bounds derived from the persisted column precisions (max value = 10^(p-s) - 10^-s).
    private const decimal MaxEnergyKcal = 99_999.99m;   // Nutrition_EnergyKcal precision(7,2)
    private const decimal MaxMacroG = 9_999.99m;        // protein/carb/fat/sugars/fiber/satfat precision(6,2)
    private const decimal MaxSodiumMg = 999_999.99m;    // Nutrition_SodiumMg precision(8,2)
    private const decimal MaxServingDecimal = 999_999.999m; // ServingSizes Quantity/GramsEquivalent precision(9,3)

    /// <summary>Validates a <see cref="CreateCustomFoodRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateCreate(CreateCustomFoodRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateName(request.Name, errors);
        ValidateBrand(request.Brand, errors);
        ValidateNutrition(request.Nutrition, errors);
        ValidateServings(request.Servings, errors);
        return errors;
    }

    /// <summary>Validates an <see cref="UpdateCustomFoodRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateUpdate(UpdateCustomFoodRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateName(request.Name, errors);
        ValidateBrand(request.Brand, errors);
        ValidateNutrition(request.Nutrition, errors);
        ValidateServings(request.Servings, errors);
        return errors;
    }

    /// <summary>
    /// Builds a <see cref="NutritionFacts"/> from a validated <see cref="NutritionRequest"/>. Call
    /// only after <see cref="ValidateCreate"/>/<see cref="ValidateUpdate"/> returned no errors.
    /// </summary>
    internal static NutritionFacts ToNutritionFacts(NutritionRequest nutrition) =>
        NutritionFacts.Create(
            energyKcal: nutrition.EnergyKcal,
            proteinG: nutrition.ProteinG,
            carbohydrateG: nutrition.CarbohydrateG,
            fatG: nutrition.FatG,
            sugarsG: nutrition.SugarsG,
            fiberG: nutrition.FiberG,
            saturatedFatG: nutrition.SaturatedFatG,
            sodiumMg: nutrition.SodiumMg);

    /// <summary>
    /// Projects a validated serving-request list into the tuple shape consumed by
    /// <see cref="FoodItem.ReplaceCustomServings"/>. Call only after validation passed.
    /// </summary>
    internal static IReadOnlyCollection<(string Label, decimal Quantity, string Unit, decimal GramsEquivalent, bool IsDefault)>
        ToServingTuples(IReadOnlyList<ServingSizeRequest> servings) =>
        servings
            .Select(s => (s.Label, s.Quantity, s.Unit, s.GramsEquivalent, s.IsDefault))
            .ToList();

    // ── shared sub-validators ─────────────────────────────────────────────────────

    private static void ValidateName(string? name, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Trim().Length > MaxNameLength)
        {
            errors["name"] = [$"Name must not exceed {MaxNameLength} characters."];
        }
    }

    private static void ValidateBrand(string? brand, Dictionary<string, string[]> errors)
    {
        if (brand is not null && brand.Trim().Length > MaxBrandLength)
        {
            errors["brand"] = [$"Brand must not exceed {MaxBrandLength} characters."];
        }
    }

    private static void ValidateNutrition(NutritionRequest? nutrition, Dictionary<string, string[]> errors)
    {
        if (nutrition is null)
        {
            errors["nutrition"] = ["Nutrition is required."];
            return;
        }

        ValidateRequiredMacro(nutrition.EnergyKcal, "nutrition.energyKcal", MaxEnergyKcal, errors);
        ValidateRequiredMacro(nutrition.ProteinG, "nutrition.proteinG", MaxMacroG, errors);
        ValidateRequiredMacro(nutrition.CarbohydrateG, "nutrition.carbohydrateG", MaxMacroG, errors);
        ValidateRequiredMacro(nutrition.FatG, "nutrition.fatG", MaxMacroG, errors);

        ValidateOptionalMicro(nutrition.SugarsG, "nutrition.sugarsG", MaxMacroG, errors);
        ValidateOptionalMicro(nutrition.FiberG, "nutrition.fiberG", MaxMacroG, errors);
        ValidateOptionalMicro(nutrition.SaturatedFatG, "nutrition.saturatedFatG", MaxMacroG, errors);
        ValidateOptionalMicro(nutrition.SodiumMg, "nutrition.sodiumMg", MaxSodiumMg, errors);
    }

    private static void ValidateRequiredMacro(
        decimal value, string key, decimal max, Dictionary<string, string[]> errors)
    {
        if (value < 0m)
        {
            errors[key] = ["Value must be zero or greater."];
        }
        else if (value > max)
        {
            errors[key] = [$"Value must not exceed {max}."];
        }
    }

    private static void ValidateOptionalMicro(
        decimal? value, string key, decimal max, Dictionary<string, string[]> errors)
    {
        if (value is not { } v)
        {
            return;
        }

        if (v < 0m)
        {
            errors[key] = ["Value must be zero or greater."];
        }
        else if (v > max)
        {
            errors[key] = [$"Value must not exceed {max}."];
        }
    }

    private static void ValidateServings(
        IReadOnlyList<ServingSizeRequest>? servings, Dictionary<string, string[]> errors)
    {
        if (servings is null || servings.Count == 0)
        {
            return;
        }

        if (servings.Count > MaxServings)
        {
            errors["servings"] = [$"At most {MaxServings} servings may be provided."];
            return;
        }

        for (var i = 0; i < servings.Count; i++)
        {
            var serving = servings[i];

            if (string.IsNullOrWhiteSpace(serving.Label))
            {
                errors[$"servings[{i}].label"] = ["Serving label is required."];
            }
            else if (serving.Label.Trim().Length > MaxServingLabelLength)
            {
                errors[$"servings[{i}].label"] = [$"Serving label must not exceed {MaxServingLabelLength} characters."];
            }

            if (string.IsNullOrWhiteSpace(serving.Unit))
            {
                errors[$"servings[{i}].unit"] = ["Serving unit is required."];
            }
            else if (serving.Unit.Trim().Length > MaxServingUnitLength)
            {
                errors[$"servings[{i}].unit"] = [$"Serving unit must not exceed {MaxServingUnitLength} characters."];
            }

            if (serving.Quantity <= 0m)
            {
                errors[$"servings[{i}].quantity"] = ["Serving quantity must be greater than zero."];
            }
            else if (serving.Quantity > MaxServingDecimal)
            {
                errors[$"servings[{i}].quantity"] = [$"Serving quantity must not exceed {MaxServingDecimal}."];
            }

            if (serving.GramsEquivalent <= 0m)
            {
                errors[$"servings[{i}].gramsEquivalent"] = ["Serving grams-equivalent must be greater than zero."];
            }
            else if (serving.GramsEquivalent > MaxServingDecimal)
            {
                errors[$"servings[{i}].gramsEquivalent"] = [$"Serving grams-equivalent must not exceed {MaxServingDecimal}."];
            }
        }
    }
}
