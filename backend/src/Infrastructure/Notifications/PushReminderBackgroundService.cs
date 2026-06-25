using System.Collections.Concurrent;
using System.Globalization;
using MAIHealthCoach.Application.Notifications;
using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Notifications;

/// <summary>
/// Periodic background sweep that evaluates each user's push-reminder preferences and dispatches due
/// meal/water reminders via the pluggable <see cref="IPushNotificationSender"/> (issue #48). All
/// scheduling decisions are delegated to the pure <see cref="ReminderDecider"/>; this service owns
/// only the I/O: the timer loop, per-tick DB queries, time-zone resolution from the stored UTC
/// offset, and in-process per-user/per-kind de-duplication.
/// </summary>
/// <remarks>
/// The loop is resilient: cancellation breaks cleanly, and any other exception in a tick is logged
/// and the loop continues, so a transient DB error never takes the service down. De-duplication is
/// in-memory only (a process restart may re-send within one window) which is acceptable for
/// best-effort reminders. Each tick opens its own DI scope to resolve the scoped
/// <see cref="AppDbContext"/>, since a hosted service is a singleton.
/// </remarks>
internal sealed class PushReminderBackgroundService : BackgroundService
{
    private const string TimeFormat = "HH:mm";

    // EF Core stores DateTimeOffset offsets within +/-14h; clamp the stored offset to this bound
    // before constructing the TimeSpan so a stray out-of-range row can never throw on ToOffset.
    private const int MaxOffsetMinutes = 14 * 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<PushReminderOptions> _options;
    private readonly ILogger<PushReminderBackgroundService> _logger;

    // Keyed by (user, reminder kind): the UTC instant we last sent that reminder, used to suppress
    // duplicate sends within one check interval.
    private readonly ConcurrentDictionary<(Guid UserId, string Kind), DateTimeOffset> _lastSentAt = new();

    public PushReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<PushReminderOptions> options,
        ILogger<PushReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(opts.CheckIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!opts.Enabled)
            {
                continue;
            }

            try
            {
                await RunTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push reminder tick failed; continuing.");
            }
        }
    }

    // Internal (not private) so the per-tick dispatch logic is invocable directly from the
    // Infrastructure/Api test assemblies (via InternalsVisibleTo) without waiting on the timer loop —
    // the loop's leading Task.Delay uses the (min 60s) configured interval, which no test should sleep
    // for. The hosted loop calls exactly this method, so a direct invocation exercises the real path.
    internal async Task RunTickAsync(CancellationToken ct)
    {
        var opts = _options.Value;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

        var utcNow = DateTimeOffset.UtcNow;

        var candidates = await db.ReminderPreferences
            .AsNoTracking()
            .Where(p => p.MealRemindersEnabled || p.WaterRemindersEnabled)
            .Take(opts.MaxUsersPerTick)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return;
        }

        var userIds = candidates.Select(c => c.UserId).ToList();

        var devices = await db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => userIds.Contains(d.UserId))
            .ToListAsync(ct);

        if (devices.Count == 0)
        {
            return;
        }

        var devicesByUser = devices
            .GroupBy(d => d.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var pref in candidates)
        {
            if (!devicesByUser.TryGetValue(pref.UserId, out var userDevices))
            {
                continue;
            }

            try
            {
                await EvaluateUserAsync(db, sender, pref, userDevices, utcNow, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push reminder evaluation failed for user {UserId}; continuing.", pref.UserId);
            }
        }
    }

    private async Task EvaluateUserAsync(
        AppDbContext db,
        IPushNotificationSender sender,
        ReminderPreferences pref,
        List<DeviceRegistration> userDevices,
        DateTimeOffset utcNow,
        CancellationToken ct)
    {
        var userId = pref.UserId;

        var offsetMinutes = Math.Clamp(pref.UtcOffsetMinutes, -MaxOffsetMinutes, MaxOffsetMinutes);
        var localDt = utcNow.ToOffset(TimeSpan.FromMinutes(offsetMinutes));
        var localToday = DateOnly.FromDateTime(localDt.DateTime);
        var localTimeNow = TimeOnly.FromTimeSpan(localDt.TimeOfDay);

        var hasMeal = await db.DiaryEntries
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.Date == localToday, ct);

        var hasWater = await db.WaterLogEntries
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.Date == localToday, ct);

        var input = new ReminderDeciderInput(
            UserId: userId,
            MealRemindersEnabled: pref.MealRemindersEnabled,
            WaterRemindersEnabled: pref.WaterRemindersEnabled,
            MealReminderTimes: pref.GetMealReminderTimes(),
            WaterReminderTime: TryParseTime(pref.WaterReminderTime),
            QuietHoursStart: TryParseTime(pref.QuietHoursStart),
            QuietHoursEnd: TryParseTime(pref.QuietHoursEnd),
            HasLoggedMealToday: hasMeal,
            HasLoggedWaterToday: hasWater,
            LocalNow: localTimeNow);

        var output = ReminderDecider.Evaluate(input);

        if (output.MealReminderDue && NotRecentlySent(userId, "meal", utcNow))
        {
            foreach (var device in userDevices)
            {
                await sender.SendAsync(
                    new PushNotificationPayload(
                        device.Token,
                        device.Platform,
                        "Time to log your meal!",
                        "Don't forget to log your meals today.",
                        "meal_reminder"),
                    ct);
            }

            MarkSent(userId, "meal", utcNow);
        }

        if (output.WaterReminderDue && NotRecentlySent(userId, "water", utcNow))
        {
            foreach (var device in userDevices)
            {
                await sender.SendAsync(
                    new PushNotificationPayload(
                        device.Token,
                        device.Platform,
                        "Stay hydrated!",
                        "Time to log your water intake.",
                        "water_reminder"),
                    ct);
            }

            MarkSent(userId, "water", utcNow);
        }
    }

    private static TimeOnly? TryParseTime(string? value) =>
        TimeOnly.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private bool NotRecentlySent(Guid userId, string kind, DateTimeOffset utcNow)
    {
        var window = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
        if (_lastSentAt.TryGetValue((userId, kind), out var last) && utcNow - last < window)
        {
            return false;
        }

        return true;
    }

    private void MarkSent(Guid userId, string kind, DateTimeOffset utcNow) =>
        _lastSentAt[(userId, kind)] = utcNow;
}
