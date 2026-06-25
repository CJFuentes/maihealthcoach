namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Selects which Claude model tier to use for a coaching request.
/// </summary>
public enum CoachModelTier
{
    /// <summary>Use the default model (claude-sonnet-4-6). Suitable for most coaching interactions.</summary>
    Default = 0,

    /// <summary>
    /// Use the escalation model (claude-opus-4-8). Reserved for complex requests that benefit
    /// from higher capability at increased cost and latency.
    /// </summary>
    Escalation = 1,
}

/// <summary>
/// Encapsulates a single coaching request: the structured user context, the user's
/// free-text message, and the model tier to use.
/// </summary>
/// <param name="UserMessage">
/// The user's free-text message or question directed to MAI. Must not be null or whitespace.
/// </param>
/// <param name="Context">
/// Optional structured user context (goals, today's intake, preferences). Pass
/// <see cref="CoachingContext.Empty"/> or <see langword="null"/> when no context is available.
/// </param>
/// <param name="ModelTier">
/// Selects the Claude model tier. Defaults to <see cref="CoachModelTier.Default"/>.
/// </param>
/// <param name="History">
/// Prior conversation turns (chronological, user/assistant), supplied by the chat feature (#39)
/// to enable multi-turn context. Null/empty for single-shot callers (#37/#38).
/// </param>
public sealed record CoachRequest(
    string UserMessage,
    CoachingContext? Context = null,
    CoachModelTier ModelTier = CoachModelTier.Default,
    IReadOnlyList<CoachConversationTurn>? History = null);
