namespace MAIHealthCoach.Application.Food;

/// <summary>
/// Outcome category for a nutrition lookup (barcode or search), so callers can branch on the
/// result without inspecting raw exceptions or HTTP status codes. Mirrors the graceful-degradation
/// contract: an Open Food Facts outage with a usable cache fallback is still <see cref="Found"/>,
/// not <see cref="ServiceUnavailable"/>.
/// </summary>
public enum NutritionLookupStatus
{
    /// <summary>A matching food was found (from cache or a fresh fetch).</summary>
    Found,

    /// <summary>The product/query genuinely yielded no result upstream and none was cached.</summary>
    NotFound,

    /// <summary>
    /// Open Food Facts was unreachable (timeout/transport/parse error) <em>and</em> there was no
    /// cache fallback to serve. A transient condition; the caller should surface a friendly message.
    /// </summary>
    ServiceUnavailable,
}
