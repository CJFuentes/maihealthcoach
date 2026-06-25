using MAIHealthCoach.Api.Features.Goals;
using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Application.Streaks;
using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Streaks;

/// <summary>
/// Registers the streaks endpoint on the supplied versioned route builder (issue #44).
/// <list type="bullet">
///   <item><description>
///   <c>GET /me/streaks</c> — current + longest active-day streak and 7/30-day calorie + water
///   adherence for the authenticated user, derived from existing diary and water entries against
///   the user's effective goal targets.
///   </description></item>
/// </list>
/// The endpoint requires authorization and scopes every query to the current user's id. Streaks are
/// always returned; adherence is <see langword="null"/> when the profile is incomplete (goals
/// cannot be computed). Active-day, grace, and adherence-band semantics are documented on
/// <see cref="StreaksResponse"/> and implemented in <see cref="StreakCalculator"/>.
/// </summary>
internal static class StreaksEndpoints
{
    internal static RouteGroupBuilder MapStreaksEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/streaks", GetStreaksAsync)
            .WithName("GetStreaks")
            .RequireAuthorization();

        return group;
    }

    // ── GET /api/v1/me/streaks ───────────────────────────────────────────────────────

    private static async Task<IResult> GetStreaksAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // ── Active days: a day counts when it has any diary OR any water entry ───────
        var diaryDates = await db.DiaryEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .Select(e => e.Date)
            .ToListAsync(ct);

        var waterDates = await db.WaterLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .Select(e => e.Date)
            .ToListAsync(ct);

        var activeDates = new HashSet<DateOnly>(diaryDates);
        foreach (var date in waterDates)
        {
            activeDates.Add(date);
        }

        var currentStreak = StreakCalculator.ComputeCurrentStreak(activeDates, today);
        var longestStreak = StreakCalculator.ComputeLongestStreak(activeDates);

        // ── Adherence: requires goal targets, which require a complete profile ───────
        var profile = await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        decimal? cal7 = null, cal30 = null, wat7 = null, wat30 = null;

        if (ProfileGoalsMapper.BuildCalculatorInput(profile) is { } input)
        {
            var computed = calculator.Compute(input);

            var overrides = await db.UserGoalTargets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

            // Layer the stored override on top of the computed target (override wins), so adherence
            // is measured against the same effective targets GET /me/goals and /me/summary report.
            var targetKcal = overrides?.CaloriesKcal ?? computed.CaloriesKcal;
            var targetWaterMl = overrides?.WaterMl ?? computed.WaterMl;

            // 30-day window covers both the 7- and 30-day adherence calculations.
            var windowStart30 = today.AddDays(-29);

            // DateOnly range predicates translate natively on Npgsql and on SQLite via ISO-string
            // ordering (the test host's DateOnly value converter), so the window filter runs in SQL.
            var diaryRows30 = await db.DiaryEntries
                .AsNoTracking()
                .Include(e => e.FoodItem)
                .Include(e => e.ServingSize)
                .Where(e => e.UserId == user.Id && e.Date >= windowStart30 && e.Date <= today)
                .ToListAsync(ct);

            // Accumulate each entry's recomputed calories into a per-day total. Computed in-memory
            // (the nutrition recompute and grouping cannot translate to SQL).
            var dailyCalories = new Dictionary<DateOnly, decimal>();
            foreach (var entry in diaryRows30)
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

            var waterRows30 = await db.WaterLogEntries
                .AsNoTracking()
                .Where(e => e.UserId == user.Id && e.Date >= windowStart30 && e.Date <= today)
                .ToListAsync(ct);

            // Materialized above (ToListAsync) so the GroupBy runs in-memory, not as server-side SQL.
            var dailyWater = waterRows30
                .GroupBy(e => e.Date)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountMl));

            cal7 = StreakCalculator.ComputeCaloriesAdherence(dailyCalories, targetKcal, today, 7);
            cal30 = StreakCalculator.ComputeCaloriesAdherence(dailyCalories, targetKcal, today, 30);
            wat7 = StreakCalculator.ComputeWaterAdherence(dailyWater, targetWaterMl, today, 7);
            wat30 = StreakCalculator.ComputeWaterAdherence(dailyWater, targetWaterMl, today, 30);
        }

        return Results.Ok(new StreaksResponse(currentStreak, longestStreak, cal7, cal30, wat7, wat30));
    }

    // ── Nutrition recompute (verbatim from SummaryEndpoints) ─────────────────────────

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
}
