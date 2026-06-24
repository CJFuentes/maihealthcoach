namespace MAIHealthCoach.Api.Features.Profile;

/// <summary>
/// A single entry in the <see cref="ProfileResponse.WeightHistory"/> array.
/// </summary>
/// <param name="WeightKg">Body weight in kilograms.</param>
/// <param name="MeasuredAt">UTC instant the measurement was recorded.</param>
public record WeightHistoryEntry(double WeightKg, DateTimeOffset MeasuredAt);

/// <summary>
/// Response body for <c>GET /api/v1/me/profile</c> and <c>PUT /api/v1/me/profile</c>.
/// Enum fields are serialised as their string names (or <see langword="null"/> when not set).
/// </summary>
/// <param name="Id">Profile internal identifier (UUIDv7).</param>
/// <param name="UserId">Owning user's internal identifier.</param>
/// <param name="HeightCm">Height in centimetres, or <see langword="null"/>.</param>
/// <param name="DateOfBirth">Date of birth, or <see langword="null"/>.</param>
/// <param name="BiologicalSex">Biological sex as a string, or <see langword="null"/>.</param>
/// <param name="ActivityLevel">Activity level as a string, or <see langword="null"/>.</param>
/// <param name="PrimaryGoal">Primary goal as a string, or <see langword="null"/>.</param>
/// <param name="Units">Unit system as a string (never null; defaults to <c>Metric</c>).</param>
/// <param name="DietType">Dietary pattern as a string, or <see langword="null"/>.</param>
/// <param name="Allergies">Allergy text, or <see langword="null"/> when no dietary preferences have been set.</param>
/// <param name="LatestWeightKg">Most recent weight in kilograms, or <see langword="null"/>.</param>
/// <param name="WeightHistory">Up to 90 most recent weight measurements, newest first.</param>
/// <param name="CreatedAt">UTC instant the profile was first created.</param>
/// <param name="UpdatedAt">UTC instant the profile was last modified.</param>
public record ProfileResponse(
    Guid Id,
    Guid UserId,
    double? HeightCm,
    DateOnly? DateOfBirth,
    string? BiologicalSex,
    string? ActivityLevel,
    string? PrimaryGoal,
    string Units,
    string? DietType,
    string? Allergies,
    double? LatestWeightKg,
    IReadOnlyList<WeightHistoryEntry> WeightHistory,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
