using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Coaching;

/// <summary>
/// The outcome of a low-level Anthropic Messages API call. Carries either the parsed
/// response or a typed failure category plus a server-side-only detail string. The
/// <see cref="ErrorDetail"/> is for logging only and is never surfaced to API clients.
/// </summary>
internal sealed class AnthropicClientResult
{
    private AnthropicClientResult(
        bool isSuccess,
        AnthropicResponse? response,
        CoachErrorCategory errorCategory,
        string? errorDetail)
    {
        IsSuccess = isSuccess;
        Response = response;
        ErrorCategory = errorCategory;
        ErrorDetail = errorDetail;
    }

    /// <summary>Whether the call succeeded and <see cref="Response"/> is populated.</summary>
    public bool IsSuccess { get; }

    /// <summary>The parsed response on success; otherwise <see langword="null"/>.</summary>
    public AnthropicResponse? Response { get; }

    /// <summary>The failure category. <see cref="CoachErrorCategory.None"/> on success.</summary>
    public CoachErrorCategory ErrorCategory { get; }

    /// <summary>Server-side-only diagnostic detail. Never returned to API clients.</summary>
    public string? ErrorDetail { get; }

    public static AnthropicClientResult Success(AnthropicResponse response) =>
        new(true, response, CoachErrorCategory.None, null);

    public static AnthropicClientResult Failure(CoachErrorCategory category, string detail) =>
        new(false, null, category, detail);
}

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper around the Anthropic Messages API. The
/// <c>BaseAddress</c> and <c>Timeout</c> are configured at registration time from
/// <see cref="AiOptions"/>; the secret <c>x-api-key</c> header is resolved and attached
/// <em>per request</em> (lazily), so an empty key never blocks application startup.
/// Failures are mapped to a typed <see cref="AnthropicClientResult"/> rather than thrown.
/// </summary>
internal sealed class AnthropicMessagesClient
{
    private const string AnthropicVersionHeader = "anthropic-version";
    private const string AnthropicVersion = "2023-06-01";
    private const string ApiKeyHeader = "x-api-key";

    // No leading slash: combined against a BaseAddress that ends with '/'.
    private const string MessagesPath = "v1/messages";

    // Cap how much of an upstream error body is logged, to avoid unbounded log entries.
    private const int MaxLoggedBodyLength = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly ILogger<AnthropicMessagesClient> _logger;

    public AnthropicMessagesClient(
        HttpClient httpClient,
        IOptions<AiOptions> aiOptions,
        ILogger<AnthropicMessagesClient> logger)
    {
        _httpClient = httpClient;
        _aiOptions = aiOptions;
        _logger = logger;
    }

    /// <summary>
    /// Sends a Messages API request and maps the outcome to a typed result. Never throws on
    /// expected transport/parse failures.
    /// </summary>
    internal async Task<AnthropicClientResult> SendAsync(
        AnthropicRequest request,
        CancellationToken cancellationToken)
    {
        // Resolve the key lazily, at call time — not at construction/registration.
        var apiKey = _aiOptions.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AnthropicClientResult.Failure(
                CoachErrorCategory.ConfigurationError,
                "Ai:ApiKey is not configured.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, MessagesPath)
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };
        httpRequest.Headers.Add(ApiKeyHeader, apiKey);
        httpRequest.Headers.Add(AnthropicVersionHeader, AnthropicVersion);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout fired (not a caller-requested cancellation). Treat as a timeout.
            _logger.LogWarning("Anthropic API call timed out. Model={Model}", request.Model);
            return AnthropicClientResult.Failure(CoachErrorCategory.Timeout, "Request timed out.");
        }
        // A caller-requested cancellation (token cancelled) propagates as OperationCanceledException.
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Anthropic API transport error. Model={Model}", request.Model);
            return AnthropicClientResult.Failure(CoachErrorCategory.UpstreamError, "Transport error.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadBodySafelyAsync(response, cancellationToken);
                var truncatedBody = errorBody.Length > MaxLoggedBodyLength
                    ? errorBody[..MaxLoggedBodyLength] + "…"
                    : errorBody;
                _logger.LogError(
                    "Anthropic API returned {StatusCode}. Model={Model}. Body={Body}",
                    (int)response.StatusCode,
                    request.Model,
                    truncatedBody);
                return AnthropicClientResult.Failure(
                    CoachErrorCategory.UpstreamError,
                    $"HTTP {(int)response.StatusCode}");
            }

            AnthropicResponse? parsed;
            try
            {
                parsed = await response.Content.ReadFromJsonAsync<AnthropicResponse>(cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize Anthropic response. Model={Model}", request.Model);
                return AnthropicClientResult.Failure(CoachErrorCategory.ParseError, "Malformed response.");
            }

            if (parsed is null)
            {
                _logger.LogError("Anthropic response deserialized to null. Model={Model}", request.Model);
                return AnthropicClientResult.Failure(CoachErrorCategory.ParseError, "Null response.");
            }

            return AnthropicClientResult.Success(parsed);
        }
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
