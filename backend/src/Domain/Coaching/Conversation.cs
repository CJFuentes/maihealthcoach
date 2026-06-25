using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Coaching;

/// <summary>
/// A coaching chat conversation owned by a single user (issue #39). Acts as the aggregate root
/// for its <see cref="Message"/> children: messages are appended exclusively through
/// <see cref="AddMessage"/>, which keeps <see cref="MessageCount"/> in step, bumps
/// <see cref="EntityBase.UpdatedAt"/> (so the newest-activity ordering used by the conversation
/// list stays correct), and assigns each message a contiguous, 0-based
/// <see cref="Message.Sequence"/>.
/// </summary>
public sealed class Conversation : EntityBase
{
    /// <summary>Foreign key referencing the owning user's <c>Users.Id</c>.</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Optional human-readable title. <see langword="null"/> until a titling feature sets one;
    /// the chat feature currently leaves it unset.
    /// </summary>
    public string? Title { get; private set; }

    /// <summary>
    /// The number of messages appended to this conversation. Doubles as the next
    /// <see cref="Message.Sequence"/> value, so it is also the source of the per-conversation
    /// ordering key.
    /// </summary>
    public int MessageCount { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private Conversation() { }

    /// <summary>
    /// Creates a new, empty <see cref="Conversation"/> for the given user. The internal key and
    /// audit timestamps are assigned here so the entity is fully initialized before it is added
    /// to the change tracker.
    /// </summary>
    /// <param name="userId">The owning user's <c>Users.Id</c>.</param>
    /// <param name="title">An optional title. May be <see langword="null"/>.</param>
    public static Conversation Create(Guid userId, string? title = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Conversation
        {
            UserId = userId,
            Title = title,
            MessageCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Appends a message to this conversation and returns it. Assigns the next 0-based
    /// <see cref="Message.Sequence"/> from the running <see cref="MessageCount"/>, increments the
    /// count, and bumps <see cref="EntityBase.UpdatedAt"/>. The returned <see cref="Message"/> is
    /// not yet tracked — the caller adds it to the change tracker and persists both within one
    /// unit of work.
    /// </summary>
    /// <param name="role">Whether the message is from the user or the assistant.</param>
    /// <param name="content">The message body. Must not be null or whitespace.</param>
    /// <returns>The newly created <see cref="Message"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="content"/> is null or whitespace.</exception>
    public Message AddMessage(CoachMessageRole role, string content)
    {
        var message = Message.Create(
            conversationId: Id,
            userId: UserId,
            role: role,
            content: content,
            sequence: MessageCount);

        MessageCount++;
        UpdatedAt = DateTimeOffset.UtcNow;

        return message;
    }
}
