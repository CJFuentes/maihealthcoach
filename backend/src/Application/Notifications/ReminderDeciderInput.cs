namespace MAIHealthCoach.Application.Notifications;

/// <summary>
/// The complete, pre-resolved input to a single reminder evaluation (issue #48). Everything the
/// decider needs is materialized here in the caller's local time, so <see cref="ReminderDecider"/>
/// stays pure: no clock, no database, no time-zone math.
/// </summary>
/// <param name="UserId">The user this evaluation is for (carried through for the caller's benefit).</param>
/// <param name="MealRemindersEnabled">Whether meal reminders are enabled.</param>
/// <param name="WaterRemindersEnabled">Whether water reminders are enabled.</param>
/// <param name="MealReminderTimes">Scheduled meal reminder times, in the user's local time.</param>
/// <param name="WaterReminderTime">Scheduled water reminder time, or null when unset.</param>
/// <param name="QuietHoursStart">Quiet-hours window start, or null when no window is set.</param>
/// <param name="QuietHoursEnd">Quiet-hours window end, or null when no window is set.</param>
/// <param name="HasLoggedMealToday">Whether the user has already logged a meal today (suppresses meal reminders).</param>
/// <param name="HasLoggedWaterToday">Whether the user has already logged water today (suppresses water reminders).</param>
/// <param name="LocalNow">The user's current local time-of-day.</param>
public sealed record ReminderDeciderInput(
    Guid UserId,
    bool MealRemindersEnabled,
    bool WaterRemindersEnabled,
    IReadOnlyList<TimeOnly> MealReminderTimes,
    TimeOnly? WaterReminderTime,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    bool HasLoggedMealToday,
    bool HasLoggedWaterToday,
    TimeOnly LocalNow);

/// <summary>
/// The decision produced by <see cref="ReminderDecider.Evaluate"/>: whether each reminder kind is
/// due right now for the evaluated user.
/// </summary>
/// <param name="MealReminderDue">True when a meal reminder should be sent now.</param>
/// <param name="WaterReminderDue">True when a water reminder should be sent now.</param>
public sealed record ReminderDeciderOutput(
    bool MealReminderDue,
    bool WaterReminderDue);
