namespace MAIHealthCoach.Api.Features.Profile;

/// <summary>
/// Request body for <c>PUT /api/v1/me/profile</c>. All fields are optional — any supplied
/// field overwrites the current value; omitted fields are left unchanged.
/// Enum values are accepted as strings (case-insensitive) so that unknown values produce a
/// 400 validation error rather than a 500 deserialization exception.
/// </summary>
/// <param name="HeightCm">Height in centimetres. Valid range 50–272.</param>
/// <param name="DateOfBirth">Date of birth (ISO 8601 date, e.g. <c>1990-04-15</c>). Must be 13–120 years ago.</param>
/// <param name="BiologicalSex">Biological sex. Accepted values: <c>Male</c>, <c>Female</c> (case-insensitive).</param>
/// <param name="ActivityLevel">Activity level. Accepted values: <c>Sedentary</c>, <c>LightlyActive</c>, <c>ModeratelyActive</c>, <c>VeryActive</c>, <c>ExtraActive</c>.</param>
/// <param name="PrimaryGoal">Primary goal. Accepted values: <c>Lose</c>, <c>Maintain</c>, <c>Gain</c>.</param>
/// <param name="Units">Unit system. Accepted values: <c>Metric</c>, <c>Imperial</c>.</param>
/// <param name="DietType">Dietary pattern. Accepted values: <c>None</c>, <c>Vegetarian</c>, <c>Vegan</c>, <c>Pescatarian</c>, <c>Keto</c>, <c>Paleo</c>.</param>
/// <param name="Allergies">Free-text allergy description. Max 1 024 characters.</param>
/// <param name="WeightKg">Current body weight in kilograms. Valid range 20–500. Appends a new measurement only when the value differs from the latest recorded weight by ≥ 0.001 kg.</param>
public record UpdateProfileRequest(
    double? HeightCm,
    DateOnly? DateOfBirth,
    string? BiologicalSex,
    string? ActivityLevel,
    string? PrimaryGoal,
    string? Units,
    string? DietType,
    string? Allergies,
    double? WeightKg
);
