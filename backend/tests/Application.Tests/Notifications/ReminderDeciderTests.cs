using MAIHealthCoach.Application.Notifications;

namespace MAIHealthCoach.Application.Tests.Notifications;

/// <summary>
/// Unit tests for the pure <see cref="ReminderDecider.Evaluate"/> decision logic (issue #48). The
/// decider owns the heart of the story's acceptance criteria: given a fully-resolved
/// <see cref="ReminderDeciderInput"/> (already in the user's local time, with the "already logged
/// today" flags pre-computed), it decides whether a meal and/or water reminder is due right now. Each
/// test constructs an input record directly and asserts the boolean outputs, so the branch coverage is
/// exhaustive without any clock, database, or time-zone dependency.
/// </summary>
public sealed class ReminderDeciderTests
{
    private static TimeOnly T(int hh, int mm) => new(hh, mm);

    /// <summary>
    /// Builds an input with sensible "everything off" defaults so each test overrides only the fields
    /// it cares about, keeping the multi-field scenarios readable.
    /// </summary>
    private static ReminderDeciderInput Input(
        bool mealEnabled = false,
        bool waterEnabled = false,
        IReadOnlyList<TimeOnly>? mealTimes = null,
        TimeOnly? waterTime = null,
        TimeOnly? quietStart = null,
        TimeOnly? quietEnd = null,
        bool loggedMeal = false,
        bool loggedWater = false,
        TimeOnly localNow = default) =>
        new(
            UserId: Guid.NewGuid(),
            MealRemindersEnabled: mealEnabled,
            WaterRemindersEnabled: waterEnabled,
            MealReminderTimes: mealTimes ?? [],
            WaterReminderTime: waterTime,
            QuietHoursStart: quietStart,
            QuietHoursEnd: quietEnd,
            HasLoggedMealToday: loggedMeal,
            HasLoggedWaterToday: loggedWater,
            LocalNow: localNow);

    // ── Meal reminder: due / suppressed branches ─────────────────────────────────

