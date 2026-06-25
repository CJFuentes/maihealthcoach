using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;

namespace MAIHealthCoach.Api.Tests.Notifications;

/// <summary>
/// Integration tests for the push-device registration endpoints (issue #48):
/// <c>POST /api/v1/me/devices</c>, <c>GET /api/v1/me/devices</c>, and
/// <c>DELETE /api/v1/me/devices/{id}</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Devices are
/// registered/listed/unregistered through the public API exactly as a client would. Each test uses a
/// unique <c>sub</c> claim so provisioned users never collide on the shared database, and registration
/// tokens are made unique per test so the globally-unique token index never collides across tests.
/// </remarks>
public sealed class DeviceEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string DevicesRoute = "/api/v1/me/devices";

    private readonly HttpClient _client;

    public DeviceEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Auth guard: every device route requires a bearer token ───────────────────

    [Fact]
    public async Task Register_WithNoToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DevicesRoute)
        {
            Content = JsonContent.Create(new { token = "tok", platform = "iOS", name = "phone" }),
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(DevicesRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unregister_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"{DevicesRoute}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST register ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewDevice_Returns201WithEchoedFields()
    {
        var token = TokenFor("device_register_new");
        var pushToken = Unique("apns");

        var response = await Post(token, new { token = pushToken, platform = "iOS", name = "Carlos's iPhone" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal(pushToken, body.GetProperty("token").GetString());
        Assert.Equal("iOS", body.GetProperty("platform").GetString());
        Assert.Equal("Carlos's iPhone", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Register_SameTokenAgainForSameUser_Returns200SameIdWithUpdatedMetadata()
    {
        var token = TokenFor("device_register_refresh");
        var pushToken = Unique("fcm");

        var first = await Post(token, new { token = pushToken, platform = "iOS", name = "Old name" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Re-register the same token: an upsert/refresh, not a new row → 200 with the same id.
        var second = await Post(token, new { token = pushToken, platform = "Android", name = "New name" });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstId, body.GetProperty("id").GetGuid());
        Assert.Equal("Android", body.GetProperty("platform").GetString());
        Assert.Equal("New name", body.GetProperty("name").GetString());

        // The list still has exactly one device for this user.
        var list = await (await Get(token, DevicesRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Register_LowercasePlatform_SucceedsCaseInsensitively()
    {
        var token = TokenFor("device_register_lowercase");

        var response = await Post(token, new { token = Unique("web"), platform = "ios", name = "lower" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Persisted/echoed as the canonical enum name regardless of request casing.
        Assert.Equal("iOS", body.GetProperty("platform").GetString());
    }

    // ── POST validation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_EmptyToken_Returns400WithFieldError()
    {
        var token = TokenFor("device_register_empty_token");

        var response = await Post(token, new { token = "", platform = "iOS", name = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Register_InvalidPlatform_Returns400WithFieldError()
    {
        var token = TokenFor("device_register_bad_platform");

        var response = await Post(token, new { token = Unique("tok"), platform = "Symbian", name = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("platform", out _));
    }

    // ── GET list ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsOnlyCallersDevicesNewestFirst()
    {
        var token = TokenFor("device_list_order");

        var firstToken = Unique("dev1");
        var secondToken = Unique("dev2");
        await Post(token, new { token = firstToken, platform = "iOS", name = "first" });
        await Post(token, new { token = secondToken, platform = "Android", name = "second" });

        var list = await (await Get(token, DevicesRoute)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, list.GetArrayLength());
        // Ordered by CreatedAt descending → the second registration is first in the list.
        Assert.Equal(secondToken, list[0].GetProperty("token").GetString());
        Assert.Equal(firstToken, list[1].GetProperty("token").GetString());
    }

    [Fact]
    public async Task List_DoesNotReturnAnotherUsersDevices()
    {
        var userAToken = TokenFor("device_list_xuser_a");
        var userBToken = TokenFor("device_list_xuser_b");

        var aPushToken = Unique("a_dev");
        var bPushToken = Unique("b_dev");
        await Post(userAToken, new { token = aPushToken, platform = "iOS", name = "a-phone" });
        await Post(userBToken, new { token = bPushToken, platform = "Web", name = "b-phone" });

        var aList = await (await Get(userAToken, DevicesRoute)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, aList.GetArrayLength());
        Assert.Equal(aPushToken, aList[0].GetProperty("token").GetString());
    }

    // ── DELETE ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unregister_Existing_Returns204ThenGoneFromList()
    {
        var token = TokenFor("device_delete");

        var created = await Post(token, new { token = Unique("del"), platform = "iOS", name = "x" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await Delete(token, $"{DevicesRoute}/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var list = await (await Get(token, DevicesRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, list.GetArrayLength());
    }

    [Fact]
    public async Task Unregister_NonExistentId_Returns404()
    {
        var token = TokenFor("device_delete_unknown");

        var response = await Delete(token, $"{DevicesRoute}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unregister_AnotherUsersDevice_Returns404AndDeviceSurvives()
    {
        var ownerToken = TokenFor("device_delete_xuser_owner");
        var attackerToken = TokenFor("device_delete_xuser_attacker");

        var created = await Post(ownerToken, new { token = Unique("owned"), platform = "iOS", name = "owned" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Cross-user delete is indistinguishable from a missing row → 404 (not 403).
        var attackerResponse = await Delete(attackerToken, $"{DevicesRoute}/{id}");
        Assert.Equal(HttpStatusCode.NotFound, attackerResponse.StatusCode);

        // The owner still sees the device — it was not deleted.
        var ownerList = await (await Get(ownerToken, DevicesRoute)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, ownerList.GetArrayLength());
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    // A globally-unique push token per call, so the unique-token index never collides across tests
    // sharing the in-memory database.
    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private async Task<HttpResponseMessage> Post(string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, DevicesRoute)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Get(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Delete(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
