using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Test host factory: forces Development env (so IsDevelopment() is deterministic in CI),
/// forces Database:AutoMigrate=false (no DB connection at startup), and captures service
/// registrations for wiring tests.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IReadOnlyList<ServiceDescriptor> CapturedServices { get; private set; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment("Development");
        builder.UseSetting("Database:AutoMigrate", "false");

        builder.ConfigureTestServices(services =>
        {
            CapturedServices = services.ToList();
        });
    }
}
