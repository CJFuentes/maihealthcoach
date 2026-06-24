namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Categorises a coaching call failure so callers can respond appropriately
/// without inspecting raw exception types or HTTP status codes.
/// </summary>
public enum CoachErrorCategory
{
    /// <summary>No error; the call succeeded.</summary>
    None = 0,

    /// <summary>
    /// The AI API key is missing or empty. The feature is not yet configured in this
    /// environment. The caller should surface a friendly "not available" message rather
    /// than an error.
    /// </summary>
    ConfigurationError = 1,

    /// <summary>
    /// The request timed out or the operation was cancelled. A transient condition;
    /// the caller may retry or surface a friendly message.
    /// </summary>
    Timeout = 2,

    /// <summary>
    /// The Anthropic API returned an HTTP error (4xx/5xx), or the request failed at the
    /// transport level. Logged server-side. The caller should surface the friendly fallback.
    /// </summary>
    UpstreamError = 3,

    /// <summary>
    /// The response from Anthropic could not be parsed (unexpected shape or missing content).
    /// Logged server-side.
    /// </summary>
    ParseError = 4,
}

/// <summary>
/// The result of a coaching call. Either <see cref="IsSuccess"/> is
/// <see langword="true"/> and <see cref="ReplyText"/> contains the assistant's
/// response, or <see cref="IsSuccess"/> is <see langword="false"/> and
/// <see cref="FallbackMessage"/> contains a user-friendly explanation.
/// </summary>
/// <remarks>
/// The raw HTTP status, exception details, and API key are never surfaced here.
/// They are logged server-side by the <see cref="ICoachService"/> implementation.
/// </remarks>
/// <param name="IsSuccess">
/// <see langword="true"/> when the call completed successfully.
/// </param>
/// <param name="ReplyText">
/// The assistant reply text. Non-null and non-empty when <see cref="IsSuccess"/>
/// is <see langword="true"/>; <see langword="null"/> on failure.
/// </param>
/// <param name="ModelUsed">
/// The model identifier that produced the reply (e.g. "claude-sonnet-4-6").
/// Non-null on success; <see langword="null"/> on failure. When the coach short-circuits a
/// high-risk request without calling the model, this carries the
/// <see cref="CoachSafetyResponder.GuardrailModelSentinel"/> value instead of a real model id,
/// so downstream billing/analytics can recognise and exclude guardrail short-circuits.
/// </param>
/// <param name="ErrorCategory">
/// The failure category. <see cref="CoachErrorCategory.None"/> on success.
/// </param>
/// <param name="FallbackMessage">
/// A user-friendly message the caller may display when <see cref="IsSuccess"/>
/// is <see langword="false"/>. Never leaks technical details.
/// </param>
/// <param name="Disclaimer">
/// A reusable medical/nutrition disclaimer for clients to surface beneath the reply. Populated
/// on all successful results — including the high-risk guardrail redirect — and
/// <see langword="null"/> on failure.
/// </param>
public sealed record CoachResult(
    bool IsSuccess,
    string? ReplyText,
    string? ModelUsed,
    CoachErrorCategory ErrorCategory,
    string? FallbackMessage,
    string? Disclaimer = null)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="replyText">The assistant reply text.</param>
    /// <param name="modelUsed">The model identifier that produced the reply.</param>
    /// <param name="disclaimer">
    /// An optional client-facing disclaimer to surface beneath the reply. <see langword="null"/>
    /// when no disclaimer applies.
    /// </param>
    public static CoachResult Success(string replyText, string modelUsed, string? disclaimer = null) =>
        new(true, replyText, modelUsed, CoachErrorCategory.None, null, disclaimer);

    /// <summary>Creates a failure result with a friendly fallback message.</summary>
    /// <param name="category">The failure category.</param>
    /// <param name="fallbackMessage">A user-friendly message that never leaks technical detail.</param>
    public static CoachResult Failure(CoachErrorCategory category, string fallbackMessage) =>
        new(false, null, null, category, fallbackMessage, null);
}
