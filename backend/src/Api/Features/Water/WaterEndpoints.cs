using MAIHealthCoach.Api.Features.Goals;
using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Domain.Water;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Water;

/// <summary>
/// Registers the water-log endpoints on the supplied versioned route builder (issue #31).
/// <list type="bullet">
///   <item><description><c>POST   /me/water</c>        — add an entry / quick-add; 201 with the day's total vs goal.</description></item>
///   <item><description><c>GET    /me/water?date=</c>  — a day's total + entries, consumed vs daily goal + remaining.</description></item>
///   <item><description><c>PUT    /me/water/{id}</c>   — edit amount/date.</description></item>
///   <item><description><c>DELETE /me/water/{id}</c>   — remove an entry.</description></item>
/// </list>
/// All endpoints require authorization and scope every query to the current user's id. Accessing
/// another user's entry returns 404 (not 403) so entry existence is never leaked across users.
/// The daily goal is the goals engine's water target (<c>GoalsCalculator</c>) with any stored
/// <c>UserGoalTargets.WaterMl</c> override layered on — never recomputed by hand. When the profile
/// is incomplete the goal is reported as unavailable (consumption is still useful on its own).
/// </summary>
internal static class WaterEndpoints
{
    internal static RouteGroupBuilder MapWaterEndpoints(this RouteGroupBuilder group)
    {
        var water = group.MapGroup("/me/water").RequireAuthorization();

        water.MapPost("/", AddEntryAsync)
            .WithName("AddWaterEntry");

        water.MapGet("/", GetDayAsync)
            .WithName("GetWaterDay");

        water.MapPut("/{id:guid}", EditEntryAsync)
            .WithName("EditWaterEntry");

        water.MapDelete("/{id:guid}", DeleteEntryAsync)
            .WithName("DeleteWaterEntry");

        return group;
    }

    // ── POST /api/v1/me/water ─────────────────────────────────────────────────────

    private static async Task<IResult> AddEntryAsync(
        AddWaterEntryRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        var errors = WaterEntryValidator.ValidateAdd(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);
        var date = WaterEntryValidator.ParseDate(request.Date);

        var entry = WaterLogEntry.Create(user.Id, request.AmountMl, date);

        db.WaterLogEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        // Return the day's running total + goal + remaining, including the just-saved entry.
        var response = await BuildDayResponseAsync(user.Id, date, db, calculator, ct);
        return Results.Created($"/api/v1/me/water/{entry.Id}", response);
    }

    // ── GET /api/v1/me/water?date=YYYY-MM-DD ─────────────────────────────────────

    private static async Task<IResult> GetDayAsync(
        string? date,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        // Optional date: default to today when omitted/blank, 400 on a malformed value. Bound as a
        // nullable string so an omitted param does not 400 on the value-type binding rules.
        DateOnly waterDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            waterDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!WaterEntryValidator.TryParseDate(date, out waterDate))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["date"] =
                    [
                        $"The 'date' query parameter must be a valid calendar date in " +
                        $"{WaterEntryValidator.DateFormat} format.",
                    ],
                });
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var response = await BuildDayResponseAsync(user.Id, waterDate, db, calculator, ct);
        return Results.Ok(response);
    }

    // ── PUT /api/v1/me/water/{id} ─────────────────────────────────────────────────

    private static async Task<IResult> EditEntryAsync(
        Guid id,
        UpdateWaterEntryRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        var errors = WaterEntryValidator.ValidateUpdate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Scope by UserId so another user's entry is indistinguishable from a missing one (404).
        var entry = await db.WaterLogEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id, ct);

        if (entry is null)
        {
            return NotFound(
                "Water entry not found.",
                $"No water entry with id '{id}' exists for this user.");
        }

        entry.Update(request.AmountMl, WaterEntryValidator.ParseDate(request.Date));
        await db.SaveChangesAsync(ct);

        // Report the day the entry now belongs to (its possibly-changed date).
        var response = await BuildDayResponseAsync(user.Id, entry.Date, db, calculator, ct);
        return Results.Ok(response);
    }

    // ── DELETE /api/v1/me/water/{id} ──────────────────────────────────────────────

    private static async Task<IResult> DeleteEntryAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var entry = await db.WaterLogEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id, ct);

        if (entry is null)
        {
            return NotFound(
                "Water entry not found.",
                $"No water entry with id '{id}' exists for this user.");
        }

        db.WaterLogEntries.Remove(entry);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static IResult NotFound(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// Builds the day response for a user/date: sums the day's entries for the consumed total, and
    /// resolves the daily water goal exactly as <c>SummaryEndpoints</c> does — compute from the
    /// profile via <see cref="ProfileGoalsMapper"/> + <see cref="GoalsCalculator"/>, then layer the
    /// stored <c>UserGoalTargets.WaterMl</c> override on top (override wins). When the profile is
    /// incomplete the goal is <see langword="null"/> and <c>GoalsAvailable</c> is false.
    /// </summary>
    private static async Task<WaterDayResponse> BuildDayResponseAsync(
        Guid userId,
        DateOnly date,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        var entries = await db.WaterLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Date == date)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        var consumedMl = entries.Sum(e => e.AmountMl);

        var profile = await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        int? goalMl = null;
        if (ProfileGoalsMapper.BuildCalculatorInput(profile) is { } input)
        {
            var computed = calculator.Compute(input);

            var overrides = await db.UserGoalTargets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId, ct);

            // Override wins when present, so the goal matches what GET /me/goals reports.
            goalMl = overrides?.WaterMl ?? computed.WaterMl;
        }

        return new WaterDayResponse(
            Date: date.ToString(WaterEntryValidator.DateFormat),
            GoalsAvailable: goalMl.HasValue,
            ConsumedMl: consumedMl,
            GoalMl: goalMl,
            RemainingMl: goalMl.HasValue ? goalMl.Value - consumedMl : null,
            Entries: entries.Select(MapEntry).ToList());
    }

    private static WaterEntryResponse MapEntry(WaterLogEntry e) =>
        new(
            Id: e.Id,
            AmountMl: e.AmountMl,
            Date: e.Date.ToString(WaterEntryValidator.DateFormat),
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt);
}
