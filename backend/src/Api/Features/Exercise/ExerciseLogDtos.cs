namespace MAIHealthCoach.Api.Features.Exercise;

/// <summary>
/// Request body for <c>POST /api/v1/me/exercise</c> — log a single exercise session (issue #34).
/// All fields are required.
/// </summary>
/// <param name="ExerciseActivityId">
/// Catalog activity being logged. Must reference an activity visible to the caller (a shared seeded
/// activity or one of the caller's own custom activities). Must be a non-empty GUID.
/// </param>
/// <param name="DurationMinutes">Minutes of activity. Must be between 1 and 1440 inclusive.</param>
/// <param name="Date">The calendar date in <c>YYYY-MM-DD</c> format. Future dates are permitted.</param>
public record LogExerciseRequest(Guid ExerciseActivityId, int DurationMinutes, string Date);

/// <summary>
/// Request body for <c>PUT /api/v1/me/exercise/{id}</c>. All fields are required. The logged
/// activity is immutable (changing it is delete-and-add), so it is not part of this request;
/// changing <see cref="Date"/> moves the entry to another day.
/// </summary>
/// <param name="DurationMinutes">Replacement minutes. Must be between 1 and 1440 inclusive.</param>
/// <param name="Date">Replacement date in <c>YYYY-MM-DD</c> format.</param>
public record UpdateExerciseLogRequest(int DurationMinutes, string Date);

/// <summary>
/// A single logged exercise entry, returned embedded in the day response. The activity name and
/// category are denormalised from the catalog navigation for display.
/// </summary>
/// <param name="Id">Entry internal identifier (UUIDv7).</param>
/// <param name="ExerciseActivityId">The logged activity's identifier.</param>
/// <param name="ActivityName">Display name of the logged activity.</param>
/// <param name="ActivityCategory">Category of the logged activity as a string.</param>
/// <param name="DurationMinutes">Duration of the session in minutes.</param>
/// <param name="CaloriesBurned">Snapshotted estimated kilocalories burned for the session.</param>
/// <param name="Date">Calendar date of the entry as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="CreatedAt">UTC instant the entry was first created.</param>
/// <param name="UpdatedAt">UTC instant the entry was last modified.</param>
public record ExerciseLogEntryResponse(
    Guid Id,
    Guid ExerciseActivityId,
    string ActivityName,
    string ActivityCategory,
    int DurationMinutes,
    int CaloriesBurned,
    string Date,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Response body for <c>POST /api/v1/me/exercise</c> (201), <c>GET /api/v1/me/exercise?date=</c>
/// (200), and the body returned by <c>PUT</c> for the entry's (possibly new) date. Reports the
/// day's logged sessions and the total calories burned across them.
/// </summary>
/// <param name="Date">The queried/logged date as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="TotalCaloriesBurned">
/// Sum of every entry's <see cref="ExerciseLogEntryResponse.CaloriesBurned"/> for the day. This is
/// the value the daily summary (FD5) consumes.
/// </param>
/// <param name="EntryCount">Number of entries logged for the day.</param>
/// <param name="Entries">All entries for the day, ordered by <c>CreatedAt</c> ascending.</param>
public record ExerciseDayResponse(
    string Date,
    int TotalCaloriesBurned,
    int EntryCount,
    IReadOnlyList<ExerciseLogEntryResponse> Entries);
