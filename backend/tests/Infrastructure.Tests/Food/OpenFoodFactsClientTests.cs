using System.Net;
using System.Text;
using MAIHealthCoach.Infrastructure.Food;
using MAIHealthCoach.Infrastructure.Tests.Coaching;
using Microsoft.Extensions.Logging.Abstractions;

namespace MAIHealthCoach.Infrastructure.Tests.Food;

/// <summary>
/// Tests for the typed <see cref="OpenFoodFactsClient"/>, exercising its outcome categorization
/// (success, not-found vs error, parse failure, timeout, transport failure) with the HTTP transport
/// faked — no test calls the real Open Food Facts network. Reuses the shared
/// <see cref="FakeHttpMessageHandler"/> from the Coaching tests (same assembly).
/// </summary>
public sealed class OpenFoodFactsClientTests
{
    private const string Barcode = "5201054000000";

    private const string FoundProductJson =
        """
        {
          "status": 1,
          "code": "5201054000000",
          "product": {
            "code": "5201054000000",
            "product_name": "Greek Yogurt",
            "nutriments": { "energy-kcal_100g": 59, "proteins_100g": 10, "carbohydrates_100g": 3.6, "fat_100g": 0.4 }
          }
        }
        """;

    private static (OpenFoodFactsClient Client, FakeHttpMessageHandler Handler) BuildClient()
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://world.openfoodfacts.org/"),
        };
        return (new OpenFoodFactsClient(httpClient, NullLogger<OpenFoodFactsClient>.Instance), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetProductAsync_RequestsTheExpectedProductPath()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => Json(HttpStatusCode.OK, FoundProductJson);

        await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.Equal($"/api/v2/product/{Barcode}.json", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetProductAsync_OnFoundProduct_ReturnsSuccessWithPayload()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => Json(HttpStatusCode.OK, FoundProductJson);

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OffErrorCategory.None, result.ErrorCategory);
        Assert.Equal("Greek Yogurt", result.Payload!.Product!.ProductName);
    }

    [Fact]
    public async Task GetProductAsync_OnStatusZeroBody_ReturnsNotFound()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) =>
            Json(HttpStatusCode.OK, """{ "status": 0, "code": "5201054000000" }""");

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.NotFound, result.ErrorCategory);
    }

    [Fact]
    public async Task GetProductAsync_On404_ReturnsNotFound()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.NotFound, result.ErrorCategory);
    }

    [Fact]
    public async Task GetProductAsync_On500_ReturnsUpstreamError()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => Json(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.UpstreamError, result.ErrorCategory);
    }

    [Fact]
    public async Task GetProductAsync_OnMalformedJson_ReturnsParseError()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => Json(HttpStatusCode.OK, "{ this is not valid json ");

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.ParseError, result.ErrorCategory);
    }

    [Fact]
    public async Task GetProductAsync_OnTransportFailure_ReturnsUpstreamError()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => throw new HttpRequestException("connection refused");

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.UpstreamError, result.ErrorCategory);
    }

    [Fact]
    public async Task GetProductAsync_OnClientTimeout_ReturnsTimeout()
    {
        var (client, handler) = BuildClient();
        // A client-side timeout surfaces as a TaskCanceledException with no caller cancellation.
        handler.ResponseFactory = (_, _) => throw new TaskCanceledException();

        var result = await client.GetProductAsync(Barcode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.Timeout, result.ErrorCategory);
    }

    [Fact]
    public async Task SearchAsync_RequestsSearchPathWithQueryAndPaging()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) =>
            Json(HttpStatusCode.OK, """{ "count": 0, "page": 1, "products": [] }""");

        await client.SearchAsync("greek yogurt", page: 2, pageSize: 20, CancellationToken.None);

        var uri = handler.LastRequest!.RequestUri!;
        Assert.Equal("/api/v2/search", uri.AbsolutePath);
        Assert.Contains("search_terms=greek%20yogurt", uri.Query);
        Assert.Contains("page=2", uri.Query);
        Assert.Contains("page_size=20", uri.Query);
    }

    [Fact]
    public async Task SearchAsync_On500_ReturnsUpstreamError()
    {
        var (client, handler) = BuildClient();
        handler.ResponseFactory = (_, _) => Json(HttpStatusCode.InternalServerError, "{}");

        var result = await client.SearchAsync("milk", page: 1, pageSize: 20, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OffErrorCategory.UpstreamError, result.ErrorCategory);
    }
}
