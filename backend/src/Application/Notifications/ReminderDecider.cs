namespace MAIHealthCoach.Application.Notifications;

/// <summary>
/// Pure decision logic for push reminders (issue #48). Given a fully-resolved
/// <see cref="ReminderDeciderInput"/> (already in the user's local time, with "already logged today"
/// flags pre-computed), decides whether a meal and/or water reminder is due right now. Deliberately
/// free of any clock, database, or time-zone dependency so it is trivially unit-testable; the caller
/// (the background service) owns all I/O and time-zone resolution.
/// </summary>
public static class ReminderDecider
{
    /// <summary>
    /// How close (in minutes) the current local time must be at or after a scheduled time for it to
    /// "match". A non-zero window absorbs the background tick's coarse granularity so a reminder is
    /// not missed when the tick lands a few minutes past the scheduled minute.
    /// </summary>
    public const int MatchWindowMinutes = 5;

    /// <summary>
    /// Evaluates whether each reminder kind is due. A reminder is due when its kind is enabled, the
    /// user has not already logged that kind today, the current local time is not within quiet hours,
    /// and the current time matches a scheduled time within <see cref="MatchWindowMinutes"/>.
    /// </summary>
    /// <param name="input">The fully-resolved evaluation input.</param>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    public static ReminderDeciderOutput Evaluate(ReminderDeciderInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var inQuietHours = IsInQuietHours(input.LocalNow, input.QuietHoursStart, input.QuietHoursEnd);

        var mealDue =
            input.MealRemindersEnabled
            && !input.HasLoggedMealToday
            && !inQuietHours
            && IsTimeMatch(input.LocalNow, input.MealReminderTimes);

        var waterDue =
            input.WaterRemindersEnabled
            && !input.HasLoggedWaterToday
            && !inQuietHours
            && input.WaterReminderTime.HasValue
            && IsTimeMatch(input.LocalNow, [input.WaterReminderTime.Value]);

        return new ReminderDeciderOutput(mealDue, waterDue);
    }

    /// <summary>
    /// Returns true when <paramref name="now"/> falls inside the quiet-hours window. A window with a
    /// null start or end is treated as "no quiet hours" (false). A same-day window (start &lt;= end)
    /// matches <c>[start, end)</c>; a window that crosses midnight (start &gt; end) matches
    /// <c>now &gt;= start || now &lt; end</c>.
    /// </summary>
    private static bool IsInQuietHours(TimeOnly now, TimeOnly? start, TimeOnly? end)
    {
        if (start is null || end is null)
        {
            return false;
        }

        var s = start.Value;
        var e = end.Value;

        return s <= e
            ? now >= s && now < e
            : now >= s || now < e;
    }

    /// <summary>
    /// Returns true when <paramref name="now"/> is at or within <see cref="MatchWindowMinutes"/>
    /// minutes after any scheduled time. The forward-only window is computed modulo a 24-hour day so
    /// a schedule near midnight still matches just after it rolls over.
    /// </summary>
    private static bool IsTimeMatch(TimeOnly now, IReadOnlyList<TimeOnly> scheduled)
    {
        var nowMins = now.Hour * 60 + now.Minute;

        foreach (var t in scheduled)
        {
            var tMins = t.Hour * 60 + t.Minute;

            // Forward-only minute distance from the scheduled time to now, modulo a 24-hour day. The
            // double-modulo normalises into [0, 1439], so a schedule near midnight still matches just
            // after it rolls over (e.g. 23:58 scheduled, 00:01 now => 3).
            var delta = ((nowMins - tMins) % 1440 + 1440) % 1440;
            if (delta < MatchWindowMinutes)
            {
                return true;
            }
        }

        return false;
    }
}
