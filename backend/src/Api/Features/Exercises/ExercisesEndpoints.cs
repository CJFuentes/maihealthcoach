using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Exercises;

/// <summary>
/// Registers the exercise catalog endpoints on the supplied versioned route builder (issue #33).
/// <list type="bullet">
///   <item><description>
///   <c>GET  /exercises</c> — list/search the catalog (seeded shared + the caller's own custom).
///   Supports optional <c>?q=</c> (case-insensitive name search) and <c>?category=</c> filter.
///   </description></item>
///   <item><description>
///   <c>POST /exercises</c> — create a custom exercise activity owned by the caller.
///   </description></item>
/// </list>
/// All endpoints require authorization. A user's custom activities are visible only to that user;
/// seeded shared activities (null owner) are visible to all authenticated users. Exercise logging
/// (issue #34) and its UI (issue #35) build on this catalog.
/// </summary>
internal static class ExercisesEndpoints
{
    /// <summary>
    /// Maps <c>GET /exercises</c> and <c>POST /exercises</c> onto <paramref name="group"/> and
    /// requires authorization on both (issue #33).
    /// </summary>
    internal static RouteGroupBuilder MapExercisesEndpoints(this RouteGroupBuilder group)
    {
        var exercises = group.MapGroup("/exercises").RequireAuthorization();

        exercises.MapGet("/", ListExercisesAsync)
            .WithName("ListExercises");

        exercises.MapPost("/", CreateCustomExerciseAsync)
            .WithName("CreateCustomExercise");

        return group;
    }

    // ── GET /api/v1/exercises ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists exercise activities visible to the current user: all shared seeded activities
    /// (null owner) plus the caller's own custom activities. Never returns another user's custom
    /// activities.
    /// </summary>
    /// <remarks>
    /// Optional <c>?q=</c> performs a case-insensitive contains search on the activity name. It
    /// lower-cases both operands and uses <c>EF.Functions.Like</c> with an explicit escape
    /// character — this translates to <c>lower(Name) LIKE lower(pattern)</c> on both Npgsql
    /// (production) and the SQLite test harness, so test and production behaviour agree (unlike the
    /// Npgsql-only <c>ILike</c>, which does not translate on SQLite). LIKE wildcards (<c>%</c>,
    /// <c>_</c>) in the user input are escaped so a literal <c>%</c> matches a literal percent sign
    /// rather than every row.
    /// Optional <c>?category=</c> filters by <see cref="ExerciseCategory"/> (string, case-insensitive);
    /// an unknown value returns 400. Both filters apply together (AND). Results are ordered shared
    /// activities first, then the caller's custom activities, each sorted alphabetically by name.
    /// </remarks>
    private static async Task<IResult> ListExercisesAsync(
        string? q,
        string? category,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        // q and category are bound as nullable strings to avoid the minimal-API "required
        // value-type query param" gotcha; an omitted value simply means "no filter".

        // Validate the optional category filter up front so an invalid value returns 400.
        ExerciseCategory? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!ExerciseValidator.TryParseCategory(category, out var parsed))
            {
                return Results.ValidationProblem(ExerciseValidator.InvalidCategoryError(category));
            }

            categoryFilter = parsed;
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Visibility: shared (null owner) OR the caller's own custom activities. Another user's
        // custom activity has a non-null owner that does not match, so it is never returned.
        var query = db.ExerciseActivities
            .AsNoTracking()
            .Where(e => e.CreatedByUserId == null || e.CreatedByUserId == user.Id);

        if (!string.IsNullOrWhiteSpace(q))
        {
            // Escape LIKE wildcards in user input so a literal '%' or '_' matches itself rather
            // than acting as a wildcard. Backslash is the escape character (escaped first so the
            // escape sequences we add are not themselves re-escaped). Both operands are lower-cased
            // for provider-agnostic case-insensitive matching (Npgsql + SQLite).
            var escaped = q.Trim()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            var pattern = $"%{escaped.ToLowerInvariant()}%";
            query = query.Where(e => EF.Functions.Like(e.Name.ToLower(), pattern, "\\"));
        }

        if (categoryFilter is { } cat)
        {
            query = query.Where(e => e.Category == cat);
        }

        // Shared rows (null owner) sort before custom rows (HasValue => 1), each by name.
        var activities = await query
            .OrderBy(e => e.CreatedByUserId.HasValue ? 1 : 0)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

        var items = activities
            .Select(e => MapToResponse(e, userId: user.Id))
            .ToList();

        return Results.Ok(new ExerciseListResponse(items.Count, items));
    }

    // ── POST /api/v1/exercises ──────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateCustomExerciseAsync(
        CreateCustomExerciseRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = ExerciseValidator.ValidateCreate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Category is known valid here (validator guarantees it parses).
        ExerciseValidator.TryParseCategory(request.Category, out var category);

        var activity = ExerciseActivity.CreateCustom(
            createdByUserId: user.Id,
            name: request.Name,
            category: category,
            metValue: request.MetValue);

        db.ExerciseActivities.Add(activity);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/v1/exercises/{activity.Id}",
            MapToResponse(activity, userId: user.Id));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps an <see cref="ExerciseActivity"/> to its response DTO. <c>IsCustom</c> is true only
    /// when the activity is owned by <paramref name="userId"/>; shared seeded activities (null
    /// owner) never match a real user id and so report false.
    /// </summary>
    private static ExerciseActivityResponse MapToResponse(ExerciseActivity activity, Guid userId) =>
        new(
            Id: activity.Id,
            Name: activity.Name,
            Category: activity.Category.ToString(),
            MetValue: activity.MetValue,
            IsCustom: activity.CreatedByUserId == userId,
            CreatedAt: activity.CreatedAt,
            UpdatedAt: activity.UpdatedAt);
}
