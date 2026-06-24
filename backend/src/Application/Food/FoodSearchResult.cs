namespace MAIHealthCoach.Application.Food;

/// <summary>
/// The result of a text search. Either <see cref="Status"/> is
/// <see cref="NutritionLookupStatus.Found"/> with zero or more <see cref="Matches"/>, or it is a
/// graceful failure with an empty match list. The search path is read-through only — matches are
/// transient (see <see cref="FoodSearchMatch"/>) and never written to the cache, so there is no
/// cache fallback when Open Food Facts is unreachable.
/// </summary>
/// <remarks>
/// <see cref="ErrorDetail"/> is for server-side logging only and must never be surfaced to API
/// clients. An empty-but-successful search (no matches for the query) is
/// <see cref="NutritionLookupStatus.Found"/> with an empty <see cref="Matches"/>, distinct from
/// <see cref="NutritionLookupStatus.ServiceUnavailable"/>.
/// </remarks>
/// <param name="Status">The lookup outcome category.</param>
/// <param name="Matches">The ranked matches; empty on an empty or failed search.</param>
/// <param name="Page">The 1-based page of results this represents.</param>
/// <param name="ErrorDetail">Server-side-only diagnostic detail. Never returned to clients.</param>
public sealed record FoodSearchResult(
    NutritionLookupStatus Status,
    IReadOnlyList<FoodSearchMatch> Matches,
    int Page,
    string? ErrorDetail)
{
    /// <summary>Creates a successful search result carrying the ranked matches.</summary>
    /// <param name="matches">The ranked matches (possibly empty).</param>
    /// <param name="page">The 1-based page these matches belong to.</param>
    public static FoodSearchResult Success(IReadOnlyList<FoodSearchMatch> matches, int page) =>
        new(NutritionLookupStatus.Found, matches, page, null);

    /// <summary>Creates a successful but empty result (blank query or no matches).</summary>
    /// <param name="page">The 1-based page requested.</param>
    public static FoodSearchResult Empty(int page) =>
        new(NutritionLookupStatus.Found, [], page, null);

    /// <summary>Creates a service-unavailable result (OFF unreachable; no cache fallback for search).</summary>
    /// <param name="detail">Server-side-only diagnostic detail; never surfaced to clients.</param>
    public static FoodSearchResult Unavailable(string detail) =>
        new(NutritionLookupStatus.ServiceUnavailable, [], 1, detail);
}
