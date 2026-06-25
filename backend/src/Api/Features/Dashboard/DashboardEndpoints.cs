using MAIHealthCoach.Api.Features.Diary;
using MAIHealthCoach.Api.Features.Goals;
using MAIHealthCoach.Api.Features.Summary;
using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Application.Streaks;
using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Dashboard;

/// <summary>
/// Registers the daily dashboard aggregate endpoint on the supplied versioned route builder
/// (issue #42).
/// <list type="bullet">
///   <item><description>
///   <c>GET /me/dashboard?date=</c> — a consolidated daily snapshot (calories + macros, water,
///   exercise, net calories, and streaks/adherence) for the authenticated user.
///   </description></item>
/// </list>
/// The endpoint requires authorization and scopes every query to the current user's id. It
/// <b>composes</b> the same computations the per-feature endpoints use rather than duplicating the
/// math, so its numbers reconcile exactly with <c>/me/summary</c>, <c>/me/water</c>,
/// <c>/me/exercise</c>, and <c>/me/streaks</c>. The optional <c>date</c> query parameter defaults to
/// today (server UTC) when omitted, and is bound as a nullable string to dodge the minimal-API
/// "required value-type query param" gotcha.
/// </summary>
internal static class DashboardEndpoints
{
    internal static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/dashboard", GetDashboardAsync)
            .WithName("GetDashboard")
            .RequireAuthorization();

