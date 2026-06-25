namespace MAIHealthCoach.Api.Features.Exercises;

/// <summary>
/// Request body for <c>POST /api/v1/exercises</c> — create a custom exercise activity owned by
/// the authenticated user (issue #33).
/// </summary>
/// <param name="Name">Display name for the activity. Required, non-blank, max 256 chars.</param>
/// <param name="Category">
/// Activity category string (Cardio, Strength, Flexibility, Sports, Other). Case-insensitive.
/// Required.
/// </param>
/// <param name="MetValue">
/// MET (Metabolic Equivalent of Task) value for the activity. Must be greater than zero and at
/// most 99.99.
/// </param>
public record CreateCustomExerciseRequest(
    string Name,
    string Category,
    decimal MetValue);

/// <summary>
/// Response body for a single exercise activity entry from the catalog.
/// </summary>
/// <param name="Id">Activity internal identifier (UUIDv7).</param>
/// <param name="Name">Display name of the activity.</param>
/// <param name="Category">Activity category as a string.</param>
/// <param name="MetValue">MET value for the activity.</param>
/// <param name="IsCustom">
/// <see langword="true"/> when this activity was created by the authenticated user;
/// <see langword="false"/> for shared seeded activities.
/// </param>
/// <param name="CreatedAt">UTC instant the activity was first created.</param>
/// <param name="UpdatedAt">UTC instant the activity was last modified.</param>
public record ExerciseActivityResponse(
    Guid Id,
    string Name,
    string Category,
    decimal MetValue,
    bool IsCustom,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Response wrapper for <c>GET /api/v1/exercises</c>: a count plus the matched activity items.
/// </summary>
/// <param name="Count">Total number of items in <paramref name="Items"/>.</param>
/// <param name="Items">The catalog entries matching the applied filters.</param>
public record ExerciseListResponse(
    int Count,
    IReadOnlyList<ExerciseActivityResponse> Items);
