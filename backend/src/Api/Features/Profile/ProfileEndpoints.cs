using MAIHealthCoach.Domain.UserProfiles;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Profile;

/// <summary>
/// Registers the <c>/me/profile</c> endpoint group on the supplied versioned route builder.
/// </summary>
internal static class ProfileEndpoints
{
    /// <summary>
    /// Maps <c>GET /me/profile</c> and <c>PUT /me/profile</c> onto
    /// <paramref name="group"/> and requires authorization on both.
    /// </summary>
    internal static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        var profile = group.MapGroup("/me/profile").RequireAuthorization();

        profile.MapGet("/", GetProfileAsync)
            .WithName("GetProfile");

        profile.MapPut("/", UpsertProfileAsync)
            .WithName("UpsertProfile");

        return group;
    }

    // ── GET /api/v1/me/profile ───────────────────────────────────────────────────

    private static async Task<IResult> GetProfileAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var profileEntity = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (profileEntity is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(MapToResponse(profileEntity));
    }

    // ── PUT /api/v1/me/profile ───────────────────────────────────────────────────

    private static async Task<IResult> UpsertProfileAsync(
        UpdateProfileRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        // 1. Validate — never throws; all enum strings are parsed here, not by the binder.
        var errors = ProfileValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // 2. Parse enum strings (already validated, so TryParse will succeed).
        Enum.TryParse<BiologicalSex>(request.BiologicalSex, ignoreCase: true, out var biologicalSex);
        Enum.TryParse<ActivityLevel>(request.ActivityLevel, ignoreCase: true, out var activityLevel);
        Enum.TryParse<PrimaryGoal>(request.PrimaryGoal, ignoreCase: true, out var primaryGoal);
        Enum.TryParse<UnitsPreference>(request.Units, ignoreCase: true, out var units);
        Enum.TryParse<DietType>(request.DietType, ignoreCase: true, out var dietType);

        var biologicalSexNullable = request.BiologicalSex is null ? (BiologicalSex?)null : biologicalSex;
        var activityLevelNullable = request.ActivityLevel is null ? (ActivityLevel?)null : activityLevel;
        var primaryGoalNullable = request.PrimaryGoal is null ? (PrimaryGoal?)null : primaryGoal;
        var unitsNullable = request.Units is null ? (UnitsPreference?)null : units;

        DietaryPreferences? dietaryPreferences = null;
        if (request.DietType is not null || request.Allergies is not null)
        {
            dietaryPreferences = DietaryPreferences.Create(
                request.DietType is null ? null : dietType,
                request.Allergies ?? string.Empty);
        }

        // 3. Resolve the current user.
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // 4. Load existing profile (with full weight collection for guard + history).
        var profileEntity = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        bool isNew = profileEntity is null;

        if (profileEntity is null)
        {
            profileEntity = UserProfile.Create(user.Id);
            db.UserProfiles.Add(profileEntity);
        }

        // 5. Apply scalar field updates.
        profileEntity.Update(
            request.HeightCm,
            request.DateOfBirth,
            biologicalSexNullable,
            activityLevelNullable,
            primaryGoalNullable,
            unitsNullable,
            dietaryPreferences);

        // 6. Append weight measurement (changed-value guard inside AddWeightMeasurement).
        if (request.WeightKg.HasValue)
        {
            profileEntity.AddWeightMeasurement(request.WeightKg.Value, DateTimeOffset.UtcNow);
        }

        // 7. Persist — mirror CurrentUserService's first-create race pattern.
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (ProfileAlreadyExists(db, user.Id))
        {
            // Concurrent first-PUT race: another request inserted the same UserId between
            // our read (null) and our insert (unique-index violation on UserId).
            // Detach, re-read the winner row, apply our changes on top of it.
            db.ChangeTracker.Clear();

            profileEntity = await db.UserProfiles
                .Include(p => p.WeightMeasurements)
                .FirstAsync(p => p.UserId == user.Id, ct);

            profileEntity.Update(
                request.HeightCm,
                request.DateOfBirth,
                biologicalSexNullable,
                activityLevelNullable,
                primaryGoalNullable,
                unitsNullable,
                dietaryPreferences);

            if (request.WeightKg.HasValue)
            {
                profileEntity.AddWeightMeasurement(request.WeightKg.Value, DateTimeOffset.UtcNow);
            }

            await db.SaveChangesAsync(ct);
            isNew = false;
        }

        var response = MapToResponse(profileEntity);
        return isNew
            ? Results.Created($"/api/v1/me/profile", response)
            : Results.Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    // Exception filter (must be synchronous) — only swallows DbUpdateException when the
    // profile row now exists, meaning we genuinely lost a first-create race.
    private static bool ProfileAlreadyExists(AppDbContext db, Guid userId) =>
        db.UserProfiles
            .AsNoTracking()
            .Any(p => p.UserId == userId);

    private static ProfileResponse MapToResponse(UserProfile profile)
    {
        var history = profile.WeightMeasurements
            .OrderByDescending(m => m.MeasuredAt)
            .Take(90)
            .Select(m => new WeightHistoryEntry(m.WeightKg, m.MeasuredAt))
            .ToList();

        return new ProfileResponse(
            Id: profile.Id,
            UserId: profile.UserId,
            HeightCm: profile.HeightCm,
            DateOfBirth: profile.DateOfBirth,
            BiologicalSex: profile.BiologicalSex?.ToString(),
            ActivityLevel: profile.ActivityLevel?.ToString(),
            PrimaryGoal: profile.PrimaryGoal?.ToString(),
            Units: profile.Units.ToString(),
            DietType: profile.DietaryPreferences?.DietType?.ToString(),
            Allergies: profile.DietaryPreferences?.Allergies,
            LatestWeightKg: profile.LatestWeightKg,
            WeightHistory: history,
            CreatedAt: profile.CreatedAt,
            UpdatedAt: profile.UpdatedAt
        );
    }
}
