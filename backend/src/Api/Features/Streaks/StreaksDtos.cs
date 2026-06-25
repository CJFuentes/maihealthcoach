namespace MAIHealthCoach.Api.Features.Streaks;

/// <summary>
/// Response for <c>GET /api/v1/me/streaks</c> (issue #44): the authenticated user's logging streaks
/// and recent calorie + water adherence, computed on the fly from existing diary and water entries
/// against the user's effective goal targets.
/// </summary>
/// <remarks>
/// <para><strong>Active day.</strong> A calendar day counts toward streaks when the user has at
/// least one diary entry <em>or</em> one water-log entry on that day. Exercise is not yet part of
/// the active-day signal: issue #33 delivered only the exercise <em>catalog</em>
/// (<c>ExerciseActivities</c>); there is no per-user exercise log table to draw from, so exercise
/// days cannot contribute until logging exists.</para>
/// <para><strong>UTC-day boundaries.</strong> All day arithmetic uses plain UTC calendar days
/// (<c>DateOnly</c>), matching how diary and water entries are stored.</para>
/// <para><strong>Streaks.</strong> <see cref="CurrentStreak"/> is the unbroken run of active days
/// ending today, with a grace rule: the streak stays alive while the most recent active day is
/// today <em>or</em> yesterday (forgiving the not-yet-logged-today gap). Future-dated logs are
/// clamped to today, so they never inflate the streak. <see cref="LongestStreak"/> is the longest
/// consecutive run anywhere in the user's history.</para>
/// <para><strong>Adherence.</strong> Each adherence field is the percentage of days in the trailing
/// window (7 or 30 days, ending today) on which the user met the relevant target, rounded to one
/// decimal place. Calorie days are met when consumption falls within ±15% of the calorie target
/// (<c>[target*0.85, target*1.15]</c> inclusive); a day with zero calories never meets a positive
/// target. Water days are met when consumption is at least the water target. Days with no entries
/// count as zero consumption.</para>
/// <para><strong>Goals unavailable.</strong> Adherence requires the user's goal targets, which are
/// computed from a complete biometric profile. When the profile is incomplete (or absent), all four
/// adherence fields are <see langword="null"/> while the streak fields are still returned
/// (streaks do not depend on goals).</para>
/// </remarks>
/// <param name="CurrentStreak">Consecutive active days ending today (grace: today or yesterday).</param>
/// <param name="LongestStreak">Longest consecutive run of active days in the user's history.</param>
/// <param name="CaloriesAdherence7d">Percent of the last 7 days within the calorie band, or null when goals are unavailable.</param>
/// <param name="CaloriesAdherence30d">Percent of the last 30 days within the calorie band, or null when goals are unavailable.</param>
/// <param name="WaterAdherence7d">Percent of the last 7 days meeting the water target, or null when goals are unavailable.</param>
/// <param name="WaterAdherence30d">Percent of the last 30 days meeting the water target, or null when goals are unavailable.</param>
public record StreaksResponse(
    int CurrentStreak,
    int LongestStreak,
    decimal? CaloriesAdherence7d,
    decimal? CaloriesAdherence30d,
    decimal? WaterAdherence7d,
    decimal? WaterAdherence30d);
