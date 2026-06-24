using System.Globalization;
using MAIHealthCoach.Domain.Diary;

namespace MAIHealthCoach.Api.Features.Diary;

/// <summary>
/// Pure-static validator for diary entry requests. Returns a dictionary of field errors keyed
/// by camelCase field name, compatible with <c>Results.ValidationProblem(errors)</c>. Never
/// throws. Foreign-key existence (food exists, serving belongs to food) is validated against
/// the database in the endpoint handlers, not here.
/// </summary>
internal static class DiaryEntryValidator
{
    /// <summary>The canonical date format accepted and emitted by the diary API.</summary>
    internal const string DateFormat = "yyyy-MM-dd";

    private const decimal MaxQuantity = 99_999.999m;

    /// <summary>Validates a <see cref="CreateDiaryEntryRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateCreate(CreateDiaryEntryRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateQuantity(request.Quantity, errors);
        ValidateMealType(request.MealType, errors);
        ValidateDate(request.Date, errors);
        return errors;
    }

    /// <summary>Validates an <see cref="UpdateDiaryEntryRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateUpdate(UpdateDiaryEntryRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateQuantity(request.Quantity, errors);
        ValidateMealType(request.MealType, errors);
        ValidateDate(request.Date, errors);
        return errors;
    }

    // ── shared sub-validators ─────────────────────────────────────────────────────

    private static void ValidateQuantity(decimal quantity, Dictionary<string, string[]> errors)
    {
        if (quantity <= 0m)
        {
            errors["quantity"] = ["Quantity must be greater than zero."];
        }
        else if (quantity > MaxQuantity)
        {
            errors["quantity"] = [$"Quantity must not exceed {MaxQuantity}."];
        }
    }

    private static void ValidateMealType(string? mealType, Dictionary<string, string[]> errors)
    {
        if (!TryParseMealType(mealType, out _))
        {
            errors["mealType"] =
            [
                $"'{mealType}' is not a valid meal type. " +
                $"Accepted values: {string.Join(", ", Enum.GetNames<MealType>())}.",
            ];
        }
    }

    private static void ValidateDate(string? date, Dictionary<string, string[]> errors)
    {
        if (!TryParseDate(date, out _))
        {
            errors["date"] = [$"Date must be a valid calendar date in {DateFormat} format."];
        }
    }

    // ── shared parse helpers (also used by the GET query-param path) ───────────────

    /// <summary>
    /// Tries to parse a <c>YYYY-MM-DD</c> date string. Returns <see langword="false"/> for null,
    /// blank, or malformed input.
    /// </summary>
    internal static bool TryParseDate(string? date, out DateOnly parsed)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            parsed = default;
            return false;
        }

        return DateOnly.TryParseExact(
            date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
    }

    /// <summary>
    /// Tries to parse a meal-type string (case-insensitive). Returns <see langword="false"/> for
    /// null, blank, or unrecognised input.
    /// </summary>
    internal static bool TryParseMealType(string? mealType, out MealType parsed)
    {
        if (string.IsNullOrWhiteSpace(mealType))
        {
            parsed = default;
            return false;
        }

        return Enum.TryParse(mealType, ignoreCase: true, out parsed)
            && Enum.IsDefined(parsed);
    }

    /// <summary>
    /// Parses a date string already known to be valid (call only after <see cref="ValidateCreate"/>
    /// or <see cref="ValidateUpdate"/> returned no errors).
    /// </summary>
    internal static DateOnly ParseDate(string date) =>
        DateOnly.ParseExact(date, DateFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Parses a meal-type string already known to be valid (call only after
    /// <see cref="ValidateCreate"/> or <see cref="ValidateUpdate"/> returned no errors).
    /// </summary>
    internal static MealType ParseMealType(string mealType) =>
        Enum.Parse<MealType>(mealType, ignoreCase: true);
}
