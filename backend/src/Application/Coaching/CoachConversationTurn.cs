using MAIHealthCoach.Domain.Coaching;

namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// A single prior turn in a coaching conversation, supplied as multi-turn context on a
/// <see cref="CoachRequest"/> (issue #39). Reuses the Domain <see cref="CoachMessageRole"/> so the
/// chat feature can map persisted <c>Message</c> rows straight through without a second enum.
/// </summary>
/// <param name="Role">Whether the turn was authored by the user or the assistant.</param>
/// <param name="Content">The turn's text content.</param>
public sealed record CoachConversationTurn(CoachMessageRole Role, string Content);
