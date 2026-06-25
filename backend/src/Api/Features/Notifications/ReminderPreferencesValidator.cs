using System.Globalization;

namespace MAIHealthCoach.Api.Features.Notifications;

/// <summary>
/// Pure-static validator for reminder-preference upsert requests (issue #48). Returns a dictionary of
/// field errors keyed by camelCase field name, compatible with
/// <c>Results.ValidationProblem(errors)</c>. Never throws. Exposes parse helpers the endpoint calls
/// only after validation has passed, so the endpoint hands the domain pre-parsed <c>TimeOnly</c>s.
/// </summary>
internal static class ReminderPreferencesValidator
{
    private const string TimeFormat = "HH:mm";
    private const int MaxMealTimes = 5;
    private const int MinOffsetMinutes = -840;
    private const int MaxOffsetMinutes = 840;

    /// <summary>Validates an <see cref="UpdateReminderPreferencesRequest"/>. Empty result means valid.</summary>
    internal static IDictionary<string, string[]> Validate(UpdateReminderPreferencesRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.MealReminderTimes is { } mealTimes)
        {
            if (mealTimes.Count > MaxMealTimes)
            {
                errors["mealReminderTimes"] = [$"At most {MaxMealTimes} meal reminder times are allowed."];
            }
            else if (mealTimes.Any(t => !IsValidTime(t)))
            {
                errors["mealReminderTimes"] = [$"Each meal reminder time must be a valid time in {TimeFormat} format."];
            }
        }

        if (request.WaterReminderTime is not null && !IsValidTime(request.WaterReminderTime))
        {
            errors["waterReminderTime"] = [$"Water reminder time must be a valid time in {TimeFormat} format."];
        }

        // Quiet hours are a window: both endpoints or neither.
        var startProvided = request.QuietHoursStart is not null;
        var endProvided = request.QuietHoursEnd is not null;

        if (startProvided != endProvided)
        {
            errors["quietHours"] = ["Quiet hours start and end must both be provided or both omitted."];
        }
        else if (startProvided && endProvided)
        {
            if (!IsValidTime(request.QuietHoursStart) || !IsValidTime(request.QuietHoursEnd))
            {
                errors["quietHours"] = [$"Quiet hours start and end must be valid times in {TimeFormat} format."];
            }
        }

        if (request.UtcOffsetMinutes is < MinOffsetMinutes or > MaxOffsetMinutes)
        {
            errors["utcOffsetMinutes"] = [$"UTC offset must be between {MinOffsetMinutes} and {MaxOffsetMinutes} minutes."];
        }

        return errors;
    }

    /// <summary>
    /// Parses a single "HH:mm" string to a <see cref="TimeOnly"/>, returning null for null/blank or
    /// malformed input. Call only after <see cref="Validate"/> has passed.
    /// </summary>
    internal static TimeOnly? ParseTimeOrNull(string? value) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Parses a list of "HH:mm" strings to <see cref="TimeOnly"/> values, returning an empty list for
    /// null. Call only after <see cref="Validate"/> has passed.
    /// </summary>
    internal static IReadOnlyList<TimeOnly> ParseTimes(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(v => TimeOnly.ParseExact(v, TimeFormat, CultureInfo.InvariantCulture))
            .ToList();
    }

    private static bool IsValidTime(string? value) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
