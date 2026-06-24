using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Application;

/// <summary>
/// Registers Application layer services with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Placeholder: CQRS handlers, validators, and mapping profiles
        // will be registered here in later milestones.
        return services;
    }
}
