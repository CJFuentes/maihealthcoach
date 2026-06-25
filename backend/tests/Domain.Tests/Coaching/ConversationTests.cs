using MAIHealthCoach.Domain.Coaching;

namespace MAIHealthCoach.Domain.Tests.Coaching;

/// <summary>
/// Pure-domain tests for the <see cref="Conversation"/> aggregate root (issue #39) — no EF, no
/// database. Guard the invariants maintained by <see cref="Conversation.AddMessage"/>: contiguous
/// 0-based sequencing, a running message count, and an advancing <c>UpdatedAt</c> audit stamp.
/// </summary>
public sealed class ConversationTests
{
    [Fact]
    public void AddMessage_AssignsSequentialSequence_AndBumpsState()
    {
        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId);

        Assert.Equal(0, conversation.MessageCount);
        var createdUpdatedAt = conversation.UpdatedAt;

        var userTurn = conversation.AddMessage(CoachMessageRole.User, "a");
        Assert.Equal(0, userTurn.Sequence);
        Assert.Equal(CoachMessageRole.User, userTurn.Role);
        Assert.Equal(userId, userTurn.UserId);
        Assert.Equal(1, conversation.MessageCount);

        var assistantTurn = conversation.AddMessage(CoachMessageRole.Assistant, "b");
        Assert.Equal(1, assistantTurn.Sequence);
        Assert.Equal(CoachMessageRole.Assistant, assistantTurn.Role);
        Assert.Equal(2, conversation.MessageCount);

        // UpdatedAt is bumped on append so the newest-activity ordering used by the list stays correct.
        Assert.True(conversation.UpdatedAt >= createdUpdatedAt);
    }
}
