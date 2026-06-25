namespace MAIHealthCoach.Api.Features.Coach;

/// <summary>
/// Request body for <c>POST /api/v1/me/coach/chat</c> (issue #39). The author role is
/// server-assigned (always the user), so it is not part of the contract.
/// </summary>
/// <param name="Message">The user's chat message. Must not be empty.</param>
/// <param name="ConversationId">
/// The conversation to append to, or <see langword="null"/> to start a new conversation.
/// </param>
public sealed record SendChatMessageRequest(string Message, Guid? ConversationId);

/// <summary>
/// Response body for a successful <c>POST /api/v1/me/coach/chat</c>. Identifies the conversation
/// the turn belongs to and carries the assistant's reply.
/// </summary>
/// <param name="ConversationId">The conversation the message pair was appended to.</param>
/// <param name="MessageId">The id of the persisted assistant message.</param>
/// <param name="Reply">The assistant's reply text.</param>
/// <param name="Disclaimer">A client-facing safety disclaimer to surface beneath the reply.</param>
/// <param name="ModelUsed">The model identifier that produced the reply, if known.</param>
/// <param name="CreatedAt">The creation timestamp of the assistant message.</param>
public sealed record SendChatMessageResponse(
    Guid ConversationId,
    Guid MessageId,
    string Reply,
    string? Disclaimer,
    string? ModelUsed,
    DateTimeOffset CreatedAt);

/// <summary>
/// Summary projection of a conversation for the list view (<c>GET /api/v1/me/coach/chat</c>).
/// </summary>
/// <param name="Id">The conversation id.</param>
/// <param name="Title">The optional conversation title.</param>
/// <param name="MessageCount">The number of messages in the conversation.</param>
/// <param name="CreatedAt">When the conversation was created.</param>
/// <param name="UpdatedAt">When the conversation last had activity.</param>
public sealed record ConversationSummaryResponse(
    Guid Id,
    string? Title,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Response body for <c>GET /api/v1/me/coach/chat</c> — the user's conversations, newest first.
/// </summary>
/// <param name="Conversations">The conversation summaries.</param>
public sealed record ConversationListResponse(IReadOnlyList<ConversationSummaryResponse> Conversations);

/// <summary>
/// A single message within a conversation detail view.
/// </summary>
/// <param name="Id">The message id.</param>
/// <param name="Role">The author role as a lowercase string ("user" or "assistant").</param>
/// <param name="Content">The message body.</param>
/// <param name="CreatedAt">When the message was created.</param>
public sealed record ChatMessageResponse(
    Guid Id,
    string Role,
    string Content,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response body for <c>GET /api/v1/me/coach/chat/{conversationId}</c> — a conversation and its
/// ordered messages.
/// </summary>
/// <param name="Id">The conversation id.</param>
/// <param name="Title">The optional conversation title.</param>
/// <param name="Messages">The conversation's messages in chronological order.</param>
public sealed record ConversationDetailResponse(
    Guid Id,
    string? Title,
    IReadOnlyList<ChatMessageResponse> Messages);
