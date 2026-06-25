using MAIHealthCoach.Application.Exercise;
using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Exercise;

/// <summary>
/// Registers the exercise-log endpoints on the supplied versioned route builder (issue #34).
/// <list type="bullet">
///   <item><description><c>POST   /me/exercise</c>      — log a session; 201 with the day's response.</description></item>
///   <item><description><c>GET    /me/exercise?date=</c> — a day's entries + total calories burned.</description></item>
///   <item><description><c>PUT    /me/exercise/{id}</c>  — edit duration/date (activity immutable).</description></item>
///   <item><description><c>DELETE /me/exercise/{id}</c>  — remove an entry.</description></item>
/// </list>
/// All endpoints require authorization and scope every query to the current user's id. Accessing
/// another user's entry returns 404 (not 403) so entry existence is never leaked across users.
/// Calories burned is a point-in-time snapshot computed at log/edit time from the activity's MET,
/// the user's current body weight, and the duration — never recomputed on read (see
/// <see cref="ExerciseLogEntry"/>). The day's <c>TotalCaloriesBurned</c> is the value FD5's summary
/// consumes. Logging requires a recorded body weight; without one the calorie estimate is
/// impossible and the request is rejected with 422.
/// </summary>
internal static class ExerciseLogEndpoints
{
    /// <summary>
    /// Maps the exercise-log routes onto <paramref name="group"/> and requires authorization on all
    /// of them (issue #34).
    /// </summary>
    internal static RouteGroupBuilder MapExerciseLogEndpoints(this RouteGroupBuilder group)
    {
        var log = group.MapGroup("/me/exercise").RequireAuthorization();

        log.MapPost("/", LogEntryAsync)
            .WithName("LogExerciseEntry");

        log.MapGet("/", GetDayAsync)
            .WithName("GetExerciseDay");

        log.MapPut("/{id:guid}", EditEntryAsync)
            .WithName("EditExerciseEntry");

        log.MapDelete("/{id:guid}", DeleteEntryAsync)
            .WithName("DeleteExerciseEntry");

        return group;
    }

    // ── POST /api/v1/me/exercise ──────────────────────────────────────────────────

    private static async Task<IResult> LogEntryAsync(
        LogExerciseRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CaloriesBurnedCalculator calculator,
        CancellationToken ct)
    {
        var errors = ExerciseLogValidator.ValidateLog(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Resolve the activity scoped to visibility: shared (null owner) OR the caller's own custom
        // activity. Another user's custom activity is indistinguishable from a missing one (404).
        var activity = await db.ExerciseActivities
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == request.ExerciseActivityId
                     && (a.CreatedByUserId == null || a.CreatedByUserId == user.Id),
                ct);

        if (activity is null)
        {
            return NotFound(
                "Exercise activity not found.",
                $"No exercise activity with id '{request.ExerciseActivityId}' exists or is " +
                $"visible to this user.");
        }

        var weightKg = await ResolveWeightKgAsync(user.Id, db, ct);
        if (weightKg is not { } weight || weight <= 0)
        {
            return WeightRequired();
        }

        var date = ExerciseLogValidator.ParseDate(request.Date);

        // Snapshot the kcal estimate from the activity MET, the user's current weight, and duration.
        var durationHours = request.DurationMinutes / 60.0;
        var kcal = calculator.EstimateKcal(activity.MetValue, weight, durationHours);

        var entry = ExerciseLogEntry.Create(
            user.Id, activity.Id, request.DurationMinutes, date, kcal);

        db.ExerciseLogEntries.Add(entry);
        await db.SaveChangesAsync(ct);

        var response = await BuildDayResponseAsync(user.Id, date, db, ct);
        return Results.Created($"/api/v1/me/exercise/{entry.Id}", response);
    }

    // ── GET /api/v1/me/exercise?date=YYYY-MM-DD ───────────────────────────────────

