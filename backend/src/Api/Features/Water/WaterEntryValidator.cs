using System.Globalization;

namespace MAIHealthCoach.Api.Features.Water;

/// <summary>
/// Pure-static validator for water-log requests. Returns a dictionary of field errors keyed by
/// camelCase field name, compatible with <c>Results.ValidationProblem(errors)</c>. Never throws.
/// Cross-user existence is enforced in the endpoint handlers (scoped queries), not here.
/// </summary>
internal static class WaterEntryValidator
{
    /// <summary>The canonical date format accepted and emitted by the water API.</summary>
    internal const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Sane per-entry upper bound (10 litres). Catches accidental five-digit input while still
    /// admitting any realistic single logged amount.
    /// </summary>
    private const int MaxAmountMl = 10_000;

    /// <summary>Validates an <see cref="AddWaterEntryRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateAdd(AddWaterEntryRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateAmountMl(request.AmountMl, errors);
        ValidateDate(request.Date, errors);
        return errors;
    }

    /// <summary>Validates an <see cref="UpdateWaterEntryRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateUpdate(UpdateWaterEntryRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateAmountMl(request.AmountMl, errors);
        ValidateDate(request.Date, errors);
        return errors;
    }

    // ── shared sub-validators ─────────────────────────────────────────────────────

    private static void ValidateAmountMl(int amountMl, Dictionary<string, string[]> errors)
    {
        if (amountMl <= 0)
        {
            errors["amountMl"] = ["Amount must be greater than zero."];
        }
        else if (amountMl > MaxAmountMl)
        {
            errors["amountMl"] = [$"Amount must not exceed {MaxAmountMl} ml."];
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
    /// Parses a date string already known to be valid (call only after <see cref="ValidateAdd"/>
    /// or <see cref="ValidateUpdate"/> returned no errors).
    /// </summary>
    internal static DateOnly ParseDate(string date) =>
        DateOnly.ParseExact(date, DateFormat, CultureInfo.InvariantCulture);
}
