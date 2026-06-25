using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Domain.Coaching;

namespace MAIHealthCoach.Api.Tests.Coach;

/// <summary>
/// Integration tests for the authenticated coach chat endpoints (issue #39):
/// <c>POST /api/v1/me/coach/chat</c>, <c>GET /api/v1/me/coach/chat</c>, and
/// <c>GET /api/v1/me/coach/chat/{conversationId}</c>. Reuses the signed-JWT harness and in-memory
/// SQLite database, swapping the coach service for a stub so the tests are deterministic and never
/// call Anthropic. Each test uses a unique <c>sub</c> claim to avoid cross-test state (the SQLite DB
/// is shared across the class), and sets <c>StubService.Handler</c> explicitly so it never relies on
/// state left over from a prior test (tests in a class run sequentially).
/// </summary>
public sealed class ChatEndpointTests : IClassFixture<CoachTestWebApplicationFactory>
{
    private const string ChatRoute = "/api/v1/me/coach/chat";

    private const string StubReply = "Here is some friendly coaching advice.";

    private readonly CoachTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChatEndpointTests(CoachTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guard ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChat_NoToken_Returns401()
    {
        ResetStubToReply();

        // POST send with no bearer.
        var post = await _client.PostAsJsonAsync(ChatRoute, new { message = "Hello" });
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);

        // GET list with no bearer.
        var list = await _client.GetAsync(ChatRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        // GET detail with no bearer.
        var detail = await _client.GetAsync($"{ChatRoute}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    // ── Validation: empty / whitespace message => 400 ProblemDetails ─────────────

    [Fact]
    public async Task SendChat_EmptyMessage_Returns400()
    {
        ResetStubToReply();
        var token = TokenFor("chat_empty_message");

        var empty = await Post(token, ChatRoute, new { message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains(
            "application/problem+json",
            empty.Content.Headers.ContentType?.MediaType ?? string.Empty);

        var whitespace = await Post(token, ChatRoute, new { message = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, whitespace.StatusCode);
        Assert.Contains(
            "application/problem+json",
            whitespace.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── Validation: message longer than MaxMessageLength => 400 ──────────────────

    [Fact]
    public async Task SendChat_OverlongMessage_Returns400()
    {
        ResetStubToReply();
        var token = TokenFor("chat_overlong_message");

        // Default MaxMessageLength is 4000; one character over the limit must be rejected.
        var response = await Post(token, ChatRoute, new { message = new string('x', 4001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    // ── New conversation: persists the pair and returns the reply ────────────────

    [Fact]
    public async Task SendChat_NewConversation_PersistsAndReturnsReply()
    {
        ResetStubToReply();
        var token = TokenFor("chat_new_conversation");

        const string userMessage = "How much protein should I eat?";

        var sendResponse = await Post(token, ChatRoute, new { message = userMessage });
        Assert.Equal(HttpStatusCode.OK, sendResponse.StatusCode);

        var sendBody = await sendResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(StubReply, sendBody.GetProperty("reply").GetString());
        var conversationId = sendBody.GetProperty("conversationId").GetGuid();
        Assert.NotEqual(Guid.Empty, conversationId);
        Assert.NotEqual(Guid.Empty, sendBody.GetProperty("messageId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(sendBody.GetProperty("disclaimer").GetString()));

        // The new conversation appears in the list with both turns counted.
        var listResponse = await Get(token, ChatRoute);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conversations = listBody.GetProperty("conversations");
        Assert.Equal(JsonValueKind.Array, conversations.ValueKind);

        var summary = conversations.EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conversationId);
        Assert.Equal(2, summary.GetProperty("messageCount").GetInt32());

        // The detail view returns both messages in order: user turn first, then assistant.
        var detailResponse = await Get(token, $"{ChatRoute}/{conversationId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detailBody = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();

        var messages = detailBody.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());

        var first = messages[0];
        Assert.Equal("user", first.GetProperty("role").GetString());
        Assert.Equal(userMessage, first.GetProperty("content").GetString());

        var second = messages[1];
        Assert.Equal("assistant", second.GetProperty("role").GetString());
        Assert.Equal(StubReply, second.GetProperty("content").GetString());
    }

    // ── Multi-turn: prior history flows to the coach on the follow-up send ───────

    [Fact]
    public async Task SendChat_MultiTurn_PassesPriorHistoryToCoach()
    {
        ResetStubToReply();
        var token = TokenFor("chat_multi_turn");

        const string firstMessage = "What is a good breakfast?";

        var firstSend = await Post(token, ChatRoute, new { message = firstMessage });
        Assert.Equal(HttpStatusCode.OK, firstSend.StatusCode);
        var firstBody = await firstSend.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = firstBody.GetProperty("conversationId").GetGuid();

        // Capture the request the second send hands to the coach.
        CoachRequest? captured = null;
        _factory.StubService.Handler = req =>
        {
            captured = req;
            return CoachResult.Success(StubReply, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);
        };

        const string secondMessage = "And what about lunch?";
        var secondSend = await Post(token, ChatRoute, new { message = secondMessage, conversationId });
        Assert.Equal(HttpStatusCode.OK, secondSend.StatusCode);

        Assert.NotNull(captured);
        Assert.NotNull(captured!.History);
        Assert.Equal(2, captured.History!.Count);

        Assert.Equal(CoachMessageRole.User, captured.History[0].Role);
        Assert.Equal(firstMessage, captured.History[0].Content);

        Assert.Equal(CoachMessageRole.Assistant, captured.History[1].Role);
        Assert.Equal(StubReply, captured.History[1].Content);

        // The new turn's message is carried on the request's UserMessage (with prompt scaffolding).
        Assert.Contains(secondMessage, captured.UserMessage);

        ResetStubToReply();
    }

    // ── Cross-user isolation: another user's conversation is 404, not readable ───

    [Fact]
    public async Task GetConversation_OtherUser_Returns404()
    {
        ResetStubToReply();
        var tokenA = TokenFor("chat_isolation_user_a");
        var tokenB = TokenFor("chat_isolation_user_b");

        var send = await Post(tokenA, ChatRoute, new { message = "User A's private message" });
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var conversationId = (await send.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();

        // User B cannot read user A's conversation.
        var detail = await Get(tokenB, $"{ChatRoute}/{conversationId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        // Nor append to it.
        var crossPost = await Post(tokenB, ChatRoute, new { message = "Sneaky", conversationId });
        Assert.Equal(HttpStatusCode.NotFound, crossPost.StatusCode);
    }

    // ── Missing conversation => 404 ──────────────────────────────────────────────

    [Fact]
    public async Task GetConversation_Missing_Returns404()
    {
        ResetStubToReply();
        var token = TokenFor("chat_missing_conversation");

        var response = await Get(token, $"{ChatRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── List scoping: a user only sees their own conversations ───────────────────

    [Fact]
    public async Task ListConversations_OnlyOwnConversations()
    {
        ResetStubToReply();
        var tokenA = TokenFor("chat_list_scope_user_a");
        var tokenB = TokenFor("chat_list_scope_user_b");

        var sendA = await Post(tokenA, ChatRoute, new { message = "User A starts a chat" });
        Assert.Equal(HttpStatusCode.OK, sendA.StatusCode);
        var conversationA = (await sendA.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("conversationId").GetGuid();

        // User B has not chatted: their list is empty and never includes A's conversation.
        var listB = await Get(tokenB, ChatRoute);
        Assert.Equal(HttpStatusCode.OK, listB.StatusCode);
        var listBodyB = await listB.Content.ReadFromJsonAsync<JsonElement>();
        var conversationsB = listBodyB.GetProperty("conversations");
        Assert.DoesNotContain(
            conversationsB.EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationA);
        Assert.Empty(conversationsB.EnumerateArray());

        // User A's own list contains exactly their conversation.
        var listA = await Get(tokenA, ChatRoute);
        var listBodyA = await listA.Content.ReadFromJsonAsync<JsonElement>();
        var conversationsA = listBodyA.GetProperty("conversations");
        Assert.Contains(
            conversationsA.EnumerateArray(),
            c => c.GetProperty("id").GetGuid() == conversationA);
    }

    // ── Coach failure => error status and nothing persisted ──────────────────────

    [Fact]
    public async Task SendChat_CoachConfigError_Returns503_AndPersistsNothing()
    {
        var token = TokenFor("chat_config_error");

        _factory.StubService.Handler =
            _ => CoachResult.Failure(CoachErrorCategory.ConfigurationError, "not configured");

        var response = await Post(token, ChatRoute, new { message = "Will this be persisted?" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // No conversation was created — the failed send persisted nothing.
        var listResponse = await Get(token, ChatRoute);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(listBody.GetProperty("conversations").EnumerateArray());

        ResetStubToReply();
    }

    [Fact]
    public async Task SendChat_CoachUpstreamError_Returns502_AndPersistsNothing()
    {
        var token = TokenFor("chat_upstream_error");

        _factory.StubService.Handler =
            _ => CoachResult.Failure(CoachErrorCategory.UpstreamError, "temporarily unavailable");

        var response = await Post(token, ChatRoute, new { message = "Will this be persisted?" });
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var listResponse = await Get(token, ChatRoute);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(listBody.GetProperty("conversations").EnumerateArray());

        ResetStubToReply();
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private void ResetStubToReply() =>
        _factory.StubService.Handler =
            _ => CoachResult.Success(StubReply, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Post(string token, string route, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
