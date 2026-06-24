using MAIHealthCoach.Domain.Food;

namespace MAIHealthCoach.Application.Food;

/// <summary>
/// The result of a barcode lookup. Either <see cref="Status"/> is
/// <see cref="NutritionLookupStatus.Found"/> and <see cref="Food"/> carries the resolved (and
/// persisted) <see cref="FoodItem"/>, or it is a graceful failure with a <see langword="null"/>
/// <see cref="Food"/>. The lookup follows a cache-first contract and never throws on expected
/// transport/parse failures — inspect <see cref="Status"/> instead.
/// </summary>
/// <remarks>
/// <see cref="ErrorDetail"/> is for server-side logging only and must never be surfaced to API
/// clients. <see cref="FromCache"/> indicates whether the food was served from the Postgres cache
/// (including a stale-but-present cache fallback during an OFF outage) rather than a fresh fetch.
/// </remarks>
public sealed record BarcodeLookupResult
{
    private BarcodeLookupResult(
        NutritionLookupStatus status,
        FoodItem? food,
        bool fromCache,
        string? errorDetail)
    {
        Status = status;
        Food = food;
        FromCache = fromCache;
        ErrorDetail = errorDetail;
    }

    /// <summary>The lookup outcome category.</summary>
    public NutritionLookupStatus Status { get; }

    /// <summary>The resolved food on success; otherwise <see langword="null"/>.</summary>
    public FoodItem? Food { get; }

    /// <summary>
    /// <see langword="true"/> when the food came from the cache (fresh hit or stale fallback);
    /// <see langword="false"/> for a fresh OFF fetch or any non-found result.
    /// </summary>
    public bool FromCache { get; }

    /// <summary>Server-side-only diagnostic detail. Never returned to clients.</summary>
    public string? ErrorDetail { get; }

    /// <summary>Creates a successful result served from the Postgres cache.</summary>
    /// <param name="food">The cached food.</param>
    public static BarcodeLookupResult FromCached(FoodItem food) =>
        new(NutritionLookupStatus.Found, food, true, null);

    /// <summary>Creates a successful result from a fresh Open Food Facts fetch.</summary>
    /// <param name="food">The freshly fetched (and persisted) food.</param>
    public static BarcodeLookupResult Fetched(FoodItem food) =>
        new(NutritionLookupStatus.Found, food, false, null);

    /// <summary>Creates a not-found result (no upstream product and nothing cached).</summary>
    public static BarcodeLookupResult NotFound() =>
        new(NutritionLookupStatus.NotFound, null, false, null);

    /// <summary>
    /// Creates a service-unavailable result (OFF unreachable and no cache fallback).
    /// </summary>
    /// <param name="detail">Server-side-only diagnostic detail; never surfaced to clients.</param>
    public static BarcodeLookupResult Unavailable(string detail) =>
        new(NutritionLookupStatus.ServiceUnavailable, null, false, detail);
}
