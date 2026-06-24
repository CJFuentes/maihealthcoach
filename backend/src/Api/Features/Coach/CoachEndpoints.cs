using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Application.MealSuggestions;
using MAIHealthCoach.Domain.UserProfiles;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Coach;

/// <summary>
/// Registers the MAI coach endpoints on the supplied versioned route builder.
/// <list type="bullet">
///   <item><description><c>GET /me/coach/meal-suggestions</c> — suggest meals that fit the user's remaining nutrition budget and dietary preferences (issue #37).</description></item>
/// </list>
/// Goals are recomputed from the profile per request (computation-first); any stored overrides are
/// layered on top before the remaining budget is derived.
/// </summary>
internal static class CoachEndpoints
{
    internal static RouteGroupBuilder MapCoachEndpoints(this RouteGroupBuilder group)
    {
        var coach = group.MapGroup("/me/coach").RequireAuthorization();

        coach.MapGet("/meal-suggestions", GetMealSuggestionsAsync)
            .WithName("GetMealSuggestions");

        return group;
    }

    // ── GET /api/v1/me/coach/meal-suggestions ─────────────────────────────────────

    private static async Task<IResult> GetMealSuggestionsAsync(
        string? mealType,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        IMealSuggestionService mealSuggestionService,
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

        var suggestionRequest = RemainingBudgetCalculator.Compute(
            computed,
            overrides,
            profile!.DietaryPreferences,
            mealType);

        var result = await mealSuggestionService.SuggestAsync(suggestionRequest, ct);

        if (!result.IsSuccess)
        {
            return MapCoachFailure(result);
        }

        return Results.Ok(MapToResponse(result));
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
                detail: "No profile exists for this user. Create a profile via PUT /api/v1/me/profile before requesting meal suggestions.",
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

    private static MealSuggestionsResponse MapToResponse(MealSuggestionResult result)
    {
        return new MealSuggestionsResponse(
            Options: result.Options
                .Select(o => new MealSuggestionOptionResponse(
                    o.Name,
                    o.Calories,
                    o.ProteinGrams,
                    o.CarbGrams,
                    o.FatGrams,
                    o.Rationale))
                .ToList(),
            RemainingCalories: result.RemainingCalories,
            RemainingProteinGrams: result.RemainingProteinGrams,
            RemainingCarbGrams: result.RemainingCarbGrams,
            RemainingFatGrams: result.RemainingFatGrams,
            Disclaimer: result.Disclaimer);
    }

    private static IResult MapCoachFailure(MealSuggestionResult result) =>
        result.ErrorCategory == CoachErrorCategory.ConfigurationError
            ? Results.Problem(
                title: "Coaching service not configured.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Problem(
                title: "Coaching service unavailable.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status502BadGateway);
}
