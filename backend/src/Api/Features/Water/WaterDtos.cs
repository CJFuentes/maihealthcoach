namespace MAIHealthCoach.Api.Features.Water;

/// <summary>
/// Request body for <c>POST /api/v1/me/water</c> — log a water-intake amount (a "quick-add" of
/// 250/500 ml is just this request with the chosen amount). All fields are required.
/// </summary>
/// <param name="AmountMl">Millilitres consumed. Must be between 1 and 10000 inclusive.</param>
/// <param name="Date">The calendar date in <c>YYYY-MM-DD</c> format. Future dates are permitted.</param>
public record AddWaterEntryRequest(int AmountMl, string Date);

/// <summary>
/// Request body for <c>PUT /api/v1/me/water/{id}</c>. All fields are required. Changing
/// <see cref="Date"/> moves the entry to another day.
/// </summary>
/// <param name="AmountMl">Replacement millilitres. Must be between 1 and 10000 inclusive.</param>
/// <param name="Date">Replacement date in <c>YYYY-MM-DD</c> format.</param>
public record UpdateWaterEntryRequest(int AmountMl, string Date);

/// <summary>
/// A single logged water entry, returned standalone (POST/PUT) and embedded in the day response.
/// </summary>
/// <param name="Id">Entry internal identifier (UUIDv7).</param>
/// <param name="AmountMl">Millilitres logged.</param>
/// <param name="Date">Calendar date of the entry as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="CreatedAt">UTC instant the entry was first created.</param>
/// <param name="UpdatedAt">UTC instant the entry was last modified.</param>
public record WaterEntryResponse(
    Guid Id,
    int AmountMl,
    string Date,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Response body for <c>POST /api/v1/me/water</c> (201) and <c>GET /api/v1/me/water?date=</c> (200),
/// and the body returned by <c>PUT</c> for the entry's (possibly new) date. Reports the day's
/// running total, the daily water goal (the goals engine's water target with any user override
/// layered on), the remaining amount versus that goal, and the day's entries.
/// </summary>
/// <param name="Date">The queried/logged date as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="GoalsAvailable">
/// <see langword="true"/> when the daily water goal could be computed (the profile is complete).
/// When <see langword="false"/>, <see cref="GoalMl"/> and <see cref="RemainingMl"/> are
/// <see langword="null"/>.
/// </param>
/// <param name="ConsumedMl">Total millilitres logged for the day (sum of all entries).</param>
/// <param name="GoalMl">Daily water target in ml, or <see langword="null"/> when goals are unavailable.</param>
/// <param name="RemainingMl">
/// <see cref="GoalMl"/> minus <see cref="ConsumedMl"/> (may be negative when over-goal);
/// <see langword="null"/> when the goal is null.
/// </param>
/// <param name="Entries">All entries for the day, ordered by <c>CreatedAt</c> ascending.</param>
public record WaterDayResponse(
    string Date,
    bool GoalsAvailable,
    int ConsumedMl,
    int? GoalMl,
    int? RemainingMl,
    IReadOnlyList<WaterEntryResponse> Entries);
