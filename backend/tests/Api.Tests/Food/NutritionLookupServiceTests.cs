using System.Net;
using System.Text;
using MAIHealthCoach.Application.Food;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// Integration tests for <see cref="NutritionLookupService"/> exercising the full cache-first
/// barcode flow and the read-through search flow over a real relational provider (SQLite in-memory)
/// with the Open Food Facts transport faked (no test ever calls the real OFF network).
/// </summary>
/// <remarks>
/// Each test owns a freshly opened in-memory SQLite connection. The service and a separate
/// read-back context share the SAME open connection so assertions reflect a genuine materialization
/// from the database rather than the change tracker's identity cache.
/// </remarks>
public sealed class NutritionLookupServiceTests
{
    private const string Barcode = "5201054000000";

    /// <summary>A found product (api/v2/product) with a full nutriments dict and a 30 g serving.</summary>
    private const string FoundProductJson =
        """
        {
          "status": 1,
          "status_verbose": "product found",
          "code": "5201054000000",
          "product": {
            "code": "5201054000000",
            "product_name": "Greek Yogurt",
            "brands": "Fage, Total",
            "serving_size": "30 g",
            "serving_quantity": 30,
            "nutriments": {
              "energy-kcal_100g": 59,
              "proteins_100g": 10,
              "carbohydrates_100g": 3.6,
              "fat_100g": 0.4,
              "sugars_100g": 3.2,
              "fiber_100g": 0,
              "saturated-fat_100g": 0.1,
              "sodium_100g": 0.036
            }
          }
        }
        """;

    /// <summary>A status-0 (not found) product envelope.</summary>
    private const string NotFoundProductJson =
        """
        { "status": 0, "status_verbose": "product not found", "code": "5201054000000" }
        """;

    /// <summary>A refreshed product with new name/nutrition and NO serving_size at all.</summary>
    private const string RefreshedProductNoServingJson =
        """
        {
          "status": 1,
          "code": "5201054000000",
          "product": {
            "code": "5201054000000",
            "product_name": "Greek Yogurt 2% (Updated)",
            "brands": "Fage",
            "nutriments": {
              "energy-kcal_100g": 73,
              "proteins_100g": 9,
              "carbohydrates_100g": 4,
              "fat_100g": 2
            }
          }
        }
        """;

    /// <summary>A two-product search page.</summary>
    private const string SearchTwoProductsJson =
        """
        {
          "count": 2,
          "page": 1,
          "products": [
            {
              "code": "1111111111111",
              "product_name": "Almond Milk",
              "brands": "Alpro",
              "serving_size": "250 ml",
              "serving_quantity": 250,
              "nutriments": { "energy-kcal_100g": 24, "proteins_100g": 0.4, "carbohydrates_100g": 2.4, "fat_100g": 1.1 }
            },
            {
              "code": "2222222222222",
              "product_name": "Oat Milk",
              "brands": "Oatly",
              "nutriments": { "energy-kcal_100g": 46, "proteins_100g": 1, "carbohydrates_100g": 6.6, "fat_100g": 1.5 }
            }
          ]
        }
        """;

    private static AppDbContext NewContext(SqliteConnection connection)
    {
        // SQLite cannot ORDER BY a DateTimeOffset, which the production cache query does on
        // LastSyncedAt. Swap in a test-only model customizer that stores DateTimeOffset as sortable
        // ticks so the SQLite-backed integration tests can exercise the real query. Production runs
        // on PostgreSQL, which orders DateTimeOffset natively, so this changes nothing under test.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetModelCustomizer>()
            .Options;
        return new AppDbContext(options);
    }

    private static OpenFoodFactsOptions DefaultOptions() => new()
    {
        BaseUrl = "https://world.openfoodfacts.org",
        UserAgent = "MAIHealthCoach.Tests/1.0",
        TimeoutSeconds = 15,
        CacheTtlDays = 30,
        SearchPageSize = 20,
    };

