using System.Globalization;
using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Domain.Users;
using MAIHealthCoach.Infrastructure.Notifications;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MAIHealthCoach.Api.Tests.Notifications;

/// <summary>
/// Integration tests for the push-reminder background sweep (issue #48). These exercise the "sender
/// abstraction invoked (mocked)" acceptance criterion by running the service's real per-tick logic
/// (<c>RunTickAsync</c>) against a seeded database with a capturing
/// <see cref="FakePushNotificationSender"/>, rather than waiting on the (minimum 60-second) timer
/// loop. The decision of <em>who</em> is due is unit-tested exhaustively in <c>ReminderDeciderTests</c>;
/// these tests assert the dispatch wiring around that decision: a due user's device receives a payload,
/// a non-due user's does not, and the seam is a singleton the host actually resolves.
/// </summary>
public sealed class PushReminderBackgroundServiceTests
    : IClassFixture<PushReminderTestWebApplicationFactory>
{
    private const string TimeFormat = "HH:mm";

    private readonly PushReminderTestWebApplicationFactory _factory;

    public PushReminderBackgroundServiceTests(PushReminderTestWebApplicationFactory factory)
    {
        _factory = factory;
        // Force the host (and its SQLite schema) to build before any test seeds the database.
        _ = factory.Services;
    }

    // ── Wiring: the seam is a registered singleton and the hosted sweep is present ──

    [Fact]
    public void Sender_IsRegisteredAsSingleton_AndIsTheFake()
    {
        using var scope = _factory.Services.CreateScope();
        var sender = scope.ServiceProvider
            .GetRequiredService<Application.Notifications.IPushNotificationSender>();

        Assert.Same(_factory.Sender, sender);
    }

    [Fact]
    public void PushReminderBackgroundService_IsRegisteredAsHostedService()
    {
        var hosted = _factory.Services.GetServices<IHostedService>();
        Assert.Contains(hosted, h => h is PushReminderBackgroundService);
    }

    // ── Dispatch: a due user's device receives a payload ──────────────────────────

    [Fact]
    public async Task RunTick_DueMealUserWithDevice_SendsMealReminderToDevice()
    {
        var userId = Guid.NewGuid();
        var pushToken = $"due-meal-{userId:N}";

        await SeedAsync(
            userId,
            mealEnabled: true,
            waterEnabled: false,
            // Schedule the meal reminder at "now" (UTC, offset 0) so it is squarely inside the window.
            mealReminderTimes: [NowUtcTimeString()],
            deviceToken: pushToken);

        await RunTickAsync();

        // Exactly the seeded user's device receives a meal-category payload.
        var sent = _factory.Sender.Sent
            .Where(p => p.Token == pushToken && p.Category == "meal_reminder")
            .ToList();
        Assert.Single(sent);
        Assert.Equal(DevicePlatform.iOS, sent[0].Platform);
    }

    // ── Dispatch: a non-due user's device receives nothing ────────────────────────

    [Fact]
    public async Task RunTick_DisabledUserWithDevice_SendsNothing()
    {
        var userId = Guid.NewGuid();
        var pushToken = $"disabled-{userId:N}";

        await SeedAsync(
            userId,
            mealEnabled: false,
            waterEnabled: false,
            mealReminderTimes: [NowUtcTimeString()],
            deviceToken: pushToken);

        await RunTickAsync();

        Assert.DoesNotContain(_factory.Sender.Sent, p => p.Token == pushToken);
    }

    [Fact]
    public async Task RunTick_EnabledUserButNotScheduledNow_SendsNothing()
    {
        var userId = Guid.NewGuid();
        var pushToken = $"off-schedule-{userId:N}";

        // Meal reminders are enabled, but the only scheduled time is far from "now" (well outside the
        // 5-minute window), so the decider declines and nothing is dispatched.
        var farFromNow = TimeOnly.FromTimeSpan(DateTimeOffset.UtcNow.TimeOfDay)
            .AddHours(6)
            .ToString(TimeFormat, CultureInfo.InvariantCulture);

        await SeedAsync(
            userId,
            mealEnabled: true,
            waterEnabled: false,
            mealReminderTimes: [farFromNow],
            deviceToken: pushToken);

        await RunTickAsync();

        Assert.DoesNotContain(_factory.Sender.Sent, p => p.Token == pushToken);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static string NowUtcTimeString() =>
        TimeOnly.FromTimeSpan(DateTimeOffset.UtcNow.TimeOfDay)
            .ToString(TimeFormat, CultureInfo.InvariantCulture);

    // Invokes the real per-tick dispatch logic the hosted loop runs, without sleeping for the
    // (minimum 60-second) tick interval. RunTickAsync is internal and visible to this test assembly.
    private async Task RunTickAsync()
    {
        var service = _factory.Services.GetServices<IHostedService>()
            .OfType<PushReminderBackgroundService>()
            .Single();

        await service.RunTickAsync(CancellationToken.None);
    }

    // Seeds a user, their reminder preferences (UTC offset 0, no quiet hours), and one iOS device
    // directly via the DbContext. No diary/water entries are created, so "already logged today" is
    // false for both kinds.
    private async Task SeedAsync(
        Guid userId,
        bool mealEnabled,
        bool waterEnabled,
        IReadOnlyList<string> mealReminderTimes,
        string deviceToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = User.Create($"clerk-{userId:N}", $"{userId:N}@test.local");
        // Pin the prefs/device to a known UserId by reusing the user's generated id.
        var actualUserId = user.Id;

        var prefs = ReminderPreferences.Create(actualUserId);
        prefs.Update(
            mealRemindersEnabled: mealEnabled,
            waterRemindersEnabled: waterEnabled,
            mealReminderTimes: mealReminderTimes
                .Select(s => TimeOnly.ParseExact(s, TimeFormat, CultureInfo.InvariantCulture))
                .ToList(),
            waterReminderTime: waterEnabled ? TimeOnly.FromTimeSpan(DateTimeOffset.UtcNow.TimeOfDay) : null,
            quietHoursStart: null,
            quietHoursEnd: null,
            utcOffsetMinutes: 0);

        var device = DeviceRegistration.Create(actualUserId, deviceToken, DevicePlatform.iOS, "seeded-device");

        db.Users.Add(user);
        db.ReminderPreferences.Add(prefs);
        db.DeviceRegistrations.Add(device);
        await db.SaveChangesAsync();
    }
}
