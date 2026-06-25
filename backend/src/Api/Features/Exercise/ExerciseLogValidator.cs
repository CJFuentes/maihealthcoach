using System.Globalization;

namespace MAIHealthCoach.Api.Features.Exercise;

/// <summary>
/// Pure-static validator for exercise-log requests (issue #34). Returns a dictionary of field
/// errors keyed by camelCase field name, compatible with <c>Results.ValidationProblem(errors)</c>.
/// Never throws. Cross-user existence and activity visibility are enforced in the endpoint handlers
/// (scoped queries), not here.
/// </summary>
internal static class ExerciseLogValidator
{
    /// <summary>The canonical date format accepted and emitted by the exercise-log API.</summary>
    internal const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Sane per-entry upper bound on duration: 1440 minutes (a single day). Catches accidental
    /// runaway input while still admitting any realistic single session.
    /// </summary>
    private const int MaxDurationMinutes = 1440;

    /// <summary>Validates a <see cref="LogExerciseRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateLog(LogExerciseRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.ExerciseActivityId == Guid.Empty)
        {
            errors["exerciseActivityId"] = ["Exercise activity id is required."];
        }

        ValidateDurationMinutes(request.DurationMinutes, errors);
        ValidateDate(request.Date, errors);
        return errors;
    }

    /// <summary>Validates an <see cref="UpdateExerciseLogRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> ValidateUpdate(UpdateExerciseLogRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        ValidateDurationMinutes(request.DurationMinutes, errors);
        ValidateDate(request.Date, errors);
        return errors;
    }

    // ── shared sub-validators ─────────────────────────────────────────────────────

    private static void ValidateDurationMinutes(int durationMinutes, Dictionary<string, string[]> errors)
    {
        if (durationMinutes <= 0)
        {
            errors["durationMinutes"] = ["Duration must be greater than zero."];
        }
        else if (durationMinutes > MaxDurationMinutes)
        {
            errors["durationMinutes"] = [$"Duration must not exceed {MaxDurationMinutes} minutes."];
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
    /// Parses a date string already known to be valid (call only after <see cref="ValidateLog"/>
    /// or <see cref="ValidateUpdate"/> returned no errors).
    /// </summary>
    internal static DateOnly ParseDate(string date) =>
        DateOnly.ParseExact(date, DateFormat, CultureInfo.InvariantCulture);
}
