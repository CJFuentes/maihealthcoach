using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.Nudges;

/// <summary>
/// The outcome of a nudge request. On success, <see cref="Nudge"/> carries the parsed motivational
/// message and <see cref="Disclaimer"/> the client-facing safety disclaimer. On failure,
/// <see cref="FallbackMessage"/> carries a user-friendly message and <see cref="ErrorCategory"/> the
/// underlying coaching failure category.
/// </summary>
/// <param name="IsSuccess"><see langword="true"/> when a nudge was produced successfully.</param>
/// <param name="Nudge">The parsed nudge; <see langword="null"/> on failure.</param>
/// <param name="Disclaimer">Client-facing safety disclaimer on success; <see langword="null"/> on failure.</param>
/// <param name="FallbackMessage">User-friendly fallback message on failure; <see langword="null"/> on success.</param>
/// <param name="ErrorCategory">The coaching failure category; <see cref="CoachErrorCategory.None"/> on success.</param>
public sealed record NudgeResult(
    bool IsSuccess,
    Nudge? Nudge,
    string? Disclaimer,
    string? FallbackMessage,
    CoachErrorCategory ErrorCategory)
{
    /// <summary>Creates a successful result carrying the parsed nudge and disclaimer.</summary>
    public static NudgeResult Success(Nudge nudge, string? disclaimer) =>
        new(true, nudge, disclaimer, null, CoachErrorCategory.None);

    /// <summary>Creates a failure result carrying the fallback message and error category.</summary>
    public static NudgeResult Failure(CoachErrorCategory category, string fallbackMessage) =>
        new(false, null, null, fallbackMessage, category);
}
