using System.Net;
using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Infrastructure.Coaching;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Tests.Coaching;

/// <summary>
/// Unit tests for <see cref="CoachService"/>. All HTTP is faked — no test calls the real
/// Anthropic API.
/// </summary>
public sealed class CoachServiceTests
{
    private const string DefaultModel = "claude-sonnet-4-6";
    private const string EscalationModel = "claude-opus-4-8";

    private const string SuccessResponseJson =
        """
        {
          "id": "msg_test_123",
          "model": "claude-sonnet-4-6",
          "content": [ { "type": "text", "text": "Here is your coaching advice." } ],
          "stop_reason": "end_turn",
          "usage": { "input_tokens": 100, "output_tokens": 50 }
        }
        """;

    [Fact]
    public async Task AskAsync_WithValidKeyAndDefaultTier_ReturnsSuccessWithReplyAndModel()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        var result = await service.AskAsync(new CoachRequest("How much protein should I eat?"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Here is your coaching advice.", result.ReplyText);
        Assert.Equal(DefaultModel, result.ModelUsed);
        Assert.Equal(CoachErrorCategory.None, result.ErrorCategory);
        Assert.Null(result.FallbackMessage);
    }

    [Fact]
    public async Task AskAsync_DefaultTier_SerializesDefaultModelInRequestBody()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        await service.AskAsync(new CoachRequest("Hi", ModelTier: CoachModelTier.Default));

        Assert.Contains($"\"model\":\"{DefaultModel}\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task AskAsync_EscalationTier_SerializesEscalationModelInRequestBody()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        await service.AskAsync(new CoachRequest("Hi", ModelTier: CoachModelTier.Escalation));

        Assert.Contains($"\"model\":\"{EscalationModel}\"", handler.LastRequestBody);
    }

    [Fact]
    public async Task AskAsync_WithEmptyApiKey_ReturnsConfigurationErrorWithoutCallingApi()
    {
        var (service, handler) = BuildSut(apiKey: "");

        var result = await service.AskAsync(new CoachRequest("Hello"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.ConfigurationError, result.ErrorCategory);
        Assert.NotNull(result.FallbackMessage);
        Assert.Null(handler.LastRequest); // No HTTP call was attempted.
    }

    [Fact]
    public async Task AskAsync_WhenHandlerCancels_ReturnsTimeoutCategory()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => throw new TaskCanceledException();

        var result = await service.AskAsync(new CoachRequest("Hello"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.Timeout, result.ErrorCategory);
        Assert.NotNull(result.FallbackMessage);
    }

    [Fact]
    public async Task AskAsync_WhenApiReturns500_ReturnsUpstreamError()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.InternalServerError, "{\"error\":\"boom\"}");

        var result = await service.AskAsync(new CoachRequest("Hello"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.UpstreamError, result.ErrorCategory);
    }

    [Fact]
    public async Task AskAsync_WhenResponseIsMalformedJson_ReturnsParseError()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, "{ this is not valid json ");

        var result = await service.AskAsync(new CoachRequest("Hello"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.ParseError, result.ErrorCategory);
    }

    [Fact]
    public async Task AskAsync_WhenResponseHasNoTextBlock_ReturnsParseError()
    {
        var (service, handler) = BuildSut();
        var noText = """{ "id": "x", "model": "claude-sonnet-4-6", "content": [], "stop_reason": "end_turn" }""";
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, noText);

        var result = await service.AskAsync(new CoachRequest("Hello"));

        Assert.False(result.IsSuccess);
        Assert.Equal(CoachErrorCategory.ParseError, result.ErrorCategory);
    }

    [Fact]
    public async Task AskAsync_SendsRequiredHeadersAndSystemGuardrails()
    {
        var (service, handler) = BuildSut(apiKey: "sk-ant-secret-123");
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        await service.AskAsync(new CoachRequest("What should I eat?"));

        var request = handler.LastRequest!;
        Assert.True(request.Headers.TryGetValues("x-api-key", out var keys));
        Assert.Equal("sk-ant-secret-123", Assert.Single(keys));
        Assert.True(request.Headers.TryGetValues("anthropic-version", out var versions));
        Assert.Equal("2023-06-01", Assert.Single(versions));
        Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);

        // System prompt (with the MAI persona + medical disclaimer guardrail) must be present.
        Assert.Contains("MAI", handler.LastRequestBody);
        Assert.Contains("not a substitute for professional medical", handler.LastRequestBody, StringComparison.OrdinalIgnoreCase);

        // The user message and the user role must be present.
        Assert.Contains("\"role\":\"user\"", handler.LastRequestBody);
        Assert.Contains("What should I eat?", handler.LastRequestBody);
    }

    [Fact]
    public async Task AskAsync_WithPopulatedContext_IncludesContextSectionsInUserContent()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        var context = new CoachingContext(PrimaryGoal: "Lose weight", DailyCalorieTarget: 1800);
        await service.AskAsync(new CoachRequest("Suggest a dinner", context));

        Assert.Contains("Primary goal: Lose weight", handler.LastRequestBody);
        Assert.Contains("1800 kcal", handler.LastRequestBody);
    }

    [Fact]
    public async Task AskAsync_WithNullContext_OmitsContextSections()
    {
        var (service, handler) = BuildSut();
        handler.ResponseFactory = (_, _) => JsonResponse(HttpStatusCode.OK, SuccessResponseJson);

        await service.AskAsync(new CoachRequest("Just a plain question", Context: null));

        Assert.DoesNotContain("## Your Goals", handler.LastRequestBody);
        Assert.DoesNotContain("Today's Intake", handler.LastRequestBody);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };

    private static (CoachService Service, FakeHttpMessageHandler Handler) BuildSut(
        string apiKey = "sk-ant-test-key",
        string defaultModel = DefaultModel,
        string escalationModel = EscalationModel)
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
            DefaultModel = defaultModel,
            EscalationModel = escalationModel,
            BaseUrl = "https://api.anthropic.com",
            TimeoutSeconds = 30,
        });

        var messagesClient = new AnthropicMessagesClient(
            httpClient, aiOptions, NullLogger<AnthropicMessagesClient>.Instance);

        var service = new CoachService(
            messagesClient, aiOptions, new CoachPromptBuilder(), NullLogger<CoachService>.Instance);

        return (service, handler);
    }
}
