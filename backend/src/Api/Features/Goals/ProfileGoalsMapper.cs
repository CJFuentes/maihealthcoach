using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Api.Features.Goals;

/// <summary>
/// Shared mapping from a persisted <see cref="UserProfile"/> to the
/// <see cref="GoalsCalculatorInput"/> the <see cref="GoalsCalculator"/> consumes. Centralised so
/// every endpoint that computes goals (the goals endpoint and the daily summary endpoint, #23)
/// applies the <em>same</em> completeness rule — a profile missing any required biometric must be
/// treated as "goals unavailable" identically everywhere, with no risk of the checks drifting
/// apart over time.
/// </summary>
internal static class ProfileGoalsMapper
{
    /// <summary>
    /// Builds a <see cref="GoalsCalculatorInput"/> from a profile, or returns
    /// <see langword="null"/> when the profile is missing or lacks any required biometric
    /// (weight, height, date of birth, biological sex, activity level, or primary goal). A
    /// <see langword="null"/> result is the canonical "goals cannot be computed" signal.
    /// </summary>
    internal static GoalsCalculatorInput? BuildCalculatorInput(UserProfile? profile)
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
    /// Computes whole-year age from <paramref name="dob"/> relative to the server's UTC date.
    /// Mirrors the algorithm in <c>ProfileValidator</c> exactly.
    /// </summary>
    internal static int AgeFrom(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
