using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Api.Tests.Coach;
using MAIHealthCoach.Application.Coaching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MAIHealthCoach.Api.Tests.RateLimiting;

/// <summary>
/// A dedicated test host for the consolidated coach LLM rate-limiting policy (issue #45). Extends
/// <see cref="AuthTestWebApplicationFactory"/> (signed-JWT validation + in-memory SQLite), swaps in a
/// <see cref="StubCoachService"/> so tests never call Anthropic, and tightens the per-user coach
/// budget — driven by <c>CoachChat:PermitLimit</c>, consolidated with issue #39 — to a tiny
/// <c>PermitLimit</c> of 3 over a long window so the 429 path can be exercised deterministically
/// across the coach LLM endpoints (chat / meal-suggestions / nudge) without wall-clock sleeps. It
/// owns its own host (and rate-limiter partition store), so its limiter state never leaks elsewhere.
/// </summary>
public sealed class CoachPolicyRateLimitTestWebApplicationFactory : AuthTestWebApplicationFactory
{
    /// <summary>The per-user coach permit limit this host enforces; the fourth request is rejected.</summary>
    public const int CoachPermitLimit = 3;

    /// <summary>The stubbed coach service; tests mutate its <see cref="StubCoachService.Handler"/>.</summary>
    public StubCoachService StubService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Tighten the coach per-user budget; a generous global budget and window keep the global
        // limiter from interfering and stop the fixed-window bucket from refilling mid-test.
        builder.UseSetting("CoachChat:PermitLimit", CoachPermitLimit.ToString());
        builder.UseSetting("CoachChat:WindowSeconds", "3600");
        builder.UseSetting("RateLimiting:GlobalPermitLimit", "1000");
        builder.UseSetting("RateLimiting:GlobalWindowSeconds", "3600");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICoachService>();
            services.AddSingleton<ICoachService>(StubService);
        });
    }
}
