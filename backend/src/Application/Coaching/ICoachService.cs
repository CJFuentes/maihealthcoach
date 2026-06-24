namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Brokers all interactions with the Claude AI coaching model. The implementation lives
/// in the Infrastructure layer; this interface allows Application-layer features (#37
/// meal suggestions, #38 nudges, #39 chat history) to depend on it without depending on
/// Infrastructure.
/// </summary>
/// <remarks>
/// The implementation is responsible for:
/// <list type="bullet">
///   <item><description>Keeping the API key server-side — it must never appear in responses or logs.</description></item>
///   <item><description>Applying system-prompt guardrails (MAI persona, scope, medical disclaimer).</description></item>
///   <item><description>Returning a <see cref="CoachResult"/> rather than throwing on expected failures
///   (missing key, timeout, HTTP error) so the caller can degrade gracefully.</description></item>
/// </list>
/// </remarks>
public interface ICoachService
{
    /// <summary>
    /// Sends a coaching request to Claude and returns a structured result. Never throws on
    /// expected failure conditions — inspect <see cref="CoachResult.IsSuccess"/> and
    /// <see cref="CoachResult.ErrorCategory"/> instead.
    /// </summary>
    /// <param name="request">The coaching request including user message and context.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>
    /// A <see cref="CoachResult"/> that is either a successful reply or a graceful
    /// failure with a friendly fallback message.
    /// </returns>
    Task<CoachResult> AskAsync(CoachRequest request, CancellationToken cancellationToken = default);
}
