using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Domain.UserProfiles;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Goals;

/// <summary>
/// Registers the goal endpoints on the supplied versioned route builder.
/// <list type="bullet">
///   <item><description><c>GET  /me/goals</c> — compute and return daily targets, with any stored overrides layered on.</description></item>
///   <item><description><c>PUT  /me/goals/overrides</c> — persist manual override values (or clear them).</description></item>
/// </list>
/// Goals are always recomputed from the profile per request (computation-first); overrides
/// are layered on top of the computed values.
/// </summary>
internal static class GoalsEndpoints
{
    internal static RouteGroupBuilder MapGoalsEndpoints(this RouteGroupBuilder group)
    {
        var goals = group.MapGroup("/me/goals").RequireAuthorization();

        goals.MapGet("/", GetGoalsAsync)
            .WithName("GetGoals");

        goals.MapPut("/overrides", SetGoalOverridesAsync)
            .WithName("SetGoalOverrides");

        return group;
    }

    // ── GET /api/v1/me/goals ──────────────────────────────────────────────────────

    private static async Task<IResult> GetGoalsAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var profile = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (BuildCalculatorInput(profile) is not { } input)
        {
            return ProfileProblem(profile);
        }

        var computed = calculator.Compute(input);

        var overrides = await db.UserGoalTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

        return Results.Ok(MapToResponse(computed, overrides));
    }

    // ── PUT /api/v1/me/goals/overrides ────────────────────────────────────────────

    private static async Task<IResult> SetGoalOverridesAsync(
        SetGoalOverridesRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        CancellationToken ct)
    {
        // 1. Validate the override values — never throws.
        var errors = SetGoalOverridesValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // 2. The response echoes the computed goals, so a complete profile is required.
        var profile = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (BuildCalculatorInput(profile) is not { } input)
        {
            return ProfileProblem(profile);
        }

        // 3. Upsert the override row.
        var overrides = await db.UserGoalTargets
            .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

        var isNew = overrides is null;
        if (overrides is null)
        {
            overrides = UserGoalTargets.Create(user.Id);
            db.UserGoalTargets.Add(overrides);
        }

        overrides.SetOverrides(
            request.CaloriesKcal,
            request.ProteinGrams,
            request.CarbohydrateGrams,
            request.FatGrams,
            request.WaterMl);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (OverridesAlreadyExist(db, user.Id))
        {
            // Concurrent first-PUT race on the unique UserId index: another request inserted
            // the same UserId between our read (null) and our insert. Re-read the winner row
            // and re-apply our changes on top of it.
            db.ChangeTracker.Clear();

            overrides = await db.UserGoalTargets
                .FirstAsync(t => t.UserId == user.Id, ct);

            overrides.SetOverrides(
                request.CaloriesKcal,
                request.ProteinGrams,
                request.CarbohydrateGrams,
                request.FatGrams,
                request.WaterMl);

            await db.SaveChangesAsync(ct);
            isNew = false;
        }

        var computed = calculator.Compute(input);
        var response = MapToResponse(computed, overrides);

        return isNew
            ? Results.Created("/api/v1/me/goals", response)
            : Results.Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="GoalsCalculatorInput"/> from a profile, or returns
    /// <see langword="null"/> when the profile is missing or lacks any required biometric.
    /// Callers translate a <see langword="null"/> result via <see cref="ProfileProblem"/>.
    /// </summary>
    private static GoalsCalculatorInput? BuildCalculatorInput(UserProfile? profile)
    {
        if (profile is null
            || profile.LatestWeightKg is not { } weightKg
            || profile.HeightCm is not { } heightCm
            || profile.DateOfBirth is not { } dob
            || profile.BiologicalSex is not { } sex
            || profile.ActivityLevel is not { } activity
            || profile.PrimaryGoal is not { } goal)
        {
            return null;
        }

        return new GoalsCalculatorInput(
            WeightKg: weightKg,
            HeightCm: heightCm,
            AgeYears: AgeFrom(dob),
            BiologicalSex: sex,
            ActivityLevel: activity,
            PrimaryGoal: goal);
    }

    /// <summary>
    /// Produces the 404/409 ProblemDetails for a missing profile or one with insufficient
    /// biometrics. Mirrors the field-completeness check in <see cref="BuildCalculatorInput"/>.
    /// </summary>
    private static IResult ProfileProblem(UserProfile? profile)
    {
        if (profile is null)
        {
            return Results.Problem(
                title: "Profile not found.",
                detail: "No profile exists for this user. Create a profile via PUT /api/v1/me/profile before requesting goals.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var missing = new List<string>();
        if (!profile.LatestWeightKg.HasValue) missing.Add("weight");
        if (!profile.HeightCm.HasValue) missing.Add("heightCm");
        if (!profile.DateOfBirth.HasValue) missing.Add("dateOfBirth");
        if (!profile.BiologicalSex.HasValue) missing.Add("biologicalSex");
        if (!profile.ActivityLevel.HasValue) missing.Add("activityLevel");
        if (!profile.PrimaryGoal.HasValue) missing.Add("primaryGoal");

        return Results.Problem(
            title: "Incomplete profile.",
            detail: $"The following profile fields are required for goals computation but are not set: {string.Join(", ", missing)}. " +
                    "Update your profile via PUT /api/v1/me/profile.",
            statusCode: StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Computes whole-year age from <paramref name="dob"/> relative to the server's UTC date.
    /// Mirrors the algorithm in <c>ProfileValidator</c> exactly.
    /// </summary>
    private static int AgeFrom(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    // Exception filter (must be synchronous) — only swallows DbUpdateException when the
    // override row now exists, meaning we genuinely lost a first-create race.
    private static bool OverridesAlreadyExist(AppDbContext db, Guid userId) =>
        db.UserGoalTargets
            .AsNoTracking()
            .Any(t => t.UserId == userId);

    private static GoalsResponse MapToResponse(
        GoalsCalculatorOutput computed,
        UserGoalTargets? overrides)
    {
        return new GoalsResponse(
            Calories: MakeGoalValue(computed.CaloriesKcal, overrides?.CaloriesKcal),
            ProteinGrams: MakeGoalValue(computed.ProteinGrams, overrides?.ProteinGrams),
            CarbohydrateGrams: MakeGoalValue(computed.CarbohydrateGrams, overrides?.CarbohydrateGrams),
            FatGrams: MakeGoalValue(computed.FatGrams, overrides?.FatGrams),
            WaterMl: MakeGoalValue(computed.WaterMl, overrides?.WaterMl),
            Bmr: computed.Bmr,
            Tdee: computed.Tdee,
            LastOverriddenAt: overrides?.LastOverriddenAt);
    }

    private static GoalValue MakeGoalValue(int computedValue, int? overrideValue) =>
        overrideValue.HasValue
            ? new GoalValue(Value: overrideValue.Value, Computed: computedValue, IsOverridden: true)
            : new GoalValue(Value: computedValue, Computed: computedValue, IsOverridden: false);
}
