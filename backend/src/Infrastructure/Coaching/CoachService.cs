using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Coaching;

/// <summary>
/// The <see cref="ICoachService"/> implementation. Orchestrates prompt building, model
/// selection, and the call to <see cref="AnthropicMessagesClient"/>, then maps the outcome
/// to a <see cref="CoachResult"/>. Validates the API key lazily at call time so the
/// application boots and passes health checks without a key configured.
/// </summary>
internal sealed class CoachService : ICoachService
{
    /// <summary>Token ceiling for a single coaching reply. Generous for conversational answers.</summary>
    private const int DefaultMaxTokens = 2048;

    // ── Friendly fallback messages (never leak technical detail) ─────────────────────
    private const string FallbackConfigError =
        "The coaching feature isn't available right now. Please check back later.";
    private const string FallbackTimeout =
        "The coaching service took too long to respond. Please try again in a moment.";
    private const string FallbackUpstream =
        "The coaching service is temporarily unavailable. Please try again shortly.";
    private const string FallbackParse =
        "The coaching service returned an unexpected response. Please try again.";
    private const string FallbackGeneric =
        "Something went wrong reaching the coaching service. Please try again.";

    private readonly AnthropicMessagesClient _client;
    private readonly IOptions<AiOptions> _aiOptions;
    private readonly CoachPromptBuilder _promptBuilder;
    private readonly ILogger<CoachService> _logger;

    // Deterministic pre-screen for red-flag input. Stateless and self-contained, so it is
    // constructed directly rather than injected — no DI registration is required.
    private readonly CoachInputRiskClassifier _riskClassifier = new();

    public CoachService(
        AnthropicMessagesClient client,
        IOptions<AiOptions> aiOptions,
        CoachPromptBuilder promptBuilder,
        ILogger<CoachService> logger)
    {
        _client = client;
        _aiOptions = aiOptions;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CoachResult> AskAsync(CoachRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Count every coach request received, regardless of outcome (config error, guardrail
        // short-circuit, upstream failure, or success) — this is the custom telemetry counter (#47).
        AppTelemetry.CoachRequests.Add(1);

        var options = _aiOptions.Value;

        // 1. Lazy key guard — the only place a missing key is detected, at call time.
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            _logger.LogWarning("CoachService invoked but Ai:ApiKey is not configured.");
            return CoachResult.Failure(CoachErrorCategory.ConfigurationError, FallbackConfigError);
        }

        // 2. Risk classification — a deterministic pre-screen before any prompt is built or sent.
        var riskLevel = _riskClassifier.Classify(request.UserMessage);
        if (riskLevel == InputRiskLevel.High)
        {
            // Short-circuit: high-risk input is answered by the guardrail layer, not the model.
            // No message content is logged — only the interception event.
            _logger.LogWarning(
                "CoachService intercepted a high-risk input; returning the safety redirect without calling the model.");
            return CoachResult.Success(
                CoachSafetyResponder.HighRiskRedirectText,
                CoachSafetyResponder.GuardrailModelSentinel,
                disclaimer: CoachPromptBuilder.SafetyDisclaimer);
        }

        // 3. Model selection.
        var modelId = request.ModelTier == CoachModelTier.Escalation
            ? options.EscalationModel
            : options.DefaultModel;

        // 4. Build the prompt and user content.
        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userContent = _promptBuilder.BuildUserContent(request.UserMessage, request.Context);

        // Assemble the messages array as [...prior history turns..., final user turn]. History is
        // mapped straight through — the chat feature (#39) guarantees it is chronological, starts on
        // a user turn, and alternates. When History is null/empty (single-shot #37/#38 callers) the
        // array reduces to the single user turn, identical to the pre-history behaviour. Only the new
        // UserMessage carries the structured context (built above); prior turns are verbatim.
        var messages = new List<AnthropicMessage>();
        if (request.History is { Count: > 0 } history)
        {
            foreach (var turn in history)
            {
                messages.Add(new AnthropicMessage
                {
                    Role = turn.Role == CoachMessageRole.User ? "user" : "assistant",
                    Content = turn.Content,
                });
            }
        }

        messages.Add(new AnthropicMessage { Role = "user", Content = userContent });

        var anthropicRequest = new AnthropicRequest
        {
            Model = modelId,
            MaxTokens = DefaultMaxTokens,
            System = systemPrompt,
            Messages = messages,
        };

        // 5. Send and map.
        var result = await _client.SendAsync(anthropicRequest, cancellationToken);
        if (!result.IsSuccess)
        {
            return CoachResult.Failure(result.ErrorCategory, MapFallback(result.ErrorCategory));
        }

        var text = result.Response?.Content?
            .FirstOrDefault(b => string.Equals(b.Type, "text", StringComparison.Ordinal))?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Anthropic response contained no text content block. Model={Model}", modelId);
            return CoachResult.Failure(CoachErrorCategory.ParseError, FallbackParse);
        }

        // 6. For elevated-risk input, append the safety note to the reply (single channel).
        if (riskLevel == InputRiskLevel.Elevated)
        {
            text += Environment.NewLine + CoachSafetyResponder.ElevatedRiskSafetyNote;
        }

        return CoachResult.Success(text, result.Response!.Model ?? modelId, CoachPromptBuilder.SafetyDisclaimer);
    }

    private static string MapFallback(CoachErrorCategory category) => category switch
    {
        CoachErrorCategory.ConfigurationError => FallbackConfigError,
        CoachErrorCategory.Timeout => FallbackTimeout,
        CoachErrorCategory.UpstreamError => FallbackUpstream,
        CoachErrorCategory.ParseError => FallbackParse,
        _ => FallbackGeneric,
    };
}
