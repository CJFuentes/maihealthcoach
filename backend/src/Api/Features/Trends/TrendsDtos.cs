namespace MAIHealthCoach.Api.Features.Trends;

/// <summary>
/// A single point in a <b>dense</b> daily time series (issue #43). The series is contiguous and
/// 0-filled: it has exactly one entry per calendar day in the requested
/// <c>[From, To]</c> window, in ascending date order, and the entry at index <c>i</c> always
/// corresponds to <c>From.AddDays(i)</c>. Days with no underlying data carry a
/// <see cref="Value"/> of <c>0</c> rather than being omitted, so a client can plot the series by
/// index without reconciling gaps. Contrast with <see cref="WeightPoint"/>, which is sparse.
/// </summary>
/// <param name="Date">The day this point represents, as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="Value">
/// The aggregated value for the day (kcal or ml depending on the series). <c>0</c> when the day has
/// no contributing entries — never <see langword="null"/>.
/// </param>
public record DailyPoint(string Date, decimal Value);

/// <summary>
/// A single point in the <b>sparse</b> body-weight time series (issue #43). Unlike
/// <see cref="DailyPoint"/>, the weight series is <em>not</em> 0-filled and is <em>not</em> indexable
/// by day-offset: it contains an entry only for days on which the user recorded a measurement, so a
/// client must position each point by its <see cref="Date"/>, never by its array index. Days without
/// a measurement are simply absent (a weight of <c>0</c> would be a meaningless data point and is
/// never emitted). When a day has multiple measurements, the latest instant for that UTC calendar day
/// wins (see <c>TrendsEndpoints</c>).
/// </summary>
/// <param name="Date">The UTC calendar day of the measurement, as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="WeightKg">The body weight recorded for that day, in kilograms.</param>
public record WeightPoint(string Date, double WeightKg);

/// <summary>
/// Response body for <c>GET /api/v1/me/trends</c> (issue #43): weight and calorie/water time-series
/// for the authenticated user over a resolved <c>[From, To]</c> window.
/// </summary>
/// <remarks>
/// <para>
/// The window is resolved from the query parameters as follows: an explicit <c>from</c> and/or
/// <c>to</c> takes precedence over <c>range</c>; otherwise <c>range</c> (7, 30, or 90) selects a
/// trailing window ending today; otherwise the default is the last 30 days. <see cref="From"/> and
/// <see cref="To"/> echo the resolved window so the client never has to re-derive it.
/// </para>
/// <para>
/// <b>Series semantics differ deliberately:</b>
/// <list type="bullet">
///   <item><description>
///   <see cref="CaloriesConsumed"/>, <see cref="CaloriesBurned"/>, <see cref="NetCalories"/>, and
///   <see cref="WaterMl"/> are <b>dense</b> <see cref="DailyPoint"/> series: one entry per day in
///   <c>[From, To]</c>, ascending, 0-filled, with index <c>i == From.AddDays(i)</c>.
///   </description></item>
///   <item><description>
///   <see cref="Weight"/> is a <b>sparse</b> <see cref="WeightPoint"/> series: one entry only for
///   days with a recorded measurement, positioned by <see cref="WeightPoint.Date"/>, never 0-filled.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
/// <param name="From">The resolved window start (inclusive) as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="To">The resolved window end (inclusive) as a <c>YYYY-MM-DD</c> string.</param>
/// <param name="CaloriesConsumed">
/// Dense daily series of calories consumed (kcal), summed from the day's diary entries.
/// </param>
/// <param name="CaloriesBurned">
/// Dense daily series of calories burned (kcal), summed from the day's exercise log entries.
/// </param>
/// <param name="NetCalories">
/// Dense daily series of net calories (consumed − burned, kcal); may be negative on a given day.
/// </param>
/// <param name="WaterMl">Dense daily series of water intake (ml), summed from the day's water entries.</param>
/// <param name="Weight">
/// Sparse body-weight series (kg): one point per day with a recorded measurement, latest-instant-wins
/// within each UTC calendar day. Never 0-filled; position by <see cref="WeightPoint.Date"/>.
/// </param>
public record TrendsResponse(
    string From,
    string To,
    IReadOnlyList<DailyPoint> CaloriesConsumed,
    IReadOnlyList<DailyPoint> CaloriesBurned,
    IReadOnlyList<DailyPoint> NetCalories,
    IReadOnlyList<DailyPoint> WaterMl,
    IReadOnlyList<WeightPoint> Weight);
