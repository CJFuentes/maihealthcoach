using System.Net;
using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Infrastructure.Coaching;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Tests.Coaching;

/// <summary>
/// Unit tests for the low-level <see cref="AnthropicMessagesClient"/>. All HTTP is faked.
/// </summary>
public sealed class AnthropicMessagesClientTests
{
    private const string SuccessResponseJson =
        """
        {
          "id": "msg_abc",
          "model": "claude-sonnet-4-6",
          "content": [ { "type": "text", "text": "ok" } ],
          "stop_reason": "end_turn"
        }
        """;

    [Fact]
    public async Task SendAsync_SetsHeadersAndPostsToMessagesEndpoint()
    {
        var (client, handler) = BuildSut(apiKey: "sk-ant-xyz");
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        await client.SendAsync(BuildRequest(), CancellationToken.None);

        var request = handler.LastRequest!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("v1/messages", request.RequestUri!.AbsoluteUri);
        Assert.Equal("sk-ant-xyz", Assert.Single(request.Headers.GetValues("x-api-key")));
        Assert.Equal("2023-06-01", Assert.Single(request.Headers.GetValues("anthropic-version")));
    }

    [Fact]
    public async Task SendAsync_SerializesSnakeCaseProperties()
    {
        var (client, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        await client.SendAsync(BuildRequest(), CancellationToken.None);

        var body = handler.LastRequestBody!;
        Assert.Contains("\"model\":\"claude-sonnet-4-6\"", body);
        Assert.Contains("\"max_tokens\":", body);
        Assert.Contains("\"system\":", body);
        Assert.Contains("\"messages\":", body);
        Assert.Contains("\"role\":\"user\"", body);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_ReturnsParsedResponse()
    {
        var (client, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        var result = await client.SendAsync(BuildRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Response);
        Assert.Equal("ok", result.Response!.Content![0].Text);
        Assert.Equal(CoachErrorCategory.None, result.ErrorCategory);
    }

    [Fact]
    public async Task SendAsync_OnNonSuccessStatus_ReturnsUpstreamError()
    {
        var (client, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.TooManyRequests, "{\"error\":\"rate\"}");

        var result = await client.SendAsync(BuildRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.UpstreamError, result.ErrorCategory);
    }

    [Fact]
    public async Task SendAsync_OnCancellation_ReturnsTimeout()
    {
        var (client, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => throw new TaskCanceledException();

        var result = await client.SendAsync(BuildRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.Timeout, result.ErrorCategory);
    }

    [Fact]
    public async Task SendAsync_WhenCallerCancels_PropagatesOperationCanceled()
    {
        var (client, handler) = BuildSut();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        handler.ResponseFactory = (_, _) => throw new OperationCanceledException(cts.Token);

        // Caller-requested cancellation must propagate, not be masked as a Timeout result.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.SendAsync(BuildRequest(), cts.Token));
    }

    [Fact]
    public async Task SendAsync_OnTransportError_ReturnsUpstreamError()
    {
        var (client, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => throw new HttpRequestException("connection refused");

        var result = await client.SendAsync(BuildRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.UpstreamError, result.ErrorCategory);
    }

    [Fact]
    public async Task SendAsync_WithEmptyApiKey_ReturnsConfigurationErrorWithoutHttpCall()
    {
        var (client, handler) = BuildSut(apiKey: "");

        var result = await client.SendAsync(BuildRequest(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.ConfigurationError, result.ErrorCategory);
        Assert.Null(handler.LastRequest);
    }

    private static AnthropicRequest BuildRequest() => new()
    {
        Model = "claude-sonnet-4-6",
        MaxTokens = 2048,
        System = "system prompt",
        Messages = [new AnthropicMessage { Role = "user", Content = "hi" }],
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static (AnthropicMessagesClient Client, FakeHttpMessageHandler Handler) BuildSut(
        string apiKey = "sk-ant-test-key")
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.anthropic.com/"),
            Timeout = TimeSpan.FromSeconds(30),
        };

        var aiOptions = Options.Create(new AiOptions
        {
            ApiKey = apiKey,
            BaseUrl = "https://api.anthropic.com",
            TimeoutSeconds = 30,
        });

        var client = new AnthropicMessagesClient(
            httpClient, aiOptions, NullLogger<AnthropicMessagesClient>.Instance);

        return (client, handler);
    }
}
