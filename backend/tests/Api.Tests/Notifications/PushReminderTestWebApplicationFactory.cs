using System.Collections.Concurrent;
using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MAIHealthCoach.Api.Tests.Notifications;

/// <summary>
/// Test host for the push-reminder background service (issue #48). Extends the signed-JWT SQLite
/// harness and swaps the real <see cref="IPushNotificationSender"/> for a
/// <see cref="FakePushNotificationSender"/> that captures every dispatched payload, plus turns the
/// reminder sweep on (<c>PushReminder:Enabled=true</c>) with the minimum allowed 60-second tick
/// interval. Tests invoke the per-tick dispatch logic directly rather than waiting on the timer loop.
/// </summary>
public sealed class PushReminderTestWebApplicationFactory : AuthTestWebApplicationFactory
{
    /// <summary>The fake sender registered into the host, exposing every captured payload.</summary>
    public FakePushNotificationSender Sender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Turn the sweep on and pin the (minimum-allowed) tick interval via configuration, so the
        // options validator passes and Enabled flips true.
        builder.UseSetting("PushReminder:Enabled", "true");
        builder.UseSetting("PushReminder:CheckIntervalSeconds", "60");

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // Replace the no-op logging sender with the capturing fake. Registered as a singleton so
            // the instance the background service resolves is the same one the test asserts against.
            services.RemoveAll<IPushNotificationSender>();
            services.AddSingleton<IPushNotificationSender>(Sender);
        });
    }
}

/// <summary>
/// An <see cref="IPushNotificationSender"/> test double that records every payload it is asked to
/// deliver into a thread-safe bag instead of contacting any push provider. Honors the interface
/// contract: it never throws.
/// </summary>
public sealed class FakePushNotificationSender : IPushNotificationSender
{
    private readonly ConcurrentBag<PushNotificationPayload> _sent = new();

    /// <summary>Every payload captured so far, in no particular order.</summary>
    public IReadOnlyCollection<PushNotificationPayload> Sent => _sent;

    public Task SendAsync(PushNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        _sent.Add(payload);
        return Task.CompletedTask;
    }
}
