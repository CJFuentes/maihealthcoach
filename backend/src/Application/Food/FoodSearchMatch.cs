using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Application.Food;

/// <summary>
/// A single candidate from a text search against Open Food Facts, wrapping a
/// <strong>transient, non-persisted</strong> <see cref="FoodItem"/> (built via
/// <see cref="FoodItem.Create"/> with <see cref="FoodSource.OpenFoodFacts"/> and the OFF
/// barcode/source-reference set) plus its <see cref="Rank"/> in the upstream result ordering.
/// </summary>
/// <remarks>
/// <para>
/// Search matches are deliberately <strong>not</strong> written to the cache. Persisting every
/// search hit would bloat the <c>FoodItems</c> table and muddy staleness tracking (many hits are
/// never logged). Barcode lookups are the cached, write-through path; search is read-through only.
/// A later user selection (issues #21/#22) can persist a chosen match on demand.
/// </para>
/// <para>
/// Because the wrapped <see cref="FoodItem"/> is not tracked by EF, its <see cref="EntityBase.Id"/>
/// is provisional and should not be treated as a stable cache key.
/// </para>
/// </remarks>
/// <param name="Food">The transient, non-persisted mapped food for this candidate.</param>
/// <param name="Rank">
/// 1-based ordinal of this match within the upstream OFF result page (1 = first/most relevant),
/// preserving Open Food Facts' own relevance ordering.
/// </param>
public sealed record FoodSearchMatch(FoodItem Food, int Rank);
