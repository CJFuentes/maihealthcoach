namespace MAIHealthCoach.Api.Features.Summary;

/// <summary>
/// One nutrient line in the daily summary: how much was consumed, the user's target (when goals
/// can be computed), how much remains against that target, and what fraction of the target has
/// been consumed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Target"/>, <see cref="Remaining"/>, and <see cref="PercentOfTarget"/> are
/// <see langword="null"/> when the user has no profile / incomplete biometrics (so goals cannot be
/// computed) — see <see cref="DailySummaryResponse"/>. <see cref="Consumed"/> is always present.
/// </para>
/// <para>
/// <see cref="Remaining"/> is <c>target − consumed</c> and may be negative (the user is over the
/// target). <see cref="PercentOfTarget"/> is <c>consumed / target × 100</c>, rounded to one
/// decimal place; it is <see langword="null"/> when there is no target or the target is zero
/// (avoids divide-by-zero) and may exceed 100 when consumed exceeds the target.
/// </para>
/// </remarks>
/// <param name="Consumed">Total consumed for the day from the food diary.</param>
/// <param name="Target">
/// The user's daily target (an integer, consistent with the goals layer), or
/// <see langword="null"/> when goals are unavailable.
/// </param>
/// <param name="Remaining">
/// Target minus consumed (may be negative and fractional), or <see langword="null"/>.
/// </param>
/// <param name="PercentOfTarget">Consumed as a percentage of the target, or <see langword="null"/>.</param>
public record NutrientSummary(
    decimal Consumed,
    int? Target,
    decimal? Remaining,
    decimal? PercentOfTarget);

/// <summary>
/// Calories and the three macronutrients consumed during a single meal slot. Used for the
/// optional per-meal breakdown in <see cref="DailySummaryResponse.Meals"/>.
/// </summary>
/// <param name="MealType">The meal slot name (Breakfast/Lunch/Dinner/Snack).</param>
/// <param name="EnergyKcal">Total energy consumed in this meal, in kcal.</param>
/// <param name="ProteinG">Total protein consumed in this meal, in grams.</param>
/// <param name="CarbohydrateG">Total carbohydrate consumed in this meal, in grams.</param>
/// <param name="FatG">Total fat consumed in this meal, in grams.</param>
/// <param name="EntryCount">Number of diary entries that contributed to this meal.</param>
public record MealSummary(
    string MealType,
    decimal EnergyKcal,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    int EntryCount);

/// <summary>
/// Response body for <c>GET /api/v1/me/summary?date=YYYY-MM-DD</c> (issue #23): the day's food
/// diary aggregated into calorie and macro totals, compared against the user's goal targets.
/// </summary>
/// <remarks>
/// <para>
/// Scope is the <b>food diary only</b> — calories and macros (protein/carbs/fat). Water and
/// exercise are intentionally out of scope here (separate features); the water <em>target</em> is
/// surfaced for convenience but no water/exercise logs are read.
/// </para>
/// <para>
/// Graceful states:
/// <list type="bullet">
///   <item><description>
///   <b>No diary entries</b> for the date → <see cref="Calories"/> and each macro report
///   <c>Consumed = 0</c> (targets/remaining still populated when goals are available), and
///   <see cref="Meals"/> is empty.
///   </description></item>
///   <item><description>
///   <b>No profile / incomplete biometrics</b> → <see cref="GoalsAvailable"/> is
///   <see langword="false"/>, every nutrient's <c>Target</c>/<c>Remaining</c>/<c>PercentOfTarget</c>
///   is <see langword="null"/>, and <see cref="WaterTargetMl"/> is <see langword="null"/>. Consumed
///   totals are still returned. The endpoint returns <c>200 OK</c> in this case so a dashboard can
///   render consumption without a hard error.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Date">The summarized date as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="GoalsAvailable">
/// <see langword="true"/> when the user's goals could be computed (complete profile); when
/// <see langword="false"/> all targets are <see langword="null"/>.
/// </param>
/// <param name="Calories">Calorie consumed/target/remaining/percent line (kcal).</param>
/// <param name="ProteinG">Protein consumed/target/remaining/percent line (grams).</param>
/// <param name="CarbohydrateG">Carbohydrate consumed/target/remaining/percent line (grams).</param>
/// <param name="FatG">Fat consumed/target/remaining/percent line (grams).</param>
/// <param name="WaterTargetMl">
/// The user's daily water target in millilitres (informational; surfaced from goals), or
/// <see langword="null"/> when goals are unavailable. No water consumption is tracked here.
/// </param>
/// <param name="EntryCount">Total number of diary entries summarized for the day.</param>
/// <param name="Meals">
/// Per-meal calorie/macro breakdown in canonical Breakfast → Lunch → Dinner → Snack order. Meal
/// slots with no entries are omitted.
/// </param>
public record DailySummaryResponse(
    string Date,
    bool GoalsAvailable,
    NutrientSummary Calories,
    NutrientSummary ProteinG,
    NutrientSummary CarbohydrateG,
    NutrientSummary FatG,
    int? WaterTargetMl,
    int EntryCount,
    IReadOnlyList<MealSummary> Meals);
