using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Placeholder: EF Core DbContext, repositories, and external HTTP clients
        // (Clerk JWKS, Anthropic, Open Food Facts) will be registered here in later milestones.
        return services;
    }
}