    private static NutritionLookupService BuildService(
        AppDbContext db,
        FakeHttpMessageHandler handler,
        OpenFoodFactsOptions? options = null)
    {
        var opts = options ?? DefaultOptions();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(opts.BaseUrl.EndsWith('/') ? opts.BaseUrl : opts.BaseUrl + "/"),
        };
        var client = new OpenFoodFactsClient(httpClient, NullLogger<OpenFoodFactsClient>.Instance);
        return new NutritionLookupService(
            db, client, Options.Create(opts), NullLogger<NutritionLookupService>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // ---- Case 1: barcode HIT from OFF on a cache miss -> Found, mapped, upserted ----

    [Fact]
    public async Task LookupByBarcode_CacheMissOffHit_ReturnsFoundMappedAndUpsertsRow()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        BarcodeLookupResult result;
        using (var ctx = NewContext(connection))
        {
            var handler = new FakeHttpMessageHandler
            {
                ResponseFactory = (_, _) => Json(HttpStatusCode.OK, FoundProductJson),
            };
            var service = BuildService(ctx, handler);

            result = await service.LookupByBarcodeAsync(Barcode);
        }

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.False(result.FromCache); // fresh fetch
        var food = result.Food!;
        Assert.Equal("Greek Yogurt", food.Name);
        Assert.Equal("Fage", food.Brand); // first of "Fage, Total"
        Assert.Equal(Barcode, food.Barcode);
        Assert.Equal(FoodSource.OpenFoodFacts, food.Source);
        Assert.Equal(Barcode, food.SourceReference);
        Assert.NotNull(food.LastSyncedAt);

        Assert.Equal(59m, food.NutritionPer100g.EnergyKcal);
        Assert.Equal(10m, food.NutritionPer100g.ProteinG);
        Assert.Equal(3.6m, food.NutritionPer100g.CarbohydrateG);
        Assert.Equal(0.4m, food.NutritionPer100g.FatG);
        Assert.Equal(3.2m, food.NutritionPer100g.SugarsG);
        Assert.Equal(0m, food.NutritionPer100g.FiberG);
        Assert.Equal(0.1m, food.NutritionPer100g.SaturatedFatG);
        Assert.Equal(36m, food.NutritionPer100g.SodiumMg); // 0.036 g -> 36 mg

        // The row was upserted: a fresh read-back over the same connection sees it persisted.
        using (var verify = NewContext(connection))
        {
            var persisted = verify.FoodItems
                .Include(f => f.ServingSizes)
                .Single(f => f.Barcode == Barcode);
            Assert.Equal("Greek Yogurt", persisted.Name);
            Assert.Equal(FoodSource.OpenFoodFacts, persisted.Source);
            // Canonical 100 g + the 30 g OFF serving.
            Assert.Equal(2, persisted.ServingSizes.Count);
            Assert.Single(persisted.ServingSizes, s => s.IsDefault);
            var off = Assert.Single(persisted.ServingSizes, s => s.GramsEquivalent == 30m);
            Assert.True(off.IsDefault);
        }
    }

    // ---- Case 4: cache miss triggers an actual HTTP call to the expected path ----

