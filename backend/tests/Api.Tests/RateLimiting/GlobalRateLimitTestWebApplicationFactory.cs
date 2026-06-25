using MAIHealthCoach.Api.Tests.Auth;
using Microsoft.AspNetCore.Hosting;

namespace MAIHealthCoach.Api.Tests.RateLimiting;

/// <summary>
/// A dedicated test host for the global API rate-limiting tests (issue #45). Extends
/// <see cref="AuthTestWebApplicationFactory"/> (for signed-JWT validation and the in-memory SQLite
/// database) and tightens the <em>global</em> per-partition budget to a tiny <c>GlobalPermitLimit</c>
/// of 3 over a long window so the 429 path can be exercised deterministically in a handful of
/// requests — no wall-clock sleeps. This factory owns its own host (and therefore its own
/// rate-limiter partition store), so its limiter state never leaks into the other tests.
/// </summary>
public sealed class GlobalRateLimitTestWebApplicationFactory : AuthTestWebApplicationFactory
{
    /// <summary>The global permit limit this host enforces; the third request to a partition still
    /// succeeds, the fourth is rejected.</summary>
    public const int GlobalPermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Small fixed permit count + generous window keeps the test deterministic (no sleeps) and
        // stops the fixed-window bucket from refilling mid-test.
        builder.UseSetting("RateLimiting:Enabled", "true");
        builder.UseSetting("RateLimiting:GlobalPermitLimit", GlobalPermitLimit.ToString());
        builder.UseSetting("RateLimiting:GlobalWindowSeconds", "3600");
    }
}
