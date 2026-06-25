using MAIHealthCoach.Domain.Exercise;

namespace MAIHealthCoach.Api.Features.Exercises;

/// <summary>
/// Pure-static validator for exercise catalog requests (issue #33). Returns a dictionary of field
/// errors keyed by camelCase field name, compatible with <c>Results.ValidationProblem(errors)</c>.
/// Never throws; an empty dictionary means the request is valid. Numeric bounds mirror the column
/// precision in <c>ExerciseActivityConfiguration</c> so out-of-range input returns 400 rather than
/// overflowing the database.
/// </summary>
internal static class ExerciseValidator
{
    private const int MaxNameLength = 256;

    // MetValue column is precision(4,2): the maximum representable value is 99.99.
    private const decimal MaxMetValue = 99.99m;

    /// <summary>
    /// Validates a <see cref="CreateCustomExerciseRequest"/>. An empty result means valid.
    /// </summary>
    internal static IDictionary<string, string[]> ValidateCreate(CreateCustomExerciseRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateName(request.Name, errors);
        ValidateCategory(request.Category, errors);
        ValidateMetValue(request.MetValue, errors);
        return errors;
    }

    /// <summary>
    /// Attempts to parse a category string into <see cref="ExerciseCategory"/>, case-insensitively.
    /// Returns <see langword="false"/> for null, blank, unknown, or out-of-range values.
    /// </summary>
    internal static bool TryParseCategory(string? category, out ExerciseCategory parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return Enum.TryParse(category.Trim(), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
    }

    /// <summary>
    /// Builds the standard "invalid category" field-error dictionary for a 400 response. The
    /// message lists every accepted category name so the client can self-correct.
    /// </summary>
    internal static Dictionary<string, string[]> InvalidCategoryError(string? category) =>
        new(StringComparer.Ordinal)
        {
            ["category"] =
            [
                $"'{category}' is not a valid category. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<ExerciseCategory>())}.",
            ],
        };

    // ── sub-validators ──────────────────────────────────────────────────────────────────────

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

    private static void ValidateCategory(string? category, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            errors["category"] = ["Category is required."];
            return;
        }

        if (!TryParseCategory(category, out _))
        {
            errors["category"] = InvalidCategoryError(category)["category"];
        }
    }

    private static void ValidateMetValue(decimal metValue, Dictionary<string, string[]> errors)
    {
        if (metValue <= 0m)
        {
            errors["metValue"] = ["MET value must be greater than zero."];
        }
        else if (metValue > MaxMetValue)
        {
            errors["metValue"] = [$"MET value must not exceed {MaxMetValue}."];
        }
    }
}