        return group;
    }

    // ── GET /api/v1/me/dashboard?date=YYYY-MM-DD ─────────────────────────────────────

    private static async Task<IResult> GetDashboardAsync(
        string? date,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        // Optional date: default to today when omitted/blank, 400 on a malformed value. Bound as a
        // nullable string so an omitted param does not 400 on the value-type binding rules.
        DateOnly dashboardDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            dashboardDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DiaryEntryValidator.TryParseDate(date, out dashboardDate))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["date"] =
                    [
                        $"The 'date' query parameter must be a valid calendar date in " +
                        $"{DiaryEntryValidator.DateFormat} format.",
                    ],
                });
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── Targets: compute the user's goals when the profile is complete ───────────
        var profile = await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        GoalsCalculatorOutput? targets = null;
        if (ProfileGoalsMapper.BuildCalculatorInput(profile) is { } input)
        {
            var computed = calculator.Compute(input);

            var overrides = await db.UserGoalTargets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

            // Layer overrides on top of the computed targets (override wins when present), so the
            // dashboard compares against the same effective targets GET /me/goals & /me/summary use.
            targets = ApplyOverrides(computed, overrides);
        }

        // ── Calories + macros for the date (same aggregation as SummaryEndpoints) ────
        // FoodItem + ServingSize are eager-loaded so nutrition is recomputed from the latest food
        // data (never snapshotted), exactly as the diary/summary endpoints do.
        var entries = await db.DiaryEntries
            .AsNoTracking()
            .Include(e => e.FoodItem)
            .Include(e => e.ServingSize)
            .Where(e => e.UserId == user.Id && e.Date == dashboardDate)
            .ToListAsync(ct);

        var consumed = AggregateDay(entries);

        var calories = new DashboardCalories(
            Calories: MakeNutrient(consumed.EnergyKcal, targets?.CaloriesKcal),
            ProteinG: MakeNutrient(consumed.ProteinG, targets?.ProteinGrams),
            CarbohydrateG: MakeNutrient(consumed.CarbohydrateG, targets?.CarbohydrateGrams),
            FatG: MakeNutrient(consumed.FatG, targets?.FatGrams),
            EntryCount: entries.Count);

        // ── Water for the date (sum + goal resolution, same as WaterEndpoints) ───────
        var consumedMl = await db.WaterLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Date == dashboardDate)
            .SumAsync(e => e.AmountMl, ct);

        var goalMl = targets?.WaterMl;
        var water = new DashboardWater(
            GoalsAvailable: goalMl.HasValue,
            ConsumedMl: consumedMl,
            GoalMl: goalMl,
            RemainingMl: goalMl.HasValue ? goalMl.Value - consumedMl : null);

        // ── Exercise for the date (calories burned sum + count, same as ExerciseEndpoints) ──
        var exerciseEntries = await db.ExerciseLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Date == dashboardDate)
            .ToListAsync(ct);

        var totalCaloriesBurned = exerciseEntries.Sum(e => e.CaloriesBurned);
        var exercise = new DashboardExercise(
            TotalCaloriesBurned: totalCaloriesBurned,
            EntryCount: exerciseEntries.Count);

        // ── Net calories: consumed − burned, rounded; null only when both sides empty ─
        // Net is meaningless with no consumption AND no exercise, so it is null in that case only.
        int? netCalories = entries.Count == 0 && exerciseEntries.Count == 0
            ? null
            : (int)Math.Round(consumed.EnergyKcal - totalCaloriesBurned, MidpointRounding.AwayFromZero);

        // ── Streaks + 7-day adherence (same derivation as StreaksEndpoints) ──────────
        var streak = await BuildStreakAsync(user.Id, today, targets, db, ct);

        var response = new DashboardResponse(
            Date: dashboardDate.ToString(DiaryEntryValidator.DateFormat),
            GoalsAvailable: targets is not null,
            Calories: calories,
            Water: water,
            Exercise: exercise,
            NetCalories: netCalories,
            Streak: streak);

        return Results.Ok(response);
    }

    // ── Streaks + adherence (mirrors StreaksEndpoints, 7-day adherence only) ─────────

    /// <summary>
    /// Builds the streak block: current/longest active-day streak (active = any diary OR water entry
    /// that day) plus the trailing-7-day calorie and water adherence. Adherence is
    /// <see langword="null"/> when <paramref name="targets"/> is <see langword="null"/> (incomplete
    /// profile). The active-day set, the 7-day window query, and the adherence calls mirror
    /// <c>StreaksEndpoints</c> precisely so the figures reconcile.
    /// </summary>
    private static async Task<DashboardStreak> BuildStreakAsync(
        Guid userId,
        DateOnly today,
        GoalsCalculatorOutput? targets,
        AppDbContext db,
        CancellationToken ct)
    {
        // Active days: a day counts when it has any diary OR any water entry.
        var diaryDates = await db.DiaryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.Date)
            .ToListAsync(ct);

        var waterDates = await db.WaterLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Select(e => e.Date)
            .ToListAsync(ct);

        var activeDates = new HashSet<DateOnly>(diaryDates);
        foreach (var d in waterDates)
        {
            activeDates.Add(d);
        }

        var currentStreak = StreakCalculator.ComputeCurrentStreak(activeDates, today);
        var longestStreak = StreakCalculator.ComputeLongestStreak(activeDates);

        decimal? cal7 = null, wat7 = null;

        if (targets is not null)
        {
            // Trailing 7-day window ending today (inclusive), same shape as StreaksEndpoints.
            var windowStart = today.AddDays(-6);

            var diaryRows = await db.DiaryEntries
                .AsNoTracking()
                .Include(e => e.FoodItem)
                .Include(e => e.ServingSize)
                .Where(e => e.UserId == userId && e.Date >= windowStart && e.Date <= today)
                .ToListAsync(ct);

            var dailyCalories = new Dictionary<DateOnly, decimal>();
            foreach (var entry in diaryRows)
            {
                if (!TryScaleEntry(entry, out var nutrition))
                {
                    // Defensive: a row whose food/serving navigation failed to materialize
                    // contributes nothing rather than throwing a 500.
                    continue;
                }

                dailyCalories[entry.Date] =
                    (dailyCalories.TryGetValue(entry.Date, out var existing) ? existing : 0m)
                    + nutrition.EnergyKcal;
            }

            var waterRows = await db.WaterLogEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.Date >= windowStart && e.Date <= today)
                .ToListAsync(ct);

            var dailyWater = waterRows
                .GroupBy(e => e.Date)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountMl));

            cal7 = StreakCalculator.ComputeCaloriesAdherence(dailyCalories, targets.CaloriesKcal, today, 7);
            wat7 = StreakCalculator.ComputeWaterAdherence(dailyWater, targets.WaterMl, today, 7);
        }

        return new DashboardStreak(currentStreak, longestStreak, cal7, wat7);
    }

    // ── Consumed aggregation (single pass, totals only — mirrors SummaryEndpoints) ───

    private readonly record struct ConsumedTotals(
        decimal EnergyKcal,
        decimal ProteinG,
        decimal CarbohydrateG,
        decimal FatG);

    /// <summary>
    /// Walks the day's entries exactly once, recomputing each entry's nutrition a single time and
    /// folding it into the grand totals. A simplified form of <c>SummaryEndpoints.AggregateDay</c>
    /// that returns only the day totals (the dashboard does not need the per-meal breakdown), so the
    /// calorie and macro numbers reconcile exactly with <c>GET /me/summary</c>.
    /// </summary>
    private static ConsumedTotals AggregateDay(IReadOnlyList<DiaryEntry> entries)
    {
        decimal totalEnergy = 0m, totalProtein = 0m, totalCarbs = 0m, totalFat = 0m;

        foreach (var entry in entries)
        {
            if (!TryScaleEntry(entry, out var nutrition))
            {
                // Defensive: an entry whose food/serving navigation failed to materialize (e.g. a
                // data-integrity anomaly) contributes nothing rather than throwing a 500.
                continue;
            }

            totalEnergy += nutrition.EnergyKcal;
            totalProtein += nutrition.ProteinG;
            totalCarbs += nutrition.CarbohydrateG;
            totalFat += nutrition.FatG;
        }

        return new ConsumedTotals(totalEnergy, totalProtein, totalCarbs, totalFat);
    }

    /// <summary>
    /// Recomputes an entry's absolute nutrition from its food and serving:
    /// <c>NutritionPer100g.ScaleToGrams(serving.GramsEquivalent × quantity)</c>. Returns
    /// <see langword="false"/> (rather than throwing) when the eager-loaded food/serving data is
    /// unexpectedly absent, so a single bad row degrades gracefully instead of failing the request.
    /// </summary>
    private static bool TryScaleEntry(DiaryEntry entry, out NutritionFacts nutrition)
    {
        if (entry.FoodItem?.NutritionPer100g is not { } per100g || entry.ServingSize is null)
        {
            nutrition = null!;
            return false;
        }

        var gramsConsumed = entry.ServingSize.GramsEquivalent * entry.Quantity;
        nutrition = per100g.ScaleToGrams(gramsConsumed);
        return true;
    }

    // ── Nutrient line construction (identical to SummaryEndpoints) ───────────────────

    /// <summary>
    /// Builds a <see cref="NutrientSummary"/> from a consumed total and an optional integer target.
    /// When no target is available, only <c>Consumed</c> is populated. <c>Remaining</c> is
    /// <c>target − consumed</c> (may be negative); <c>PercentOfTarget</c> is the consumed fraction
    /// of the target rounded to one decimal, and is <see langword="null"/> when the target is zero.
    /// </summary>
    private static NutrientSummary MakeNutrient(decimal consumed, int? target)
    {
        if (target is not { } t)
        {
            return new NutrientSummary(consumed, Target: null, Remaining: null, PercentOfTarget: null);
        }

        decimal? percent = t == 0
            ? null
            : Math.Round(consumed / t * 100m, 1, MidpointRounding.AwayFromZero);

        return new NutrientSummary(
            Consumed: consumed,
            Target: t,
            Remaining: t - consumed,
            PercentOfTarget: percent);
    }

    // ── Goals override layering (mirrors SummaryEndpoints / GoalsEndpoints) ──────────

    /// <summary>
    /// Returns the effective targets with any stored overrides layered on top of the computed
    /// values (override wins when present). Mirrors the override-wins rule in <c>SummaryEndpoints</c>.
    /// </summary>
    private static GoalsCalculatorOutput ApplyOverrides(
        GoalsCalculatorOutput computed,
        UserGoalTargets? overrides)
    {
        if (overrides is null)
        {
            return computed;
        }

        return computed with
        {
            CaloriesKcal = overrides.CaloriesKcal ?? computed.CaloriesKcal,
            ProteinGrams = overrides.ProteinGrams ?? computed.ProteinGrams,
            CarbohydrateGrams = overrides.CarbohydrateGrams ?? computed.CarbohydrateGrams,
            FatGrams = overrides.FatGrams ?? computed.FatGrams,
            WaterMl = overrides.WaterMl ?? computed.WaterMl,
        };
    }
}
