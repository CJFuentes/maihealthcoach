using MAIHealthCoach.Domain.UserProfiles;

namespace MAIHealthCoach.Application.Goals;

/// <summary>
/// Immutable snapshot of the profile data required for goals computation. All fields are
/// non-nullable — the caller must validate completeness before constructing this record;
/// incomplete profiles must never reach <see cref="GoalsCalculator"/>.
/// </summary>
/// <param name="WeightKg">Most recent body weight in kilograms.</param>
/// <param name="HeightCm">Standing height in centimetres.</param>
/// <param name="AgeYears">Age in whole years, computed from date of birth by the caller.</param>
/// <param name="BiologicalSex">Biological sex for Mifflin-St Jeor formula selection.</param>
/// <param name="ActivityLevel">Activity level for the TDEE multiplier selection.</param>
/// <param name="PrimaryGoal">Goal direction for the calorie-target adjustment.</param>
public record GoalsCalculatorInput(
    double WeightKg,
    double HeightCm,
    int AgeYears,
    BiologicalSex BiologicalSex,
    ActivityLevel ActivityLevel,
    PrimaryGoal PrimaryGoal
);
