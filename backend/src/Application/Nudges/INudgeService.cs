namespace MAIHealthCoach.Application.Nudges;

/// <summary>
/// Produces short, encouraging motivational nudges personalised to a user's recent adherence and
/// streaks (issue #38) — celebrating wins and gently re-motivating after lapses. Orchestrates a
/// coaching call via <c>ICoachService</c> (so #41 guardrails and the safety disclaimer apply) and
/// parses the reply into a structured nudge. Lives entirely in the Application layer — it performs
/// no HTTP work itself and never throws on expected coaching failures (inspect
/// <see cref="NudgeResult.IsSuccess"/>).
/// </summary>
public interface INudgeService
{
    /// <summary>
    /// Builds a coaching prompt from the supplied nudge request, asks MAI for a short motivational
    /// message, and parses the reply into a <see cref="NudgeResult"/>.
    /// </summary>
    /// <param name="request">The optional streak/adherence signals and optional profile context.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A success result with a parsed nudge, or a graceful failure with a fallback message.</returns>
    Task<NudgeResult> GetNudgeAsync(NudgeRequest request, CancellationToken cancellationToken = default);
}
