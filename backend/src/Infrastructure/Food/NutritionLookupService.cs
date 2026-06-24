using MAIHealthCoach.Application.Food;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Food;

/// <summary>
/// Cache-first <see cref="INutritionLookupService"/> over Open Food Facts. Barcode lookups consult
/// the Postgres cache first and only call OFF on a miss or staleness; results are written through.
/// When OFF is unreachable, a present (even stale) cached row is served. Text searches are
/// read-through only and never persisted. Never throws on expected transport/parse failures.
/// </summary>
internal sealed class NutritionLookupService : INutritionLookupService
{
    private readonly AppDbContext _db;
    private readonly OpenFoodFactsClient _offClient;
    private readonly OpenFoodFactsOptions _options;
    private readonly ILogger<NutritionLookupService> _logger;

    public NutritionLookupService(
        AppDbContext db,
        OpenFoodFactsClient offClient,
        IOptions<OpenFoodFactsOptions> options,
        ILogger<NutritionLookupService> logger)
    {
        _db = db;
        _offClient = offClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BarcodeLookupResult> LookupByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        // Blank barcodes can never match a real product; treat as a not-found without a round trip.
        var normalized = barcode?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return BarcodeLookupResult.NotFound();
        }

        // Deterministically pick the freshest cache row (re-used GTINs/regional variants can collide).
        var cached = await _db.FoodItems
            .Include(f => f.ServingSizes)
            .Where(f => f.Barcode == normalized && f.Source == FoodSource.OpenFoodFacts)
            .OrderByDescending(f => f.LastSyncedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // Fresh cache hit: serve directly, no HTTP call.
        if (cached is not null && IsFresh(cached, now))
        {
            return BarcodeLookupResult.FromCached(cached);
        }

        var clientResult = await _offClient.GetProductAsync(normalized, cancellationToken);

        // OFF reachable and product present: upsert and serve fresh.
        if (clientResult.IsSuccess && clientResult.Payload?.Product is { } product)
        {
            return await UpsertFromProductAsync(product, normalized, cached, cancellationToken);
        }

        // OFF says the product genuinely does not exist upstream.
        if (clientResult.ErrorCategory == OffErrorCategory.NotFound)
        {
            // A previously-cached row is still valid data — serve it rather than nothing.
            return cached is not null
                ? BarcodeLookupResult.FromCached(cached)
                : BarcodeLookupResult.NotFound();
        }

        // OFF transport/timeout/parse error: graceful degradation to stale cache if present.
        if (cached is not null)
        {
            _logger.LogWarning(
                "Open Food Facts unavailable ({Category}); serving stale cache. Barcode={Barcode}",
                clientResult.ErrorCategory,
                normalized);
            return BarcodeLookupResult.FromCached(cached);
        }

        return BarcodeLookupResult.Unavailable(clientResult.ErrorDetail ?? clientResult.ErrorCategory.ToString());
    }

    /// <inheritdoc />
    public async Task<FoodSearchResult> SearchAsync(
        string query,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var normalized = query?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return FoodSearchResult.Empty(page);
        }

        var clientResult = await _offClient.SearchAsync(normalized, page, _options.SearchPageSize, cancellationToken);

        if (!clientResult.IsSuccess)
        {
            // No cache fallback for search — search hits are never persisted.
            return FoodSearchResult.Unavailable(
                clientResult.ErrorDetail ?? clientResult.ErrorCategory.ToString());
        }

        var products = clientResult.Payload?.Products;
        if (products is null || products.Count == 0)
        {
            return FoodSearchResult.Success([], page);
        }

        var matches = new List<FoodSearchMatch>(products.Count);
        var rank = 0;
        foreach (var product in products)
        {
            rank++;
            if (!OpenFoodFactsMapper.TryMapProduct(product, out var mapped))
            {
                continue;
            }

            // Build a transient (non-persisted) FoodItem. A malformed record must not throw out.
            try
            {
                var food = FoodItem.Create(
                    mapped.Name,
                    FoodSource.OpenFoodFacts,
                    mapped.Nutrition,
                    brand: mapped.Brand,
                    barcode: mapped.Barcode,
                    sourceReference: mapped.SourceReference);

                if (mapped.Serving is { } serving)
                {
                    food.AddServingSize(serving.Label, serving.Quantity, serving.Unit, serving.GramsEquivalent, isDefault: true);
                }

                matches.Add(new FoodSearchMatch(food, rank));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping malformed Open Food Facts search result. Code={Code}",
                    mapped.Barcode);
            }
        }

        return FoodSearchResult.Success(matches, page);
    }

    /// <summary>
    /// Maps and persists a fetched product: refreshes the existing cache row in place (keeping its
    /// Id stable) or inserts a new one. A malformed record never throws out of the service — on
    /// failure it falls back to the stale cache when present, else not-found.
    /// </summary>
    private async Task<BarcodeLookupResult> UpsertFromProductAsync(
        OffProduct product,
        string barcode,
        FoodItem? cached,
        CancellationToken cancellationToken)
    {
        if (!OpenFoodFactsMapper.TryMapProduct(product, out var mapped))
        {
            return cached is not null ? BarcodeLookupResult.FromCached(cached) : BarcodeLookupResult.NotFound();
        }

        var syncedAt = DateTimeOffset.UtcNow;
        FoodItem food;
        try
        {
            if (cached is null)
            {
                food = FoodItem.Create(
                    mapped.Name,
                    FoodSource.OpenFoodFacts,
                    mapped.Nutrition,
                    brand: mapped.Brand,
                    barcode: barcode,
                    sourceReference: mapped.SourceReference,
                    lastSyncedAt: syncedAt);

                if (mapped.Serving is { } serving)
                {
                    food.AddServingSize(serving.Label, serving.Quantity, serving.Unit, serving.GramsEquivalent, isDefault: true);
                }

                _db.FoodItems.Add(food);
            }
            else
            {
                cached.RefreshFromSource(
                    mapped.Name,
                    mapped.Brand,
                    mapped.Nutrition,
                    mapped.Serving,
                    mapped.SourceReference,
                    syncedAt);
                food = cached;
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Malformed Open Food Facts product could not be persisted. Barcode={Barcode}",
                barcode);
            return cached is not null ? BarcodeLookupResult.FromCached(cached) : BarcodeLookupResult.NotFound();
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // A transient persistence failure must never throw out of the service. Degrade to the
            // stale cache when present (the in-memory mutations are simply not persisted this time),
            // otherwise report the cache layer as unavailable.
            _logger.LogError(
                ex,
                "Failed to persist Open Food Facts product. Barcode={Barcode}",
                barcode);
            return cached is not null
                ? BarcodeLookupResult.FromCached(cached)
                : BarcodeLookupResult.Unavailable("Persistence error.");
        }

        return BarcodeLookupResult.Fetched(food);
    }

    /// <summary>
    /// A cached row is fresh when it has a sync timestamp within the configured TTL window.
    /// A <see langword="null"/> <c>LastSyncedAt</c> is always treated as stale.
    /// </summary>
    private bool IsFresh(FoodItem cached, DateTimeOffset now) =>
        cached.LastSyncedAt is { } syncedAt
        && now - syncedAt < TimeSpan.FromDays(_options.CacheTtlDays);
}
