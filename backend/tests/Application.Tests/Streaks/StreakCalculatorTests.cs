using MAIHealthCoach.Application.Streaks;

namespace MAIHealthCoach.Application.Tests.Streaks;

/// <summary>
/// Unit tests for <see cref="StreakCalculator"/>. Each test calls the static methods directly and
/// asserts against hand-computed reference values. <c>Today</c> is fixed at 2026-06-25 so the
/// window and grace-rule arithmetic is deterministic.
/// </summary>
public sealed class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 6, 25);

    private static DateOnly D(int dayOffset) => Today.AddDays(dayOffset);

    // ── ComputeCurrentStreak ──────────────────────────────────────────────────────

    [Fact]
    public void CurrentStreak_Empty_ReturnsZero()
    {
        Assert.Equal(0, StreakCalculator.ComputeCurrentStreak(Array.Empty<DateOnly>(), Today));
    }

    [Fact]
    public void CurrentStreak_OnlyToday_ReturnsOne()
    {
        var dates = new[] { Today };
        Assert.Equal(1, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_TodayAndYesterday_ReturnsTwo()
    {
        var dates = new[] { D(-1), Today };
        Assert.Equal(2, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_OnlyYesterday_ReturnsOne_GraceRule()
    {
        var dates = new[] { D(-1) };
        Assert.Equal(1, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_MostRecentTwoDaysAgo_ReturnsZero()
    {
        var dates = new[] { D(-2), D(-3) };
        Assert.Equal(0, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_GapInMiddle_CountsOnlyRecentRun()
    {
        // Recent run: today, yesterday. Older run (separated by a gap at -2) is ignored.
        var dates = new[] { D(-5), D(-4), D(-1), Today };
        Assert.Equal(2, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_FutureDateOnly_ReturnsZero_Clamp()
    {
        // A lone future log clamps to today; today itself is not active, so the streak is 0.
        var dates = new[] { D(3) };
        Assert.Equal(0, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_FuturePlusTodayAndYesterday_ReturnsTwo_Clamp()
    {
        // Future date is clamped to today; the run today + yesterday is counted as 2.
        var dates = new[] { D(5), D(-1), Today };
        Assert.Equal(2, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    [Fact]
    public void CurrentStreak_TenDayUnbrokenRunEndingToday_ReturnsTen()
    {
        var dates = Enumerable.Range(0, 10).Select(i => D(-i)).ToArray();
        Assert.Equal(10, StreakCalculator.ComputeCurrentStreak(dates, Today));
    }

    // ── ComputeLongestStreak ──────────────────────────────────────────────────────

    [Fact]
    public void LongestStreak_Empty_ReturnsZero()
    {
        Assert.Equal(0, StreakCalculator.ComputeLongestStreak(Array.Empty<DateOnly>()));
    }

    [Fact]
    public void LongestStreak_Single_ReturnsOne()
    {
        Assert.Equal(1, StreakCalculator.ComputeLongestStreak(new[] { Today }));
    }

    [Fact]
    public void LongestStreak_TwoRuns_ReturnsLongest()
    {
        // Run A: -20..-16 (5 days). Run B: -2..-1 (2 days). Longest = 5.
        var runA = Enumerable.Range(16, 5).Select(i => D(-i));      // -20, -19, -18, -17, -16
        var runB = new[] { D(-2), D(-1) };
        var dates = runA.Concat(runB).ToArray();
        Assert.Equal(5, StreakCalculator.ComputeLongestStreak(dates));
    }

    [Fact]
    public void LongestStreak_AllConsecutiveSeven_ReturnsSeven()
    {
        var dates = Enumerable.Range(0, 7).Select(i => D(-i)).ToArray();
        Assert.Equal(7, StreakCalculator.ComputeLongestStreak(dates));
    }

    [Fact]
    public void LongestStreak_WithDuplicates_DedupsToThree()
    {
        // -2, -1, 0 with duplicates → longest consecutive run is 3.
        var dates = new[] { D(-2), D(-1), Today, Today, D(-1) };
        Assert.Equal(3, StreakCalculator.ComputeLongestStreak(dates));
    }

    // ── ComputeCaloriesAdherence ──────────────────────────────────────────────────

    [Fact]
    public void CaloriesAdherence_AllDaysMet_ReturnsHundred()
    {
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = target;
        }

        Assert.Equal(100.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_NoneMet_ReturnsZero()
    {
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = 5000m; // well above the +15% band.
        }

        Assert.Equal(0.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_ZeroConsumption_NotMet_ReturnsZero()
    {
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>(); // every day absent → 0 kcal.
        Assert.Equal(0.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_ThreeOfSeven_Returns42Point9()
    {
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>
        {
            [D(0)] = target,
            [D(-1)] = target,
            [D(-2)] = target,
        };

        Assert.Equal(42.9m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_LowerBoundInclusive_ReturnsHundred()
    {
        // target 2000 → lower bound 1700 (2000*0.85), inclusive.
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = 1700m;
        }

        Assert.Equal(100.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_UpperBoundInclusive_ReturnsHundred()
    {
        // target 2000 → upper bound 2300 (2000*1.15), inclusive.
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = 2300m;
        }

        Assert.Equal(100.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_JustBelowLowerBound_ReturnsZero()
    {
        // 1699.99 is below the 1700 lower bound → not met.
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = 1699.99m;
        }

        Assert.Equal(0.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void CaloriesAdherence_ThirtyDayWindow_AllMet_ReturnsHundred()
    {
        const int target = 2000;
        var consumed = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < 30; i++)
        {
            consumed[D(-i)] = target;
        }

        Assert.Equal(100.0m, StreakCalculator.ComputeCaloriesAdherence(consumed, target, Today, 30));
    }

    // ── ComputeWaterAdherence ─────────────────────────────────────────────────────

    [Fact]
    public void WaterAdherence_AllDaysMet_ReturnsHundred()
    {
        const int target = 3000;
        var consumed = new Dictionary<DateOnly, int>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = 4000;
        }

        Assert.Equal(100.0m, StreakCalculator.ComputeWaterAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void WaterAdherence_NoneMet_ReturnsZero()
    {
        const int target = 3000;
        var consumed = new Dictionary<DateOnly, int>(); // every day absent → 0 ml.
        Assert.Equal(0.0m, StreakCalculator.ComputeWaterAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void WaterAdherence_ExactlyTargetMet_ReturnsHundred()
    {
        const int target = 3000;
        var consumed = new Dictionary<DateOnly, int>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = target; // >= target is met.
        }

        Assert.Equal(100.0m, StreakCalculator.ComputeWaterAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void WaterAdherence_OneLessThanTarget_NotMet_ReturnsZero()
    {
        const int target = 3000;
        var consumed = new Dictionary<DateOnly, int>();
        for (var i = 0; i < 7; i++)
        {
            consumed[D(-i)] = target - 1;
        }

        Assert.Equal(0.0m, StreakCalculator.ComputeWaterAdherence(consumed, target, Today, 7));
    }

    [Fact]
    public void WaterAdherence_FourOfSeven_Returns57Point1()
    {
        const int target = 3000;
        var consumed = new Dictionary<DateOnly, int>
        {
            [D(0)] = target,
            [D(-1)] = target,
            [D(-2)] = target,
            [D(-3)] = target,
        };

        Assert.Equal(57.1m, StreakCalculator.ComputeWaterAdherence(consumed, target, Today, 7));
    }
}
