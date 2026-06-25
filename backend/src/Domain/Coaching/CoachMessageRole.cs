namespace MAIHealthCoach.Domain.Coaching;

/// <summary>
/// The author role of a single message in a coaching conversation (issue #39). Stored as a
/// readable string in the database (see <c>MessageConfiguration</c>) and mapped to the
/// Anthropic <c>role</c> field ("user"/"assistant") when assembling a multi-turn prompt.
/// </summary>
public enum CoachMessageRole
{
    /// <summary>A message authored by the end user.</summary>
    User = 0,

    /// <summary>A message authored by the MAI assistant (the model's reply).</summary>
    Assistant = 1,
}
