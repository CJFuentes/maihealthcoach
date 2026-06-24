namespace MAIHealthCoach.Application.Food;

/// <summary>
/// Resolves nutrition data by barcode or text query, wrapping Open Food Facts (OFF) so callers
/// (issue #21 endpoints, #22 diary) never talk to OFF directly. The implementation lives in the
/// Infrastructure layer; this interface lets Application/API features depend on it without
/// depending on Infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Cache-first, graceful degradation.</strong> Barcode lookups consult the Postgres cache
/// first and only call OFF on a miss or when the cached row is stale; a fresh cache hit performs no
/// HTTP call. When OFF is unreachable, a present (even stale) cached row is served rather than
/// failing. Barcode results are persisted (write-through); text-search results are transient and
/// never cached.
/// </para>
/// <para>
/// Neither method throws on expected transport/parse failures. Inspect the returned result's
/// <see cref="NutritionLookupStatus"/> rather than catching exceptions; only a caller-requested
/// cancellation propagates.
/// </para>
/// </remarks>
public interface INutritionLookupService
{
    /// <summary>
    /// Looks up a food by barcode, cache-first. Returns a cached row when fresh; otherwise fetches
    /// from OFF and upserts the cache. Degrades to a stale cached row when OFF is unreachable.
    /// </summary>
    /// <param name="barcode">The GTIN/EAN barcode to look up. Blank input yields a not-found result.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>
    /// A <see cref="BarcodeLookupResult"/> whose <see cref="NutritionLookupStatus"/> describes the
    /// outcome; never throws on expected failures.
    /// </returns>
    Task<BarcodeLookupResult> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Open Food Facts by free-text query, returning transient (non-persisted) ranked
    /// matches. There is no cache fallback for search — an OFF outage yields a service-unavailable
    /// result.
    /// </summary>
    /// <param name="query">The free-text search query. Blank input yields an empty result.</param>
    /// <param name="page">The 1-based result page to fetch.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>
    /// A <see cref="FoodSearchResult"/> whose <see cref="NutritionLookupStatus"/> describes the
    /// outcome; never throws on expected failures.
    /// </returns>
    Task<FoodSearchResult> SearchAsync(string query, int page = 1, CancellationToken cancellationToken = default);
}
