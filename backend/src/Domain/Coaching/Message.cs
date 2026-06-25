using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.Coaching;

/// <summary>
/// A single message within a coaching <see cref="Conversation"/> (issue #39), authored either by
/// the user or the assistant. Messages are immutable once created — a turn is appended, never
/// edited — and are minted exclusively through <see cref="Conversation.AddMessage"/> so the
/// conversation can assign a contiguous, 0-based <see cref="Sequence"/> and keep
/// <see cref="Conversation.MessageCount"/> in step.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="UserId"/> is denormalized from the owning <see cref="Conversation"/> (mirroring
/// <c>DiaryEntry.UserId</c>) so user-scoped reads — and the per-user authorization predicate —
/// can be satisfied without joining back through the conversation.
/// </para>
/// <para>
/// <see cref="Sequence"/> exists because two messages in the same user/assistant pair are
/// persisted in one <c>SaveChanges</c> and can share an identical <see cref="EntityBase.CreatedAt"/>.
/// Ordering by <see cref="Sequence"/> (rather than by timestamp) therefore reliably places the
/// user turn before the assistant turn.
/// </para>
/// </remarks>
public sealed class Message : EntityBase
{
    /// <summary>Foreign key referencing the owning <c>Conversations.Id</c>.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>
    /// Foreign key referencing the owning user's <c>Users.Id</c>. Denormalized from the
    /// conversation so messages can be queried and authorized per user without a join.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>Whether this message was authored by the user or the assistant.</summary>
    public CoachMessageRole Role { get; private set; }

    /// <summary>The message body. Never <see langword="null"/>.</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// The 0-based position of this message within its conversation, assigned by
    /// <see cref="Conversation.AddMessage"/>. Primary ordering key for reads.
    /// </summary>
    public int Sequence { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private Message() { }

    /// <summary>
    /// Creates a new <see cref="Message"/>. The internal key and audit timestamps are assigned
    /// here so the entity is fully initialized before it is added to the change tracker. Intended
    /// to be called from <see cref="Conversation.AddMessage"/>, which supplies the
    /// <paramref name="sequence"/> from the conversation's running count.
    /// </summary>
    /// <param name="conversationId">The owning <c>Conversations.Id</c>.</param>
    /// <param name="userId">The owning user's <c>Users.Id</c>.</param>
    /// <param name="role">Whether the message is from the user or the assistant.</param>
    /// <param name="content">The message body. Must not be null or whitespace.</param>
    /// <param name="sequence">The 0-based position within the conversation.</param>
    /// <exception cref="ArgumentException"><paramref name="content"/> is null or whitespace.</exception>
    public static Message Create(
        Guid conversationId,
        Guid userId,
        CoachMessageRole role,
        string content,
        int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var now = DateTimeOffset.UtcNow;
        return new Message
        {
            ConversationId = conversationId,
            UserId = userId,
            Role = role,
            Content = content,
            Sequence = sequence,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
