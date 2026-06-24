using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Test host factory that forces <c>Database:AutoMigrate</c> off, guaranteeing that
/// integration tests never open a database connection or run startup migrations,
/// regardless of the host environment they execute in. This keeps the test suite
/// fully self-contained and runnable with no PostgreSQL instance available.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Snapshot of the application's service registrations, captured at host build time.
    /// Lets wiring tests assert on registration metadata (e.g. service lifetime) without
    /// resolving — and therefore constructing — the underlying services.
    /// </summary>
    public IReadOnlyList<ServiceDescriptor> CapturedServices { get; private set; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.ConfigureTestServices(services =>
        {
            CapturedServices = services.ToList();
        });
    }
}
