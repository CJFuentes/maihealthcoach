using System.Globalization;
using System.Text.Json;
using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Notifications;

/// <summary>
/// A user's push-reminder preferences (issue #48): whether meal and water reminders are enabled, the
/// times of day they fire, an optional quiet-hours window during which no reminder is sent, and the
/// user's UTC offset so the server can evaluate "is it that local time yet?" without storing a full
/// IANA time zone. One row per user (unique on <see cref="UserId"/>).
/// </summary>
/// <remarks>
/// All time-of-day values are persisted as <c>"HH:mm"</c> <see cref="string"/> columns rather than
/// <c>TimeOnly</c>: the SQLite integration-test harness's value-converter shim does not handle
/// <c>TimeOnly</c>, so the EF model avoids it entirely. The pure reminder decider still works in
/// <c>TimeOnly</c>, parsing these strings in memory. Meal reminder times are stored as a JSON array
/// of <c>"HH:mm"</c> strings (capped at five) in <see cref="MealReminderTimesJson"/>; the nullable
/// single-value fields hold either a <c>"HH:mm"</c> string or null.
/// </remarks>
public sealed class ReminderPreferences : EntityBase
{
    private const string TimeFormat = "HH:mm";

    /// <summary>Foreign key referencing the owning user's <c>Users.Id</c>. Unique.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Whether meal reminders are enabled for this user.</summary>
    public bool MealRemindersEnabled { get; private set; }

    /// <summary>Whether water reminders are enabled for this user.</summary>
    public bool WaterRemindersEnabled { get; private set; }

    /// <summary>
    /// JSON array of <c>"HH:mm"</c> strings (at most five) at which meal reminders fire. Defaults to
    /// an empty array. Read via <see cref="GetMealReminderTimes"/>, which is parse-safe.
    /// </summary>
    public string MealReminderTimesJson { get; private set; } = "[]";

    /// <summary>The <c>"HH:mm"</c> time the water reminder fires, or null when unset.</summary>
    public string? WaterReminderTime { get; private set; }

    /// <summary>The <c>"HH:mm"</c> start of the quiet-hours window, or null when unset.</summary>
    public string? QuietHoursStart { get; private set; }

    /// <summary>The <c>"HH:mm"</c> end of the quiet-hours window, or null when unset.</summary>
    public string? QuietHoursEnd { get; private set; }

    /// <summary>The user's offset from UTC, in minutes, used to compute their local time.</summary>
    public int UtcOffsetMinutes { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private ReminderPreferences() { }

    /// <summary>
    /// Creates a default-disabled preferences row for the given user. Audit timestamps are stamped
    /// here so the entity is fully initialized before it is added to the change tracker.
    /// </summary>
    /// <param name="userId">The owning user's internal <c>Users.Id</c>.</param>
    public static ReminderPreferences Create(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ReminderPreferences
        {
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Applies a full set of preference updates. The caller works in <c>TimeOnly</c>; this method
    /// serializes meal times to a JSON array of <c>"HH:mm"</c> strings and renders the nullable
    /// single-value fields via <c>ToString("HH:mm")</c> (null stays null).
    /// </summary>
    /// <param name="mealRemindersEnabled">Whether meal reminders are enabled.</param>
    /// <param name="waterRemindersEnabled">Whether water reminders are enabled.</param>
    /// <param name="mealReminderTimes">Meal reminder times (at most five).</param>
    /// <param name="waterReminderTime">Water reminder time, or null.</param>
    /// <param name="quietHoursStart">Quiet-hours start, or null.</param>
    /// <param name="quietHoursEnd">Quiet-hours end, or null.</param>
    /// <param name="utcOffsetMinutes">The user's UTC offset in minutes.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mealReminderTimes"/> has more than five entries.
    /// </exception>
    public void Update(
        bool mealRemindersEnabled,
        bool waterRemindersEnabled,
        IReadOnlyList<TimeOnly> mealReminderTimes,
        TimeOnly? waterReminderTime,
        TimeOnly? quietHoursStart,
        TimeOnly? quietHoursEnd,
        int utcOffsetMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mealReminderTimes.Count, 5);

        MealRemindersEnabled = mealRemindersEnabled;
        WaterRemindersEnabled = waterRemindersEnabled;

        var mealTimeStrings = mealReminderTimes
            .Select(t => t.ToString(TimeFormat, CultureInfo.InvariantCulture))
            .ToList();
        MealReminderTimesJson = JsonSerializer.Serialize(mealTimeStrings);

        WaterReminderTime = waterReminderTime?.ToString(TimeFormat, CultureInfo.InvariantCulture);
        QuietHoursStart = quietHoursStart?.ToString(TimeFormat, CultureInfo.InvariantCulture);
        QuietHoursEnd = quietHoursEnd?.ToString(TimeFormat, CultureInfo.InvariantCulture);

        UtcOffsetMinutes = utcOffsetMinutes;

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Parses <see cref="MealReminderTimesJson"/> back into <c>TimeOnly</c> values. Fully defensive:
    /// returns an empty list for null/blank JSON or on any deserialization/parse failure, so a
    /// corrupt persisted value can never throw on read.
    /// </summary>
    public IReadOnlyList<TimeOnly> GetMealReminderTimes()
    {
        if (string.IsNullOrWhiteSpace(MealReminderTimesJson))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(MealReminderTimesJson) ?? [];
            return raw
                .Select(s => TimeOnly.ParseExact(s, TimeFormat, CultureInfo.InvariantCulture))
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