    [Fact]
    public void Evaluate_MealEnabledNotLoggedWithinWindowNoQuietHours_MealDue()
    {
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(9, 0)],
            localNow: T(9, 0));

        var output = ReminderDecider.Evaluate(input);

        Assert.True(output.MealReminderDue);
        Assert.False(output.WaterReminderDue);
    }

    [Fact]
    public void Evaluate_MealAlreadyLoggedToday_MealNotDue()
    {
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(9, 0)],
            loggedMeal: true,
            localNow: T(9, 0));

        Assert.False(ReminderDecider.Evaluate(input).MealReminderDue);
    }

    [Fact]
    public void Evaluate_MealRemindersDisabled_MealNotDue()
    {
        var input = Input(
            mealEnabled: false,
            mealTimes: [T(9, 0)],
            localNow: T(9, 0));

        Assert.False(ReminderDecider.Evaluate(input).MealReminderDue);
    }

    [Fact]
    public void Evaluate_MealWithinQuietHours_MealNotDue()
    {
        // Quiet window 08:00-10:00 covers the 09:00 schedule, so no reminder fires.
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(9, 0)],
            quietStart: T(8, 0),
            quietEnd: T(10, 0),
            localNow: T(9, 0));

        Assert.False(ReminderDecider.Evaluate(input).MealReminderDue);
    }

    [Fact]
    public void Evaluate_MealOutsideAllScheduledWindows_MealNotDue()
    {
        // Scheduled 09:00, now 10:30 — well past the 5-minute match window.
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(9, 0)],
            localNow: T(10, 30));

        Assert.False(ReminderDecider.Evaluate(input).MealReminderDue);
    }

    [Fact]
    public void Evaluate_MealMultipleTimesMatchesSecond_MealDue()
    {
        // Two scheduled times; "now" matches the second one — any match suffices.
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(8, 0), T(12, 30)],
            localNow: T(12, 31));

        Assert.True(ReminderDecider.Evaluate(input).MealReminderDue);
    }

    // ── Match-window boundary: window is [0, 5) minutes after the scheduled time ──

    [Theory]
    [InlineData(9, 0, true)]   // exactly on the scheduled minute → due
    [InlineData(9, 4, true)]   // 4 minutes after → still inside [0, 5)
    [InlineData(9, 5, false)]  // 5 minutes after → window is half-open, NOT due
    [InlineData(8, 59, false)] // 1 minute before → forward-only window, NOT due
    public void Evaluate_MealMatchWindowBoundary_RespectsHalfOpenFiveMinuteWindow(
        int nowHour, int nowMinute, bool expectedDue)
    {
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(9, 0)],
            localNow: T(nowHour, nowMinute));

        Assert.Equal(expectedDue, ReminderDecider.Evaluate(input).MealReminderDue);
    }

    [Fact]
    public void Evaluate_MealScheduledNearMidnightNowJustAfter_MealDue()
    {
        // Cross-midnight match: scheduled 23:58, now 00:01 → 3 minutes after, inside the window.
        // Verifies the modular minute arithmetic wraps the day boundary.
        var input = Input(
            mealEnabled: true,
            mealTimes: [T(23, 58)],
            localNow: T(0, 1));

        Assert.True(ReminderDecider.Evaluate(input).MealReminderDue);
    }

    // ── Water reminder: symmetric set of branches ────────────────────────────────

    [Fact]
    public void Evaluate_WaterEnabledNotLoggedWithinWindowNoQuietHours_WaterDue()
    {
        var input = Input(
            waterEnabled: true,
            waterTime: T(10, 0),
            localNow: T(10, 0));

        var output = ReminderDecider.Evaluate(input);

        Assert.True(output.WaterReminderDue);
        Assert.False(output.MealReminderDue);
    }

    [Fact]
    public void Evaluate_WaterAlreadyLoggedToday_WaterNotDue()
    {
        var input = Input(
            waterEnabled: true,
            waterTime: T(10, 0),
            loggedWater: true,
            localNow: T(10, 0));

        Assert.False(ReminderDecider.Evaluate(input).WaterReminderDue);
    }

    [Fact]
    public void Evaluate_WaterRemindersDisabled_WaterNotDue()
    {
        var input = Input(
            waterEnabled: false,
            waterTime: T(10, 0),
            localNow: T(10, 0));

        Assert.False(ReminderDecider.Evaluate(input).WaterReminderDue);
    }

    [Fact]
    public void Evaluate_WaterWithinQuietHours_WaterNotDue()
    {
        var input = Input(
            waterEnabled: true,
            waterTime: T(10, 0),
            quietStart: T(9, 0),
            quietEnd: T(11, 0),
            localNow: T(10, 0));

        Assert.False(ReminderDecider.Evaluate(input).WaterReminderDue);
    }

    [Fact]
    public void Evaluate_WaterReminderTimeNullEvenWhenEnabled_WaterNotDue()
    {
        // Enabled but no time configured → nothing to match against, so never due.
        var input = Input(
            waterEnabled: true,
            waterTime: null,
            localNow: T(10, 0));

        Assert.False(ReminderDecider.Evaluate(input).WaterReminderDue);
    }

    [Fact]
    public void Evaluate_WaterOutsideWindow_WaterNotDue()
    {
        var input = Input(
            waterEnabled: true,
            waterTime: T(10, 0),
            localNow: T(15, 0));

        Assert.False(ReminderDecider.Evaluate(input).WaterReminderDue);
    }

    // ── Quiet hours: same-day window and the cross-midnight window ────────────────

    [Theory]
    // Same-day quiet window 13:00-14:00.
    [InlineData(13, 0, true)]   // start is inclusive → quiet
    [InlineData(13, 30, true)]  // mid-window → quiet
    [InlineData(14, 0, false)]  // end is exclusive → not quiet
    [InlineData(12, 59, false)] // before start → not quiet
    public void Evaluate_SameDayQuietWindow_SuppressesOnlyInsideHalfOpenRange(
        int nowHour, int nowMinute, bool expectedQuiet)
    {
        // Schedule the meal exactly at "now" so the only thing that can suppress it is quiet hours.
        var now = T(nowHour, nowMinute);
        var input = Input(
            mealEnabled: true,
            mealTimes: [now],
            quietStart: T(13, 0),
            quietEnd: T(14, 0),
            localNow: now);

        // Quiet ⇒ not due; not quiet ⇒ due (everything else is satisfied).
        Assert.Equal(!expectedQuiet, ReminderDecider.Evaluate(input).MealReminderDue);
    }

    [Theory]
    // Cross-midnight quiet window 22:00-07:00.
    [InlineData(23, 0, true)]  // 23:00 is after start → quiet
    [InlineData(2, 0, true)]   // 02:00 is before end → quiet
    [InlineData(22, 0, true)]  // start is inclusive → quiet
    [InlineData(8, 0, false)]  // 08:00 is past the 07:00 end → not quiet, reminder may fire
    [InlineData(7, 0, false)]  // end is exclusive → not quiet
    public void Evaluate_CrossMidnightQuietWindow_SuppressesAcrossTheMidnightBoundary(
        int nowHour, int nowMinute, bool expectedQuiet)
    {
        var now = T(nowHour, nowMinute);
        var input = Input(
            mealEnabled: true,
            mealTimes: [now],
            quietStart: T(22, 0),
            quietEnd: T(7, 0),
            localNow: now);

        Assert.Equal(!expectedQuiet, ReminderDecider.Evaluate(input).MealReminderDue);
    }

    // ── Both kinds due in a single evaluation ────────────────────────────────────

    [Fact]
    public void Evaluate_MealAndWaterBothDue_BothFlagsTrue()
    {
        var input = Input(
            mealEnabled: true,
            waterEnabled: true,
            mealTimes: [T(8, 0)],
            waterTime: T(8, 0),
            localNow: T(8, 0));

        var output = ReminderDecider.Evaluate(input);

        Assert.True(output.MealReminderDue);
        Assert.True(output.WaterReminderDue);
    }

    [Fact]
    public void Evaluate_NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReminderDecider.Evaluate(null!));
    }
}
