namespace MAIHealthCoach.Api.Features.Notifications;

/// <summary>
/// Request body for upserting a user's push-reminder preferences (issue #48). All time-of-day values
/// are <c>"HH:mm"</c> strings, validated and parsed in the handler. Quiet hours must be supplied as a
/// pair (both or neither).
/// </summary>
/// <param name="MealRemindersEnabled">Whether meal reminders are enabled.</param>
/// <param name="WaterRemindersEnabled">Whether water reminders are enabled.</param>
/// <param name="MealReminderTimes">Meal reminder times as "HH:mm" strings (at most five); null means none.</param>
/// <param name="WaterReminderTime">Water reminder time as "HH:mm", or null.</param>
/// <param name="QuietHoursStart">Quiet-hours start as "HH:mm", or null.</param>
/// <param name="QuietHoursEnd">Quiet-hours end as "HH:mm", or null.</param>
/// <param name="UtcOffsetMinutes">The user's UTC offset in minutes, in [-840, 840].</param>
public record UpdateReminderPreferencesRequest(
    bool MealRemindersEnabled,
    bool WaterRemindersEnabled,
    IReadOnlyList<string>? MealReminderTimes,
    string? WaterReminderTime,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    int UtcOffsetMinutes);

/// <summary>
/// API representation of a user's push-reminder preferences (issue #48). Time-of-day values are
/// emitted as <c>"HH:mm"</c> strings.
/// </summary>
/// <param name="Id">The preferences row id; <see cref="System.Guid.Empty"/> for the synthetic default when none is stored.</param>
/// <param name="MealRemindersEnabled">Whether meal reminders are enabled.</param>
/// <param name="WaterRemindersEnabled">Whether water reminders are enabled.</param>
/// <param name="MealReminderTimes">Meal reminder times as "HH:mm" strings.</param>
/// <param name="WaterReminderTime">Water reminder time as "HH:mm", or null.</param>
/// <param name="QuietHoursStart">Quiet-hours start as "HH:mm", or null.</param>
/// <param name="QuietHoursEnd">Quiet-hours end as "HH:mm", or null.</param>
/// <param name="UtcOffsetMinutes">The user's UTC offset in minutes.</param>
/// <param name="CreatedAt">UTC instant the row was created (default when synthetic).</param>
/// <param name="UpdatedAt">UTC instant the row was last updated (default when synthetic).</param>
public record ReminderPreferencesResponse(
    Guid Id,
    bool MealRemindersEnabled,
    bool WaterRemindersEnabled,
    IReadOnlyList<string> MealReminderTimes,
    string? WaterReminderTime,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    int UtcOffsetMinutes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
