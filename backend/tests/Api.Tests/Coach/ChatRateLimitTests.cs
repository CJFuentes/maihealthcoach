using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Api.Tests.Coach;

/// <summary>
/// Integration test for the per-user rate limit on <c>POST /api/v1/me/coach/chat</c> (issue #39).
/// Bound to <see cref="ChatRateLimitTestWebApplicationFactory"/>, which lowers
/// <c>CoachChat:PermitLimit</c> to 2 and owns its own host (and therefore its own rate-limiter
/// partition store), so the limiter state here never leaks into the other chat tests. The single
/// test uses a <c>sub</c> unique to this case so its fixed-window bucket starts empty.
/// </summary>
public sealed class ChatRateLimitTests : IClassFixture<ChatRateLimitTestWebApplicationFactory>
{
    private const string ChatRoute = "/api/v1/me/coach/chat";

    private readonly ChatRateLimitTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ChatRateLimitTests(ChatRateLimitTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SendChat_RateLimitExceeded_Returns429()
    {
        _factory.StubService.Handler =
            _ => CoachResult.Success(
                "Here is some friendly coaching advice.",
                "claude-sonnet-4-6",
                CoachPromptBuilder.SafetyDisclaimer);

        var token = JwtTestHelper.CreateToken("chat_rate_limit", "chat_rate_limit@test.local");

        // PermitLimit is 2 for this host: the first two sends succeed, the third is rejected.
        var first = await Post(token, new { message = "First message" });
        var second = await Post(token, new { message = "Second message" });
        var third = await Post(token, new { message = "Third message" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);

        // The 429 carries problem+json and a Retry-After header.
        Assert.Contains(
            "application/problem+json",
            third.Content.Headers.ContentType?.MediaType ?? string.Empty);
        Assert.True(third.Headers.Contains("Retry-After"));
    }

    private async Task<HttpResponseMessage> Post(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ChatRoute)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
