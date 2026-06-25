using MAIHealthCoach.Api.Features.Diary;
using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Trends;

/// <summary>
/// Registers the trends time-series endpoint on the supplied versioned route builder (issue #43).
/// <list type="bullet">
///   <item><description>
///   <c>GET /me/trends?from=&amp;to=&amp;range=</c> — weight and calorie/water daily time-series for
///   the authenticated user over a resolved <c>[from, to]</c> window.
///   </description></item>
/// </list>
/// The endpoint requires authorization and scopes every query to the current user's id. The window is
/// resolved from the query parameters with an explicit <c>from</c>/<c>to</c> taking precedence over
/// <c>range</c>, which in turn takes precedence over the default last-30-days window. All three
/// parameters are bound as nullable strings to dodge the minimal-API "required value-type query param"
/// gotcha. The dense calorie/water series are 0-filled and index-aligned to the window; the weight
/// series is sparse (see <c>TrendsDtos</c>).
/// </summary>
internal static class TrendsEndpoints
{
    internal static RouteGroupBuilder MapTrendsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/trends", GetTrendsAsync)
            .WithName("GetTrends")
            .RequireAuthorization();

        return group;
    }

    // ── GET /api/v1/me/trends?from=YYYY-MM-DD&to=YYYY-MM-DD&range=7|30|90 ─────────────

    private static async Task<IResult> GetTrendsAsync(
        string? from,
        string? to,
        string? range,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        DateOnly resolvedFrom;
        DateOnly resolvedTo;

        // Window resolution is a total, ordered if/else chain. An explicit from/to wins outright and
        // makes range irrelevant; failing that, range selects a trailing window; failing that, the
        // default is the last 30 days. All params are bound as nullable strings (minimal-API gotcha).
        if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
        {
            // (a) from/to branch WINS — range is ignored entirely on this path.
            DateOnly parsedFrom = default;
            DateOnly parsedTo = default;

            if (!string.IsNullOrWhiteSpace(from) && !DiaryEntryValidator.TryParseDate(from, out parsedFrom))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["from"] =
                        [
                            $"The 'from' query parameter must be a valid calendar date in " +
                            $"{DiaryEntryValidator.DateFormat} format.",
                        ],
                    });
            }

            if (!string.IsNullOrWhiteSpace(to) && !DiaryEntryValidator.TryParseDate(to, out parsedTo))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["to"] =
                        [
                            $"The 'to' query parameter must be a valid calendar date in " +
                            $"{DiaryEntryValidator.DateFormat} format.",
                        ],
                    });
            }

            resolvedTo = string.IsNullOrWhiteSpace(to) ? today : parsedTo;
            resolvedFrom = string.IsNullOrWhiteSpace(from) ? resolvedTo.AddDays(-29) : parsedFrom;
        }
        else if (!string.IsNullOrWhiteSpace(range))
        {
            // (b) range branch — valid only for 7, 30, or 90; anything else is a 400.
            if (!int.TryParse(range, out var days) || (days != 7 && days != 30 && days != 90))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["range"] = ["The 'range' parameter must be 7, 30, or 90."],
                    });
            }

            resolvedTo = today;
            resolvedFrom = today.AddDays(-(days - 1));
        }
        else
        {
            // (c) default — last 30 days ending today.
            resolvedTo = today;
            resolvedFrom = today.AddDays(-29);
        }

        if (resolvedFrom > resolvedTo)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["from"] = ["The 'from' date must not be after 'to'."],
                });
        }

        var span = resolvedTo.DayNumber - resolvedFrom.DayNumber + 1;

        // 366 = roughly a year of daily points. This cap is only reachable via a free-form from/to
        // window (the range branch maxes out at 90), so it bounds an otherwise unbounded request.
        if (span > 366)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["range"] = ["The requested window must not exceed 366 days."],
                });
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // ── Source rows for the window, all scoped to this user and read-only ─────────
        // Diary: FoodItem + ServingSize are eager-loaded so nutrition is recomputed from the latest
        // food data (never snapshotted), exactly as the diary/summary/dashboard endpoints do.
        var diaryEntries = await db.DiaryEntries
            .AsNoTracking()
            .Include(e => e.FoodItem)
            .Include(e => e.ServingSize)
            .Where(e => e.UserId == user.Id && e.Date >= resolvedFrom && e.Date <= resolvedTo)
            .ToListAsync(ct);

        var exerciseEntries = await db.ExerciseLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Date >= resolvedFrom && e.Date <= resolvedTo)
            .ToListAsync(ct);

        var waterEntries = await db.WaterLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id && e.Date >= resolvedFrom && e.Date <= resolvedTo)
            .ToListAsync(ct);

        var profile = await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        // ── Aggregate each metric into a per-day dictionary (in memory) ──────────────
        var caloriesByDate = new Dictionary<DateOnly, decimal>();
        foreach (var entry in diaryEntries)
        {
            if (!TryScaleEntry(entry, out var nutrition))
            {
                // Defensive: a row whose food/serving navigation failed to materialize contributes
                // nothing rather than throwing a 500.
                continue;
            }

            caloriesByDate[entry.Date] =
                (caloriesByDate.TryGetValue(entry.Date, out var existing) ? existing : 0m)
                + nutrition.EnergyKcal;
        }

        // CaloriesBurned is an int snapshot; cast to decimal so it lines up with the DailyPoint value.
        var burnedByDate = exerciseEntries
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.Sum(e => (decimal)e.CaloriesBurned));

        // AmountMl is an int; cast to decimal for the same reason.
        var waterByDate = waterEntries
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (decimal)g.Sum(e => e.AmountMl));

        // ── Dense series: one 0-filled point per day, index i == resolvedFrom.AddDays(i) ──
        var consumed = new List<DailyPoint>(span);
        var burned = new List<DailyPoint>(span);
        var net = new List<DailyPoint>(span);
        var water = new List<DailyPoint>(span);

        for (var i = 0; i < span; i++)
        {
            var day = resolvedFrom.AddDays(i);
            var dateStr = day.ToString(DiaryEntryValidator.DateFormat);

            var c = caloriesByDate.GetValueOrDefault(day, 0m);
            var b = burnedByDate.GetValueOrDefault(day, 0m);
            var w = waterByDate.GetValueOrDefault(day, 0m);

            consumed.Add(new DailyPoint(dateStr, c));
            burned.Add(new DailyPoint(dateStr, b));
            net.Add(new DailyPoint(dateStr, c - b));
            water.Add(new DailyPoint(dateStr, w));
        }

        // ── Sparse weight series: one point per UTC calendar day with a measurement ───
        var weightPoints = new List<WeightPoint>();
        if (profile is not null)
        {
            // The bucket key is the UTC calendar day of MeasuredAt (matches how Profile/Dashboard
            // interpret weight days). DateOnly.FromDateTime would not translate to SQL, so the
            // measurements were loaded above and this grouping happens in memory.
            var grouped = profile.WeightMeasurements
                .Select(m => new
                {
                    Day = DateOnly.FromDateTime(m.MeasuredAt.UtcDateTime),
                    Measurement = m,
                })
                .Where(x => x.Day >= resolvedFrom && x.Day <= resolvedTo)
                .GroupBy(x => x.Day)
                .OrderBy(g => g.Key);

            foreach (var dayGroup in grouped)
            {
                // Latest instant within the UTC day wins.
                var latest = dayGroup.OrderByDescending(x => x.Measurement.MeasuredAt).First().Measurement;
                weightPoints.Add(new WeightPoint(
                    dayGroup.Key.ToString(DiaryEntryValidator.DateFormat),
                    latest.WeightKg));
            }
        }

        return Results.Ok(new TrendsResponse(
            From: resolvedFrom.ToString(DiaryEntryValidator.DateFormat),
            To: resolvedTo.ToString(DiaryEntryValidator.DateFormat),
            CaloriesConsumed: consumed,
            CaloriesBurned: burned,
            NetCalories: net,
            WaterMl: water,
            Weight: weightPoints));
    }

    // ── Nutrition scaling (verbatim copy from DashboardEndpoints) ────────────────────
    // TODO(#43 follow-up): extract TryScaleEntry into a shared helper (3rd copy: Summary/Dashboard/Trends).

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
