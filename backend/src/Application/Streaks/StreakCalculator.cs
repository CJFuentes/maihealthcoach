namespace MAIHealthCoach.Application.Streaks;

/// <summary>
/// Pure, stateless functions that derive logging streaks and adherence percentages from a user's
/// set of "active" calendar days and per-day consumption totals (issue #44). No external
/// dependencies, so it can be unit-tested by calling the static methods directly.
/// </summary>
/// <remarks>
/// <para><strong>UTC-day boundaries.</strong> All dates are interpreted as plain calendar days in
/// UTC (the same <see cref="DateOnly"/> values diary/water entries are persisted with). The caller
/// supplies <c>today</c> as <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>.</para>
/// <para><strong>Grace rule.</strong> The current streak is considered "alive" when the most recent
/// active day is <em>today or yesterday</em>. This forgives the not-yet-logged-today gap so a user
/// who logged yesterday but has not logged yet today still sees their streak. A most-recent active
/// day two or more days in the past breaks the streak (returns 0).</para>
/// <para><strong>Future clamp.</strong> Diary and water entries permit future dates. A most-recent
/// active day after <c>today</c> is clamped down to <c>today</c> before applying the grace rule and
/// walking backwards, so future-dated logs never inflate the current streak.</para>
/// <para><strong>Adherence bands.</strong> Calorie adherence counts a day as met when consumption
/// falls within ±15% of the target (<c>[target*0.85, target*1.15]</c> inclusive); a day with zero
/// consumption never meets a positive target. Water adherence counts a day as met when consumption
/// is at least the target. Both are reported as the percentage of days met over the window,
/// rounded to one decimal place (away-from-zero).</para>
/// </remarks>
public static class StreakCalculator
{
    /// <summary>
    /// Computes the current consecutive run of active days ending at <paramref name="today"/>
    /// (grace: today or yesterday). Returns 0 when there are no active days, when the most recent
    /// active day (clamped to <paramref name="today"/>) is more than one day in the past, otherwise
    /// the length of the unbroken run walking backwards from that most-recent day.
    /// </summary>
    /// <param name="activeDates">The distinct calendar days the user was active (diary or water).</param>
    /// <param name="today">The current UTC calendar day.</param>
    public static int ComputeCurrentStreak(IReadOnlyCollection<DateOnly> activeDates, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(activeDates);

        if (activeDates.Count == 0)
        {
            return 0;
        }

        var mostRecent = activeDates.Max();

        // Clamp a future-dated most-recent day down to today so future logs never inflate the streak.
        var effectiveMostRecent = mostRecent > today ? today : mostRecent;

        // Grace rule: the streak is alive only when the (clamped) most recent active day is today or
        // yesterday. Anything older means the streak has already been broken.
        if (effectiveMostRecent < today.AddDays(-1))
        {
            return 0;
        }

        var lookup = activeDates as ISet<DateOnly> ?? new HashSet<DateOnly>(activeDates);

        var streak = 0;
        var cursor = effectiveMostRecent;
        while (lookup.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    /// <summary>
    /// Computes the longest consecutive run of active days anywhere in the user's history. Returns
    /// 0 when there are no active days. Duplicate dates are de-duplicated before measuring.
    /// </summary>
    /// <param name="activeDates">The calendar days the user was active (diary or water).</param>
    public static int ComputeLongestStreak(IReadOnlyCollection<DateOnly> activeDates)
    {
        ArgumentNullException.ThrowIfNull(activeDates);

        if (activeDates.Count == 0)
        {
            return 0;
        }

        var sorted = activeDates.Distinct().OrderBy(d => d).ToList();

        var longest = 1;
        var current = 1;
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] == sorted[i - 1].AddDays(1))
            {
                current++;
                if (current > longest)
                {
                    longest = current;
                }
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }

    /// <summary>
    /// Computes calorie adherence over the trailing <paramref name="windowDays"/>-day window ending
    /// at <paramref name="today"/>: the percentage of days whose consumed calories fall within ±15%
    /// of <paramref name="targetKcal"/> (<c>[target*0.85, target*1.15]</c> inclusive). Days absent
    /// from the dictionary count as zero consumption and therefore never meet a positive target.
    /// Result is rounded to one decimal place (away-from-zero).
    /// </summary>
    /// <param name="dailyCaloriesConsumed">Consumed kcal keyed by calendar day.</param>
    /// <param name="targetKcal">The effective daily calorie target.</param>
    /// <param name="today">The current UTC calendar day (inclusive window end).</param>
    /// <param name="windowDays">Window length in days (e.g. 7 or 30).</param>
    public static decimal ComputeCaloriesAdherence(
        IReadOnlyDictionary<DateOnly, decimal> dailyCaloriesConsumed,
        int targetKcal,
        DateOnly today,
        int windowDays)
    {
        ArgumentNullException.ThrowIfNull(dailyCaloriesConsumed);

        var windowStart = today.AddDays(-(windowDays - 1));
        var lower = targetKcal * 0.85m;
        var upper = targetKcal * 1.15m;

        var daysMet = 0;
        for (var d = windowStart; d <= today; d = d.AddDays(1))
        {
            var consumed = dailyCaloriesConsumed.TryGetValue(d, out var c) ? c : 0m;
            if (consumed >= lower && consumed <= upper)
            {
                daysMet++;
            }
        }

        return Math.Round((decimal)daysMet / windowDays * 100m, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes water adherence over the trailing <paramref name="windowDays"/>-day window ending at
    /// <paramref name="today"/>: the percentage of days whose consumed water is at least
    /// <paramref name="targetWaterMl"/>. Days absent from the dictionary count as zero consumption.
    /// Result is rounded to one decimal place (away-from-zero).
    /// </summary>
    /// <param name="dailyWaterConsumed">Consumed millilitres keyed by calendar day.</param>
    /// <param name="targetWaterMl">The effective daily water target in millilitres.</param>
    /// <param name="today">The current UTC calendar day (inclusive window end).</param>
    /// <param name="windowDays">Window length in days (e.g. 7 or 30).</param>
    public static decimal ComputeWaterAdherence(
        IReadOnlyDictionary<DateOnly, int> dailyWaterConsumed,
        int targetWaterMl,
        DateOnly today,
        int windowDays)
    {
        ArgumentNullException.ThrowIfNull(dailyWaterConsumed);

        var windowStart = today.AddDays(-(windowDays - 1));

        var daysMet = 0;
        for (var d = windowStart; d <= today; d = d.AddDays(1))
        {
            var consumed = dailyWaterConsumed.TryGetValue(d, out var c) ? c : 0;
            if (consumed >= targetWaterMl)
            {
                daysMet++;
            }
        }

        return Math.Round((decimal)daysMet / windowDays * 100m, 1, MidpointRounding.AwayFromZero);
    }
}
