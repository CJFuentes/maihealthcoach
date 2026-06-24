using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Application.MealSuggestions;
using MAIHealthCoach.Application.Nudges;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Application;

/// <summary>
/// Registers Application layer services with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // GoalsCalculator is stateless and dependency-free — a singleton avoids
        // repeated allocations per request (issue #17).
        services.AddSingleton<GoalsCalculator>();

        // Meal suggestions (issue #37) orchestrate per-request coaching calls — scoped.
        services.AddScoped<IMealSuggestionService, MealSuggestionService>();

        // Motivational nudges (issue #38) orchestrate per-request coaching calls — scoped.
        services.AddScoped<INudgeService, NudgeService>();

        return services;
    }
}