    private static async Task<IResult> GetDayAsync(
        string? date,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        // Optional date: default to today when omitted/blank, 400 on a malformed value. Bound as a
        // nullable string so an omitted param does not 400 on the value-type binding rules.
        DateOnly exDate;
        if (string.IsNullOrWhiteSpace(date))
        {
            exDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }
        else if (!ExerciseLogValidator.TryParseDate(date, out exDate))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["date"] =
                    [
                        $"The 'date' query parameter must be a valid calendar date in " +
                        $"{ExerciseLogValidator.DateFormat} format.",
                    ],
                });
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var response = await BuildDayResponseAsync(user.Id, exDate, db, ct);
        return Results.Ok(response);
    }

    // ── PUT /api/v1/me/exercise/{id} ──────────────────────────────────────────────

    private static async Task<IResult> EditEntryAsync(
        Guid id,
        UpdateExerciseLogRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CaloriesBurnedCalculator calculator,
        CancellationToken ct)
    {
        var errors = ExerciseLogValidator.ValidateUpdate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Scope by UserId so another user's entry is indistinguishable from a missing one (404).
        // Tracked: this entry is mutated below.
        var entry = await db.ExerciseLogEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id, ct);

        if (entry is null)
        {
            return NotFound(
                "Exercise entry not found.",
                $"No exercise entry with id '{id}' exists for this user.");
        }

        // The activity is immutable, but we still need its MET to recompute the kcal snapshot
        // against the (possibly new) duration and the user's current weight. The FK guarantees the
        // activity exists; treat its absence defensively as a 404.
        var activity = await db.ExerciseActivities
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == entry.ExerciseActivityId, ct);

        if (activity is null)
        {
            return NotFound(
                "Exercise activity not found.",
                $"No exercise activity with id '{entry.ExerciseActivityId}' exists.");
        }

        var weightKg = await ResolveWeightKgAsync(user.Id, db, ct);
        if (weightKg is not { } weight || weight <= 0)
        {
            return WeightRequired();
        }

        var durationHours = request.DurationMinutes / 60.0;
        var kcal = calculator.EstimateKcal(activity.MetValue, weight, durationHours);

        entry.Update(request.DurationMinutes, ExerciseLogValidator.ParseDate(request.Date), kcal);
        await db.SaveChangesAsync(ct);

        // Report the day the entry now belongs to (its possibly-changed date).
        var response = await BuildDayResponseAsync(user.Id, entry.Date, db, ct);
        return Results.Ok(response);
    }

    // ── DELETE /api/v1/me/exercise/{id} ───────────────────────────────────────────

    private static async Task<IResult> DeleteEntryAsync(
        Guid id,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var entry = await db.ExerciseLogEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == user.Id, ct);

        if (entry is null)
        {
            return NotFound(
                "Exercise entry not found.",
                $"No exercise entry with id '{id}' exists for this user.");
        }

        db.ExerciseLogEntries.Remove(entry);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static IResult NotFound(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// 422 returned when the user has no recorded body weight: the MET formula cannot estimate
    /// calories burned without a positive weight, and the calculator throws on a non-positive
    /// value, so we reject before calling it.
    /// </summary>
    private static IResult WeightRequired() =>
        Results.Problem(
            title: "Body weight required",
            detail: "Record your body weight in your profile before logging exercise so calories " +
                    "burned can be estimated.",
            statusCode: StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Loads the user's most recent recorded body weight in kilograms, or <see langword="null"/>
    /// when the profile is missing or has no weight measurements. Reads the same
    /// <c>UserProfile.LatestWeightKg</c> the summary/goals path uses.
    /// </summary>
    private static async Task<double?> ResolveWeightKgAsync(
        Guid userId,
        AppDbContext db,
        CancellationToken ct)
    {
        var profile = await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return profile?.LatestWeightKg;
    }

    /// <summary>
    /// Builds the day response for a user/date: lists the day's entries (with their activity
    /// name/category from the <c>Include</c>'d navigation) and sums their snapshotted calories
    /// burned for the day total.
    /// </summary>
    private static async Task<ExerciseDayResponse> BuildDayResponseAsync(
        Guid userId,
        DateOnly date,
        AppDbContext db,
        CancellationToken ct)
    {
        var entries = await db.ExerciseLogEntries
            .AsNoTracking()
            .Include(e => e.ExerciseActivity)
            .Where(e => e.UserId == userId && e.Date == date)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(ct);

        var total = entries.Sum(e => e.CaloriesBurned);

        return new ExerciseDayResponse(
            Date: date.ToString(ExerciseLogValidator.DateFormat),
            TotalCaloriesBurned: total,
            EntryCount: entries.Count,
            Entries: entries.Select(MapEntry).ToList());
    }

    private static ExerciseLogEntryResponse MapEntry(ExerciseLogEntry e) =>
        new(
            Id: e.Id,
            ExerciseActivityId: e.ExerciseActivityId,
            ActivityName: e.ExerciseActivity.Name,
            ActivityCategory: e.ExerciseActivity.Category.ToString(),
            DurationMinutes: e.DurationMinutes,
            CaloriesBurned: e.CaloriesBurned,
            Date: e.Date.ToString(ExerciseLogValidator.DateFormat),
            CreatedAt: e.CreatedAt,
            UpdatedAt: e.UpdatedAt);
}
