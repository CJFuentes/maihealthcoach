namespace MAIHealthCoach.Api.Features.Goals;

/// <summary>
/// Pure-static validator for <see cref="SetGoalOverridesRequest"/>. Returns a dictionary of
/// field errors keyed by camelCase field name, compatible with
/// <c>Results.ValidationProblem(errors)</c>. Never throws. Only non-<see langword="null"/>
/// values are validated; a <see langword="null"/> means "clear this override".
/// </summary>
internal static class SetGoalOverridesValidator
{
    private const int MinCaloriesKcal = 1200;
    private const int MinProteinGrams = 10;
    private const int MinCarbohydrateGrams = 0;
    private const int MinFatGrams = 10;
    private const int MinWaterMl = 500;

    private const int MaxCaloriesKcal = 10_000;
    private const int MaxProteinGrams = 1_000;
    private const int MaxCarbohydrateGrams = 2_000;
    private const int MaxFatGrams = 1_000;
    private const int MaxWaterMl = 20_000;

    /// <summary>
    /// Validates <paramref name="request"/> and returns a dictionary of field errors.
    /// An empty dictionary means the request is valid.
    /// </summary>
    internal static IDictionary<string, string[]> Validate(SetGoalOverridesRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.CaloriesKcal.HasValue
            && request.CaloriesKcal.Value is < MinCaloriesKcal or > MaxCaloriesKcal)
        {
            errors["caloriesKcal"] =
                [$"Calorie override must be between {MinCaloriesKcal} and {MaxCaloriesKcal} kcal."];
        }

        if (request.ProteinGrams.HasValue
            && request.ProteinGrams.Value is < MinProteinGrams or > MaxProteinGrams)
        {
            errors["proteinGrams"] =
                [$"Protein override must be between {MinProteinGrams} and {MaxProteinGrams} g."];
        }

        if (request.CarbohydrateGrams.HasValue
            && request.CarbohydrateGrams.Value is < MinCarbohydrateGrams or > MaxCarbohydrateGrams)
        {
            errors["carbohydrateGrams"] =
                [$"Carbohydrate override must be between {MinCarbohydrateGrams} and {MaxCarbohydrateGrams} g."];
        }

        if (request.FatGrams.HasValue
            && request.FatGrams.Value is < MinFatGrams or > MaxFatGrams)
        {
            errors["fatGrams"] =
                [$"Fat override must be between {MinFatGrams} and {MaxFatGrams} g."];
        }

        if (request.WaterMl.HasValue
            && request.WaterMl.Value is < MinWaterMl or > MaxWaterMl)
        {
            errors["waterMl"] =
                [$"Water override must be between {MinWaterMl} and {MaxWaterMl} ml."];
        }

        return errors;
    }
}
