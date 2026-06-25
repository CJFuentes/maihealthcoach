using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Coaching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MAIHealthCoach.Api.Tests.Coach;

/// <summary>
/// A dedicated coach test host for the chat rate-limiting test (issue #39). Mirrors
/// <see cref="CoachTestWebApplicationFactory"/> (which is <c>sealed</c>, so it cannot be subclassed)
/// by extending <see cref="AuthTestWebApplicationFactory"/> and swapping in the
/// <see cref="StubCoachService"/>, then tightens the per-user send budget to a <c>PermitLimit</c> of
/// 2 so the 429 path can be exercised in just three requests.
/// </summary>
/// <remarks>
/// The chat rate limiter resolves <c>IOptions&lt;CoachChatOptions&gt;</c> <em>per request</em>
/// (see <c>ChatRateLimiting</c>), so the <c>UseSetting</c> override below takes effect even though
/// the limiter is registered at startup. A long window keeps the fixed-window bucket from rolling
/// mid-test. This factory owns its own host — and therefore its own rate-limiter partition store —
/// so its limiter state never leaks into the shared <see cref="CoachTestWebApplicationFactory"/>
/// used by the other chat tests.
/// </remarks>
public sealed class ChatRateLimitTestWebApplicationFactory : AuthTestWebApplicationFactory
{
    /// <summary>The stubbed coach service; the test mutates its <see cref="StubCoachService.Handler"/>.</summary>
    public StubCoachService StubService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Two sends allowed per window; the third is rejected with 429. A generous window
        // (one hour) ensures the bucket does not refill while the three requests are in flight.
        builder.UseSetting("CoachChat:PermitLimit", "2");
        builder.UseSetting("CoachChat:WindowSeconds", "3600");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICoachService>();
            services.AddSingleton<ICoachService>(StubService);
        });
    }
}
