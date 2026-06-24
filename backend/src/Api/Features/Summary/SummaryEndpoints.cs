using MAIHealthCoach.Api.Features.Diary;
using MAIHealthCoach.Api.Features.Goals;
using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Summary;

/// <summary>
/// Registers the daily nutrition summary endpoint on the supplied versioned route builder
/// (issue #23).
/// <list type="bullet">
///   <item><description>
///   <c>GET /me/summary?date=</c> — aggregate the day's diary into calorie + macro totals,
///   compared against the user's goal targets (consumed / target / remaining / percent-of-target).
///   </description></item>
/// </list>
/// The endpoint requires authorization and scopes every query to the current user's id. The
/// optional <c>date</c> query parameter defaults to today (server UTC) when omitted, and is bound
/// as a nullable string to dodge the minimal-API "required value-type query param" gotcha.
/// </summary>
internal static class SummaryEndpoints
{
    internal static RouteGroupBuilder MapSummaryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/summary", GetSummaryAsync)
            .WithName("GetDailySummary")
            .RequireAuthorization();

        return group;
    }

    // ── GET /api/v1/me/summary?date=YYYY-MM-DD ───────────────────────────────────────

    private static async Task<IResult> GetSummaryAsync(
        string? date,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        // Optional date: default to today when omitted/blank, 400 on a malformed value. Bound as a
        // nullable string so an omitted param does not 400 on the value-type binding rules.
        DateOnly summaryDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            summaryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!DiaryEntryValidator.TryParseDate(date, out summaryDate))
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

        // ── Consumed: aggregate the day's diary, recomputing each entry's nutrition ──
        // FoodItem + ServingSize are eager-loaded so nutrition can be recomputed from the latest
        // food data (never snapshotted), exactly as the diary endpoints do.
        var entries = await db.DiaryEntries
            .AsNoTracking()
            .Include(e => e.FoodItem)
            .Include(e => e.ServingSize)
            .Where(e => e.UserId == user.Id && e.Date == summaryDate)
            .ToListAsync(ct);

        var (consumed, meals) = AggregateDay(entries);

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
            // summary compares consumption against the same effective targets GET /me/goals reports.
            targets = ApplyOverrides(computed, overrides);
        }

        var response = new DailySummaryResponse(
            Date: summaryDate.ToString(DiaryEntryValidator.DateFormat),
            GoalsAvailable: targets is not null,
            Calories: MakeNutrient(consumed.EnergyKcal, targets?.CaloriesKcal),
            ProteinG: MakeNutrient(consumed.ProteinG, targets?.ProteinGrams),
            CarbohydrateG: MakeNutrient(consumed.CarbohydrateG, targets?.CarbohydrateGrams),
            FatG: MakeNutrient(consumed.FatG, targets?.FatGrams),
            WaterTargetMl: targets?.WaterMl,
            EntryCount: entries.Count,
            Meals: meals);

        return Results.Ok(response);
    }

    // ── Consumed aggregation (single pass) ───────────────────────────────────────────

    private readonly record struct ConsumedTotals(
        decimal EnergyKcal,
        decimal ProteinG,
        decimal CarbohydrateG,
        decimal FatG);

    /// <summary>
    /// Walks the day's entries exactly once, recomputing each entry's nutrition a single time and
    /// folding it into both the grand totals and the per-meal buckets. Computing nutrition once per
    /// entry guarantees the grand totals and the per-meal sums always reconcile (they come from the
    /// same numbers) and avoids the repeated <see cref="NutritionFacts.ScaleToGrams"/> work of a
    /// per-meal re-scan. Meals with no entries are omitted, in canonical
    /// Breakfast → Lunch → Dinner → Snack order.
    /// </summary>
    private static (ConsumedTotals Totals, List<MealSummary> Meals) AggregateDay(
        IReadOnlyList<DiaryEntry> entries)
    {
        decimal totalEnergy = 0m, totalProtein = 0m, totalCarbs = 0m, totalFat = 0m;

        // One accumulator per meal slot, indexed by the MealType enum value.
        var mealAcc = new MealAccumulator[Enum.GetValues<MealType>().Length];

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

            ref var acc = ref mealAcc[(int)entry.MealType];
            acc.EnergyKcal += nutrition.EnergyKcal;
            acc.ProteinG += nutrition.ProteinG;
            acc.CarbohydrateG += nutrition.CarbohydrateG;
            acc.FatG += nutrition.FatG;
            acc.Count++;
        }

        var meals = new List<MealSummary>(mealAcc.Length);
        foreach (var meal in new[] { MealType.Breakfast, MealType.Lunch, MealType.Dinner, MealType.Snack })
        {
            var acc = mealAcc[(int)meal];
            if (acc.Count > 0)
            {
                meals.Add(new MealSummary(
                    meal.ToString(),
                    acc.EnergyKcal,
                    acc.ProteinG,
                    acc.CarbohydrateG,
                    acc.FatG,
                    acc.Count));
            }
        }

        return (new ConsumedTotals(totalEnergy, totalProtein, totalCarbs, totalFat), meals);
    }

    private struct MealAccumulator
    {
        public decimal EnergyKcal;
        public decimal ProteinG;
        public decimal CarbohydrateG;
        public decimal FatG;
        public int Count;
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

    // ── Nutrient line construction ───────────────────────────────────────────────────

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

    // ── Goals override layering (mirrors GoalsEndpoints) ─────────────────────────────

    /// <summary>
    /// Returns the effective targets with any stored overrides layered on top of the computed
    /// values. Mirrors the override-wins rule in <c>GoalsEndpoints</c>.
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
