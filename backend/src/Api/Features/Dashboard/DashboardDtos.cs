using MAIHealthCoach.Api.Features.Summary;

namespace MAIHealthCoach.Api.Features.Dashboard;

/// <summary>
/// Response body for <c>GET /api/v1/me/dashboard?date=YYYY-MM-DD</c> (issue #42): a consolidated
/// daily snapshot composed from the existing summary, water, exercise, and streak computations.
/// </summary>
/// <remarks>
/// <para>
/// This endpoint <b>composes</b> the same per-feature services rather than recomputing the math, so
/// its calorie and macro numbers reconcile exactly with <c>GET /me/summary</c>, its water block with
/// <c>GET /me/water</c>, its exercise block with <c>GET /me/exercise</c>, and its streak block with
/// <c>GET /me/streaks</c>.
/// </para>
/// <para>
/// Graceful states:
/// <list type="bullet">
///   <item><description>
///   <b>No profile / incomplete biometrics</b> → <see cref="GoalsAvailable"/> is
///   <see langword="false"/>, every nutrient's target/remaining/percent is <see langword="null"/>,
///   <see cref="DashboardWater.GoalMl"/> and <see cref="DashboardWater.RemainingMl"/> are
///   <see langword="null"/>, and adherence figures are <see langword="null"/>. Consumed totals and
///   streaks are still returned.
///   </description></item>
///   <item><description>
///   <b>No data</b> for the date → consumed totals, water, and exercise report zeros and empty
///   counts; streaks are still computed from history.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Date">The snapshot date as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="GoalsAvailable">
/// <see langword="true"/> when the user's goals could be computed (complete profile); when
/// <see langword="false"/> all targets are <see langword="null"/>.
/// </param>
/// <param name="Calories">Calorie and macro consumed/target/remaining/percent block.</param>
/// <param name="Water">Water consumed/goal/remaining block.</param>
/// <param name="Exercise">Exercise calories-burned and entry-count block.</param>
/// <param name="NetCalories">
/// Calories consumed minus calories burned, rounded to the nearest whole number (away-from-zero).
/// May be negative when exercise burn exceeds consumption. It is <see langword="null"/> only when
/// the day has <em>no</em> diary entries <em>and</em> <em>no</em> exercise entries — net calories
/// are meaningless with no data on either side.
/// </param>
/// <param name="Streak">Current/longest streak and 7-day adherence block.</param>
public record DashboardResponse(
    string Date,
    bool GoalsAvailable,
    DashboardCalories Calories,
    DashboardWater Water,
    DashboardExercise Exercise,
    int? NetCalories,
    DashboardStreak Streak);

/// <summary>
/// The day's calorie and macronutrient consumption compared against the user's effective targets.
/// Each line carries consumed / target / remaining / percent-of-target, identical in shape and
/// derivation to <c>GET /me/summary</c> so the numbers reconcile exactly.
/// </summary>
/// <param name="Calories">Calorie consumed/target/remaining/percent line (kcal).</param>
/// <param name="ProteinG">Protein consumed/target/remaining/percent line (grams).</param>
/// <param name="CarbohydrateG">Carbohydrate consumed/target/remaining/percent line (grams).</param>
/// <param name="FatG">Fat consumed/target/remaining/percent line (grams).</param>
/// <param name="EntryCount">Total number of diary entries summarized for the day.</param>
public record DashboardCalories(
    NutrientSummary Calories,
    NutrientSummary ProteinG,
    NutrientSummary CarbohydrateG,
    NutrientSummary FatG,
    int EntryCount);

/// <summary>
/// The day's water intake against the user's daily water goal, mirroring <c>GET /me/water</c>.
/// </summary>
/// <param name="GoalsAvailable">
/// <see langword="true"/> when a water goal could be resolved (complete profile); when
/// <see langword="false"/> the goal/remaining fields are <see langword="null"/>.
/// </param>
/// <param name="ConsumedMl">Total millilitres of water logged for the day.</param>
/// <param name="GoalMl">
/// The effective daily water target in millilitres, or <see langword="null"/> when goals are
/// unavailable.
/// </param>
/// <param name="RemainingMl">
/// Goal minus consumed (may be negative when over the goal), or <see langword="null"/> when goals
/// are unavailable.
/// </param>
public record DashboardWater(
    bool GoalsAvailable,
    int ConsumedMl,
    int? GoalMl,
    int? RemainingMl);

/// <summary>
/// The day's exercise activity, mirroring <c>GET /me/exercise</c>: the sum of the snapshotted
/// calories burned across the day's logged sessions and how many sessions contributed.
/// </summary>
/// <param name="TotalCaloriesBurned">Total calories burned across the day's exercise entries.</param>
/// <param name="EntryCount">Number of exercise entries logged for the day.</param>
public record DashboardExercise(
    decimal TotalCaloriesBurned,
    int EntryCount);

/// <summary>
/// The user's logging streaks and recent adherence, mirroring <c>GET /me/streaks</c> but surfacing
/// only the trailing-7-day adherence figures for the dashboard.
/// </summary>
/// <param name="CurrentStreak">Consecutive active days ending today (grace: today or yesterday).</param>
/// <param name="LongestStreak">Longest consecutive run of active days in the user's history.</param>
/// <param name="CaloriesAdherence7d">
/// Percent of the last 7 days within the calorie band, or <see langword="null"/> when goals are
/// unavailable.
/// </param>
/// <param name="WaterAdherence7d">
/// Percent of the last 7 days meeting the water target, or <see langword="null"/> when goals are
/// unavailable.
/// </param>
public record DashboardStreak(
    int CurrentStreak,
    int LongestStreak,
    decimal? CaloriesAdherence7d,
    decimal? WaterAdherence7d);
