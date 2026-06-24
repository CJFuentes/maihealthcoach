using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tag applied to health check registrations that gate readiness.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. Set ConnectionStrings__Postgres via environment variable, user-secrets, or appsettings.Development.json.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        // Placeholder: repositories and external HTTP clients (Clerk JWKS, Anthropic,
        // Open Food Facts) will be registered here in later milestones.
        return services;
    }

    /// <summary>
    /// Registers the DbContext health check tagged "ready" with a 3-second per-check
    /// timeout so the readiness probe never hangs on an unreachable database.
    /// Must be called after <see cref="AddInfrastructure"/> so <see cref="AppDbContext"/>
    /// is already registered.
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "postgres",
                tags: [ReadyTag]);

        // AddDbContextCheck exposes no timeout parameter, so the 3-second per-check
        // timeout is applied by locating the registration after the fact. This caps
        // how long the readiness probe can block on an unreachable database.
        services.Configure<HealthCheckServiceOptions>(opts =>
        {
            var registration = opts.Registrations.FirstOrDefault(r => r.Name == "postgres");
            if (registration is not null)
            {
                registration.Timeout = TimeSpan.FromSeconds(3);
            }
        });

        return services;
    }
}
