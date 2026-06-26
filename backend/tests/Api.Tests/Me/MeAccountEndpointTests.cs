using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests.Me;

/// <summary>
/// Integration tests for the GDPR account endpoints (issue #46):
/// <c>GET /api/v1/me/data-export</c> (right of access / portability),
/// <c>DELETE /api/v1/me</c> (right of erasure), and the anonymous
/// <c>GET /api/v1/privacy-policy</c>.
/// </summary>
/// <remarks>
/// Reuses the signed-JWT SQLite harness (<see cref="AuthTestWebApplicationFactory"/>). Owned data is
/// either created through the public API exactly as a client would (water) or seeded directly into
/// the shared in-memory database via a service scope (custom foods/activities, conversations) — the
/// same scope is then reopened to assert per-table emptiness after deletion. Each test uses a unique
/// <c>sub</c> claim so provisioned users never collide on the shared database.
/// </remarks>
public sealed class MeAccountEndpointTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private const string DataExportRoute = "/api/v1/me/data-export";
    private const string AccountRoute = "/api/v1/me";
    private const string MeRoute = "/api/v1/me";
    private const string WaterRoute = "/api/v1/me/water";
    private const string PrivacyPolicyRoute = "/api/v1/privacy-policy";

    private readonly AuthTestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MeAccountEndpointTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Auth guards ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DataExport_WithNoToken_Returns401()
    {
        var response = await _client.GetAsync(DataExportRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithNoToken_Returns401()
    {
        var response = await _client.DeleteAsync(AccountRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PrivacyPolicy_WithNoToken_Returns200()
    {
        // The privacy policy must be readable without an account.
        var response = await _client.GetAsync(PrivacyPolicyRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/markdown", response.Content.Headers.ContentType?.ToString());

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Privacy Policy", body);
    }

    // ── Data export ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task DataExport_NewUserNoData_ReturnsEmptySections()
    {
        var token = TokenFor("export_empty");

        var response = await Get(token, DataExportRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The export is offered as a downloadable attachment.
        Assert.True(response.Content.Headers.TryGetValues("Content-Disposition", out var disposition));
        Assert.Contains("attachment", string.Join(";", disposition!));

        var export = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("1.0", export.GetProperty("schemaVersion").GetString());
        Assert.Equal("export_empty@test.local", export.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, export.GetProperty("profile").ValueKind);

        AssertEmptyArray(export, "waterLog");
        AssertEmptyArray(export, "foodDiary");
        AssertEmptyArray(export, "customFoods");
        AssertEmptyArray(export, "favoriteFoods");
        AssertEmptyArray(export, "exerciseLog");
        AssertEmptyArray(export, "customExerciseActivities");
        AssertEmptyArray(export, "coachConversations");
        AssertEmptyArray(export, "devices");
        Assert.Equal(JsonValueKind.Null, export.GetProperty("reminderPreferences").ValueKind);
    }

    [Fact]
    public async Task DataExport_WithSeededData_IncludesUsersData()
    {
        var token = TokenFor("export_with_data");

        // (a) Provision the user and capture their id so the seeded custom food is owned by them.
        var userId = await ProvisionUser(token);

        // (b) A water entry created through the public API, exactly as a client would.
        var waterResponse = await Post(token, WaterRoute, new { amountMl = 500, date = "2026-06-24" });
        Assert.Equal(HttpStatusCode.Created, waterResponse.StatusCode);

        // (c) A custom food owned by this user, seeded directly into the shared database.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FoodItems.Add(FoodItem.CreateCustom(
                userId, "My Custom Food", NutritionFacts.Create(100m, 10m, 5m, 2m)));
            db.SaveChanges();
        }

        // (d) The export must surface both the water entry and the custom food.
        var response = await Get(token, DataExportRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var export = await response.Content.ReadFromJsonAsync<JsonElement>();

        var waterLog = export.GetProperty("waterLog");
        Assert.Equal(1, waterLog.GetArrayLength());
        Assert.Equal(500, waterLog[0].GetProperty("amountMl").GetInt32());

        var customFoodNames = export.GetProperty("customFoods")
            .EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("My Custom Food", customFoodNames);
    }

    // ── Account deletion ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_RemovesAllOwnedData_AndReturns204()
    {
        var userAToken = TokenFor("delete_user_a");
        var userBToken = TokenFor("delete_user_b");

        // User A is provisioned and seeded across every owned table.
        var userAId = await ProvisionUser(userAToken);
        await Post(userAToken, WaterRoute, new { amountMl = 500, date = "2026-06-24" });

        Guid sharedFoodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.FoodItems.Add(FoodItem.CreateCustom(
                userAId, "A Custom Food", NutritionFacts.Create(120m, 8m, 14m, 3m)));
            db.ExerciseActivities.Add(ExerciseActivity.CreateCustom(
                userAId, "A Custom Run", ExerciseCategory.Cardio, 8.0m));

            var conversation = Conversation.Create(userAId);
            var message = conversation.AddMessage(CoachMessageRole.User, "hi");
            db.Conversations.Add(conversation);
            db.CoachMessages.Add(message);

            // Push notification data owned by A (issue #48): a device + a reminder-preferences row.
            db.DeviceRegistrations.Add(DeviceRegistration.Create(
                userAId, "a-push-token", DevicePlatform.Web, "A's Browser"));
            db.ReminderPreferences.Add(ReminderPreferences.Create(userAId));

            // A SHARED catalog food (CreatedByUserId == null) that must survive A's deletion.
            var sharedFood = FoodItem.Create(
                "Shared OFF Food", FoodSource.OpenFoodFacts, NutritionFacts.Create(50m, 1m, 1m, 1m));
            db.FoodItems.Add(sharedFood);

            db.SaveChanges();
            sharedFoodId = sharedFood.Id;
        }

        // User B is an unrelated account whose data must be untouched by A's deletion.
        var userBId = await ProvisionUser(userBToken);
        await Post(userBToken, WaterRoute, new { amountMl = 750, date = "2026-06-24" });
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FoodItems.Add(FoodItem.CreateCustom(
                userBId, "B Custom Food", NutritionFacts.Create(90m, 5m, 12m, 1m)));
            db.SaveChanges();
        }

        var deleteResponse = await Delete(userAToken, AccountRoute);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Every table owned by User A is empty.
            Assert.False(db.Users.Any(u => u.Id == userAId));
            Assert.False(db.WaterLogEntries.Any(e => e.UserId == userAId));
            Assert.False(db.UserFavoriteFoods.Any(f => f.UserId == userAId));
            Assert.False(db.DiaryEntries.Any(e => e.UserId == userAId));
            Assert.False(db.ExerciseLogEntries.Any(e => e.UserId == userAId));
            Assert.False(db.Conversations.Any(c => c.UserId == userAId));
            Assert.False(db.CoachMessages.Any(m => m.UserId == userAId));
            Assert.False(db.FoodItems.Any(f => f.CreatedByUserId == userAId));
            Assert.False(db.ExerciseActivities.Any(a => a.CreatedByUserId == userAId));
            Assert.False(db.UserProfiles.Any(p => p.UserId == userAId));
            Assert.False(db.UserGoalTargets.Any(t => t.UserId == userAId));
            Assert.False(db.DeviceRegistrations.Any(d => d.UserId == userAId));
            Assert.False(db.ReminderPreferences.Any(r => r.UserId == userAId));

            // User B and the shared catalog row are untouched.
            Assert.True(db.Users.Any(u => u.Id == userBId));
            Assert.True(db.WaterLogEntries.Any(e => e.UserId == userBId));
            Assert.True(db.FoodItems.Any(f => f.CreatedByUserId == userBId));
            Assert.True(db.FoodItems.Any(f => f.Id == sharedFoodId));
        }
    }

    [Fact]
    public async Task DeleteAccount_IsIdempotent_DeletingTwiceStill204OrCleanly()
    {
        var token = TokenFor("delete_idempotent");
        await ProvisionUser(token);

        var first = await Delete(token, AccountRoute);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // The endpoint resolves the caller via GetOrCreateCurrentUserAsync, so the second
        // authenticated DELETE re-provisions the user from the same token and then deletes the
        // freshly-created (empty) account — which is still a clean 204 NoContent.
        var second = await Delete(token, AccountRoute);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string TokenFor(string sub) =>
        JwtTestHelper.CreateToken(sub, $"{sub}@test.local");

    private static void AssertEmptyArray(JsonElement export, string property)
    {
        var array = export.GetProperty(property);
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.Equal(0, array.GetArrayLength());
    }

    /// <summary>
    /// Provisions the user behind <paramref name="token"/> via <c>GET /api/v1/me</c> and returns
    /// their local id, so seeded data can be owner-scoped to the same user the API resolves.
    /// </summary>
    private async Task<Guid> ProvisionUser(string token)
    {
        var response = await Get(token, MeRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

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

    private async Task<HttpResponseMessage> Delete(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
