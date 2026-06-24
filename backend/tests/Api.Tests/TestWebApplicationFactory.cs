using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Test host factory that forces the environment to <c>Development</c> and
/// <c>Database:AutoMigrate</c> to <c>false</c>.
///
/// Forcing Development is required because <c>MapOpenApi()</c> in Program.cs is
/// gated on <c>IsDevelopment()</c>. Without this override, WebApplicationFactory
/// defaults to Production and <c>/openapi/v1.json</c> would return 404.
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
        // UseEnvironment must come before base.ConfigureWebHost so the environment is
        // committed before the app reads it (e.g., to gate MapOpenApi).
        builder.UseEnvironment("Development");

        base.ConfigureWebHost(builder);

        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureTestServices(services =>
        {
            CapturedServices = services.ToList();
        });
    }
}
