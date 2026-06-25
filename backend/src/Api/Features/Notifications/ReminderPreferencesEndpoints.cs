using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Notifications;

/// <summary>
/// Registers the reminder-preferences endpoints on the supplied versioned route builder (issue #48).
/// <list type="bullet">
///   <item><description><c>GET /me/reminder-preferences</c> — the caller's preferences (a disabled synthetic default when none stored).</description></item>
///   <item><description><c>PUT /me/reminder-preferences</c> — upsert the caller's preferences.</description></item>
/// </list>
/// Both endpoints require authorization and scope to the current user. The upsert resolves the
/// first-create race the same way <c>ProfileEndpoints</c> does.
/// </summary>
internal static class ReminderPreferencesEndpoints
{
    internal static RouteGroupBuilder MapReminderPreferencesEndpoints(this RouteGroupBuilder group)
    {
        var prefs = group.MapGroup("/me/reminder-preferences").RequireAuthorization();

        prefs.MapGet("/", GetPreferencesAsync)
            .WithName("GetReminderPreferences");

        prefs.MapPut("/", UpsertPreferencesAsync)
            .WithName("UpsertReminderPreferences");

        return group;
    }

    // ── GET /api/v1/me/reminder-preferences ───────────────────────────────────────

    private static async Task<IResult> GetPreferencesAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var row = await db.ReminderPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (row is null)
        {
            // No row yet: report the disabled default so clients get a stable shape without a 404.
            return Results.Ok(new ReminderPreferencesResponse(
                Id: Guid.Empty,
                MealRemindersEnabled: false,
                WaterRemindersEnabled: false,
                MealReminderTimes: [],
                WaterReminderTime: null,
                QuietHoursStart: null,
                QuietHoursEnd: null,
                UtcOffsetMinutes: 0,
                CreatedAt: default,
                UpdatedAt: default));
        }

        return Results.Ok(Map(row));
    }

    // ── PUT /api/v1/me/reminder-preferences ───────────────────────────────────────

    private static async Task<IResult> UpsertPreferencesAsync(
        UpdateReminderPreferencesRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var errors = ReminderPreferencesValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Parse to TimeOnly only after validation passed.
        var mealTimes = ReminderPreferencesValidator.ParseTimes(request.MealReminderTimes);
        var waterTime = ReminderPreferencesValidator.ParseTimeOrNull(request.WaterReminderTime);
        var quietStart = ReminderPreferencesValidator.ParseTimeOrNull(request.QuietHoursStart);
        var quietEnd = ReminderPreferencesValidator.ParseTimeOrNull(request.QuietHoursEnd);

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var row = await db.ReminderPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        bool isNew = row is null;

        if (row is null)
        {
            row = ReminderPreferences.Create(user.Id);
            db.ReminderPreferences.Add(row);
        }

        row.Update(
            request.MealRemindersEnabled,
            request.WaterRemindersEnabled,
            mealTimes,
            waterTime,
            quietStart,
            quietEnd,
            request.UtcOffsetMinutes);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (PreferencesExist(db, user.Id))
        {
            // Concurrent first-PUT race: another request inserted the same UserId between our read
            // (null) and our insert (unique-index violation). Detach, re-read the winner, re-apply.
            db.ChangeTracker.Clear();

            row = await db.ReminderPreferences
                .FirstAsync(p => p.UserId == user.Id, ct);

            row.Update(
                request.MealRemindersEnabled,
                request.WaterRemindersEnabled,
                mealTimes,
                waterTime,
                quietStart,
                quietEnd,
                request.UtcOffsetMinutes);

            await db.SaveChangesAsync(ct);
            isNew = false;
        }

        var response = Map(row);
        return isNew
            ? Results.Created("/api/v1/me/reminder-preferences", response)
            : Results.Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // Exception filter (must be synchronous) — only swallows DbUpdateException when the preferences
    // row now exists, meaning we genuinely lost a first-create race.
    private static bool PreferencesExist(AppDbContext db, Guid userId) =>
        db.ReminderPreferences
            .AsNoTracking()
            .Any(p => p.UserId == userId);

    private static ReminderPreferencesResponse Map(ReminderPreferences row) =>
        new(
            Id: row.Id,
            MealRemindersEnabled: row.MealRemindersEnabled,
            WaterRemindersEnabled: row.WaterRemindersEnabled,
            MealReminderTimes: row.GetMealReminderTimes().Select(t => t.ToString("HH:mm")).ToList(),
            WaterReminderTime: row.WaterReminderTime,
            QuietHoursStart: row.QuietHoursStart,
            QuietHoursEnd: row.QuietHoursEnd,
            UtcOffsetMinutes: row.UtcOffsetMinutes,
            CreatedAt: row.CreatedAt,
            UpdatedAt: row.UpdatedAt);
}
