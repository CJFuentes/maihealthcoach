using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MAIHealthCoach.Infrastructure.Food;

/// <summary>
/// Categorises an Open Food Facts call outcome so the service can branch without inspecting raw
/// exceptions or HTTP status codes. <see cref="NotFound"/> is a <em>successful</em> "no product"
/// outcome (404 or a status-0 body), distinct from a transport/parse failure.
/// </summary>
internal enum OffErrorCategory
{
    /// <summary>No error; the call succeeded with a payload.</summary>
    None = 0,

    /// <summary>The product/query genuinely yielded no result upstream (404 or status 0).</summary>
    NotFound = 1,

    /// <summary>The request timed out (the client timeout fired, not a caller cancellation).</summary>
    Timeout = 2,

    /// <summary>A non-2xx (non-404) response or a transport-level failure.</summary>
    UpstreamError = 3,

    /// <summary>The response body could not be parsed into the expected shape.</summary>
    ParseError = 4,
}

/// <summary>
/// The outcome of a low-level Open Food Facts call. Carries either the parsed payload or a typed
/// failure category plus a server-side-only detail string. <see cref="ErrorDetail"/> is for logging
/// only and is never surfaced to API clients.
/// </summary>
internal sealed class OffClientResult<T>
    where T : class
{
    private OffClientResult(bool isSuccess, T? payload, OffErrorCategory errorCategory, string? errorDetail)
    {
        IsSuccess = isSuccess;
        Payload = payload;
        ErrorCategory = errorCategory;
        ErrorDetail = errorDetail;
    }

    /// <summary>Whether the call succeeded and <see cref="Payload"/> is populated.</summary>
    public bool IsSuccess { get; }

    /// <summary>The parsed payload on success; otherwise <see langword="null"/>.</summary>
    public T? Payload { get; }

    /// <summary>The outcome category. <see cref="OffErrorCategory.None"/> on success.</summary>
    public OffErrorCategory ErrorCategory { get; }

    /// <summary>Server-side-only diagnostic detail. Never returned to API clients.</summary>
    public string? ErrorDetail { get; }

    public static OffClientResult<T> Success(T payload) =>
        new(true, payload, OffErrorCategory.None, null);

    public static OffClientResult<T> NotFound() =>
        new(false, null, OffErrorCategory.NotFound, null);

    public static OffClientResult<T> Failure(OffErrorCategory category, string detail) =>
        new(false, null, category, detail);
}

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper around the Open Food Facts api/v2 endpoints. The
/// <c>BaseAddress</c>, <c>Timeout</c>, and required <c>User-Agent</c> are configured at registration
/// time from <c>OpenFoodFactsOptions</c>. Failures are mapped to a typed
/// <see cref="OffClientResult{T}"/> rather than thrown; a 404 (or status-0 body) is reported as a
/// successful not-found rather than an error.
/// </summary>
internal sealed class OpenFoodFactsClient
{
    // Cap how much of an upstream error body is logged, to avoid unbounded log entries.
    private const int MaxLoggedBodyLength = 500;

    // Fields requested on search to keep payloads small and parsing predictable.
    private const string SearchFields = "code,product_name,brands,serving_size,serving_quantity,nutriments";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenFoodFactsClient> _logger;

    public OpenFoodFactsClient(HttpClient httpClient, ILogger<OpenFoodFactsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches a single product by barcode. A 404 or a status-0 body is reported as a successful
    /// <see cref="OffErrorCategory.NotFound"/>. Never throws on expected transport/parse failures.
    /// </summary>
    internal async Task<OffClientResult<OffProductResponse>> GetProductAsync(
        string barcode,
        CancellationToken cancellationToken)
    {
        // No leading slash: combined against a BaseAddress that ends with '/'.
        var path = $"api/v2/product/{Uri.EscapeDataString(barcode)}.json";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(path, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Open Food Facts product call timed out. Barcode={Barcode}", barcode);
            return OffClientResult<OffProductResponse>.Failure(OffErrorCategory.Timeout, "Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Open Food Facts product transport error. Barcode={Barcode}", barcode);
            return OffClientResult<OffProductResponse>.Failure(OffErrorCategory.UpstreamError, "Transport error.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return OffClientResult<OffProductResponse>.NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                await LogUpstreamErrorAsync(response, $"product Barcode={barcode}", cancellationToken);
                return OffClientResult<OffProductResponse>.Failure(
                    OffErrorCategory.UpstreamError,
                    $"HTTP {(int)response.StatusCode}");
            }

            OffProductResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<OffProductResponse>(SerializerOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Open Food Facts product. Barcode={Barcode}", barcode);
                return OffClientResult<OffProductResponse>.Failure(OffErrorCategory.ParseError, "Malformed response.");
            }

            if (parsed is null)
            {
                _logger.LogError("Open Food Facts product deserialized to null. Barcode={Barcode}", barcode);
                return OffClientResult<OffProductResponse>.Failure(OffErrorCategory.ParseError, "Null response.");
            }

            // A status-0 body (or absent product) is a genuine not-found, not an error.
            if (parsed.Status == 0 || parsed.Product is null)
            {
                return OffClientResult<OffProductResponse>.NotFound();
            }

            return OffClientResult<OffProductResponse>.Success(parsed);
        }
    }

    /// <summary>
    /// Searches products by free-text query. Never throws on expected transport/parse failures.
    /// </summary>
    internal async Task<OffClientResult<OffSearchResponse>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var path =
            $"api/v2/search?search_terms={Uri.EscapeDataString(query)}&page={page}&page_size={pageSize}&fields={SearchFields}&json=1";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(path, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Open Food Facts search call timed out. Query={Query}", query);
            return OffClientResult<OffSearchResponse>.Failure(OffErrorCategory.Timeout, "Request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Open Food Facts search transport error. Query={Query}", query);
            return OffClientResult<OffSearchResponse>.Failure(OffErrorCategory.UpstreamError, "Transport error.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await LogUpstreamErrorAsync(response, $"search Query={query}", cancellationToken);
                return OffClientResult<OffSearchResponse>.Failure(
                    OffErrorCategory.UpstreamError,
                    $"HTTP {(int)response.StatusCode}");
            }

            OffSearchResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<OffSearchResponse>(SerializerOptions, cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Open Food Facts search. Query={Query}", query);
                return OffClientResult<OffSearchResponse>.Failure(OffErrorCategory.ParseError, "Malformed response.");
            }

            if (parsed is null)
            {
                _logger.LogError("Open Food Facts search deserialized to null. Query={Query}", query);
                return OffClientResult<OffSearchResponse>.Failure(OffErrorCategory.ParseError, "Null response.");
            }

            return OffClientResult<OffSearchResponse>.Success(parsed);
        }
    }

    private async Task LogUpstreamErrorAsync(
        HttpResponseMessage response,
        string context,
        CancellationToken cancellationToken)
    {
        var errorBody = await ReadBodySafelyAsync(response, cancellationToken);
        var truncatedBody = errorBody.Length > MaxLoggedBodyLength
            ? errorBody[..MaxLoggedBodyLength] + "…"
            : errorBody;
        _logger.LogError(
            "Open Food Facts returned {StatusCode}. {Context}. Body={Body}",
            (int)response.StatusCode,
            context,
            truncatedBody);
    }

    private static async Task<string> ReadBodySafelyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return "<unreadable>";
        }
        catch (OperationCanceledException)
        {
            return "<unreadable>";
        }
    }
}