    [Fact]
    public async Task LookupByBarcode_CacheMiss_MakesHttpCallToProductPath()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => Json(HttpStatusCode.OK, FoundProductJson),
        };
        var service = BuildService(db, handler);

        await service.LookupByBarcodeAsync(Barcode);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal($"/api/v2/product/{Barcode}.json", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    // ---- Case 2: product not found upstream, no cache -> NotFound (status 0) ----

    [Fact]
    public async Task LookupByBarcode_OffStatusZeroNoCache_ReturnsNotFound()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => Json(HttpStatusCode.OK, NotFoundProductJson),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.NotFound, result.Status);
        Assert.Null(result.Food);
        Assert.False(result.FromCache);
    }

    // ---- Case 2 (HTTP 404 variant): product not found, no cache -> NotFound ----

    [Fact]
    public async Task LookupByBarcode_Off404NoCache_ReturnsNotFound()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.NotFound, result.Status);
        Assert.Null(result.Food);
    }

    // ---- Case 3: fresh cache hit -> FromCache, NO HTTP call ----

    [Fact]
    public async Task LookupByBarcode_FreshCacheHit_ReturnsFromCacheWithoutCallingOff()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        SeedFood(connection, name: "Cached Yogurt", syncedAt: DateTimeOffset.UtcNow);

        using var db = NewContext(connection);
        // A handler that throws if invoked proves no HTTP call is made on a fresh hit.
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => throw new InvalidOperationException("OFF must not be called on a fresh cache hit."),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.True(result.FromCache);
        Assert.Equal("Cached Yogurt", result.Food!.Name);
        Assert.Null(handler.LastRequest);
        Assert.Equal(0, handler.CallCount);
    }

    // ---- Case 5: stale cache + OFF reachable -> refresh in place (Id stable, no dup row) ----

    [Fact]
    public async Task LookupByBarcode_StaleCacheOffReachable_RefreshesInPlaceKeepingIdAndRowCount()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var staleId = SeedFood(
            connection,
            name: "Old Yogurt",
            syncedAt: DateTimeOffset.UtcNow.AddDays(-60)); // older than the 30-day TTL

        BarcodeLookupResult result;
        using (var db = NewContext(connection))
        {
            var handler = new FakeHttpMessageHandler
            {
                ResponseFactory = (_, _) => Json(HttpStatusCode.OK, FoundProductJson),
            };
            var service = BuildService(db, handler);
            result = await service.LookupByBarcodeAsync(Barcode);
        }

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.False(result.FromCache); // a fresh fetch occurred
        Assert.Equal(staleId, result.Food!.Id); // refreshed in place, Id stable
        Assert.Equal("Greek Yogurt", result.Food.Name); // nutrition/name updated

        using (var verify = NewContext(connection))
        {
            // Exactly one owning row for the barcode — no duplicate inserted.
            Assert.Equal(1, verify.FoodItems.Count(f => f.Barcode == Barcode));
            var refreshed = verify.FoodItems
                .Include(f => f.ServingSizes)
                .Single(f => f.Barcode == Barcode);
            Assert.Equal(staleId, refreshed.Id);
            Assert.Equal(59m, refreshed.NutritionPer100g.EnergyKcal); // updated to the OFF value
            Assert.Single(refreshed.ServingSizes, s => s.IsDefault);
        }
    }

    // ---- Case 6: stale cache + OFF transport error -> serve stale (FromCache, Found, no throw) ----

    [Fact]
    public async Task LookupByBarcode_StaleCacheOffTransportError_ServesStaleFromCache()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        SeedFood(connection, name: "Stale Yogurt", syncedAt: DateTimeOffset.UtcNow.AddDays(-60));

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => throw new HttpRequestException("connection refused"),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.True(result.FromCache);
        Assert.Equal("Stale Yogurt", result.Food!.Name);
    }

    [Fact]
    public async Task LookupByBarcode_StaleCacheOff500_ServesStaleFromCache()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        SeedFood(connection, name: "Stale Yogurt", syncedAt: DateTimeOffset.UtcNow.AddDays(-60));

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => Json(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}"),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.True(result.FromCache);
    }

    [Fact]
    public async Task LookupByBarcode_StaleCacheOffTimeout_ServesStaleFromCache()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        SeedFood(connection, name: "Stale Yogurt", syncedAt: DateTimeOffset.UtcNow.AddDays(-60));

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            // A client-timeout surfaces as TaskCanceledException with no caller cancellation.
            ResponseFactory = (_, _) => throw new TaskCanceledException(),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.True(result.FromCache);
    }

    // ---- Case 7: no cache + OFF error -> ServiceUnavailable (no throw) ----

    [Fact]
    public async Task LookupByBarcode_NoCacheOffTransportError_ReturnsServiceUnavailable()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => throw new HttpRequestException("connection refused"),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.ServiceUnavailable, result.Status);
        Assert.Null(result.Food);
        Assert.False(result.FromCache);
    }

    // ---- Case 8 (RISK 1): refresh where OFF drops the serving -> exactly one default (canonical) ----

    [Fact]
    public async Task LookupByBarcode_RefreshWhereOffDropsServing_PromotesCanonicalAsTheSoleDefault()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Seed a stale food that previously carried an OFF-derived default serving (30 g).
        Guid seededId;
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
            var food = FoodItem.Create(
                "Old Yogurt",
                FoodSource.OpenFoodFacts,
                NutritionFacts.Create(59m, 10m, 3.6m, 0.4m),
                brand: "Fage",
                barcode: Barcode,
                sourceReference: Barcode,
                lastSyncedAt: DateTimeOffset.UtcNow.AddDays(-60));
            food.AddServingSize("30 g", quantity: 30m, unit: "g", gramsEquivalent: 30m, isDefault: true);
            ctx.FoodItems.Add(food);
            ctx.SaveChanges();
            seededId = food.Id;
        }

        using (var db = NewContext(connection))
        {
            var handler = new FakeHttpMessageHandler
            {
                // Refreshed product has NO serving_size — the OFF serving is dropped.
                ResponseFactory = (_, _) => Json(HttpStatusCode.OK, RefreshedProductNoServingJson),
            };
            var service = BuildService(db, handler);
            await service.LookupByBarcodeAsync(Barcode);
        }

        using (var verify = NewContext(connection))
        {
            var refreshed = verify.FoodItems
                .Include(f => f.ServingSizes)
                .Single(f => f.Barcode == Barcode);

            Assert.Equal(seededId, refreshed.Id);
            Assert.Equal("Greek Yogurt 2% (Updated)", refreshed.Name);

            // The dropped OFF serving must NOT linger; the canonical 100 g is promoted to default.
            var defaults = refreshed.ServingSizes.Where(s => s.IsDefault).ToList();
            var sole = Assert.Single(defaults);
            Assert.Equal(100m, sole.GramsEquivalent);
            Assert.Equal("g", sole.Unit);
            // No leftover 30 g serving.
            Assert.DoesNotContain(refreshed.ServingSizes, s => s.GramsEquivalent == 30m);
        }
    }

    // ---- Case 9: search returns ranked matches, NOT persisted ----

    [Fact]
    public async Task Search_WithMatches_ReturnsRankedMatchesAndPersistsNothing()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        FoodSearchResult result;
        using (var db = NewContext(connection))
        {
            var handler = new FakeHttpMessageHandler
            {
                ResponseFactory = (_, _) => Json(HttpStatusCode.OK, SearchTwoProductsJson),
            };
            var service = BuildService(db, handler);
            result = await service.SearchAsync("milk");
        }

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.Equal(2, result.Matches.Count);

        // Ranks ascending, preserving OFF order.
        Assert.Equal(1, result.Matches[0].Rank);
        Assert.Equal("Almond Milk", result.Matches[0].Food.Name);
        Assert.Equal(2, result.Matches[1].Rank);
        Assert.Equal("Oat Milk", result.Matches[1].Food.Name);

        // Search results are read-through only — nothing was persisted.
        using (var verify = NewContext(connection))
        {
            Assert.Equal(0, verify.FoodItems.Count());
        }
    }

    // ---- Case 10: blank query -> Empty, no HTTP call ----

    [Fact]
    public async Task Search_BlankQuery_ReturnsEmptyWithoutCallingOff()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => throw new InvalidOperationException("OFF must not be called for a blank query."),
        };
        var service = BuildService(db, handler);

        var result = await service.SearchAsync("   ");

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.Empty(result.Matches);
        Assert.Null(handler.LastRequest);
        Assert.Equal(0, handler.CallCount);
    }

    // ---- Case 10: search OFF error -> ServiceUnavailable ----

    [Fact]
    public async Task Search_OffTransportError_ReturnsServiceUnavailable()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => throw new HttpRequestException("connection refused"),
        };
        var service = BuildService(db, handler);

        var result = await service.SearchAsync("milk");

        Assert.Equal(NutritionLookupStatus.ServiceUnavailable, result.Status);
        Assert.Empty(result.Matches);
    }

    // ---- Case 11: bad/partial JSON tolerance through the full service+mapper path ----

    [Fact]
    public async Task LookupByBarcode_NutrimentValuesAsStrings_ParsesWithoutThrowing()
    {
        const string stringNutrimentsJson =
            """
            {
              "status": 1,
              "code": "5201054000000",
              "product": {
                "code": "5201054000000",
                "product_name": "Stringy Cereal",
                "nutriments": {
                  "energy-kcal_100g": "250",
                  "proteins_100g": "8.5",
                  "carbohydrates_100g": "70",
                  "fat_100g": "3"
                }
              }
            }
            """;

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => Json(HttpStatusCode.OK, stringNutrimentsJson),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.Found, result.Status);
        Assert.Equal(250m, result.Food!.NutritionPer100g.EnergyKcal);
        Assert.Equal(8.5m, result.Food.NutritionPer100g.ProteinG);
        // No micros supplied -> null.
        Assert.Null(result.Food.NutritionPer100g.SugarsG);
        Assert.Null(result.Food.NutritionPer100g.SodiumMg);
    }

    [Fact]
    public async Task LookupByBarcode_ProductWithNoEnergy_IsSkippedAndReturnsNotFound()
    {
        const string noEnergyJson =
            """
            {
              "status": 1,
              "code": "5201054000000",
              "product": {
                "code": "5201054000000",
                "product_name": "Mystery Food",
                "nutriments": { "proteins_100g": 8 }
              }
            }
            """;

        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var ctx = NewContext(connection))
        {
            ctx.Database.EnsureCreated();
        }

        using var db = NewContext(connection);
        var handler = new FakeHttpMessageHandler
        {
            ResponseFactory = (_, _) => Json(HttpStatusCode.OK, noEnergyJson),
        };
        var service = BuildService(db, handler);

        var result = await service.LookupByBarcodeAsync(Barcode);

        Assert.Equal(NutritionLookupStatus.NotFound, result.Status);
        using var verify = NewContext(connection);
        Assert.Equal(0, verify.FoodItems.Count());
    }

    /// <summary>
    /// Seeds a single OFF-sourced <see cref="FoodItem"/> for <see cref="Barcode"/> with the given
    /// sync timestamp and returns its Id. Uses its own context over the supplied open connection.
    /// </summary>
    private static Guid SeedFood(SqliteConnection connection, string name, DateTimeOffset syncedAt)
    {
        using var ctx = NewContext(connection);
        ctx.Database.EnsureCreated();
        var food = FoodItem.Create(
            name,
            FoodSource.OpenFoodFacts,
            NutritionFacts.Create(50m, 5m, 5m, 1m),
            brand: "SeedBrand",
            barcode: Barcode,
            sourceReference: Barcode,
            lastSyncedAt: syncedAt);
        ctx.FoodItems.Add(food);
        ctx.SaveChanges();
        return food.Id;
    }
}
