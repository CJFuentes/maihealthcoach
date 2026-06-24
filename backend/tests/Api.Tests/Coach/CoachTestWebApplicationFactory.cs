using MAIHealthCoach.Api.Tests.Auth;
using MAIHealthCoach.Application.Coaching;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MAIHealthCoach.Api.Tests.Coach;

/// <summary>
/// A test double for <see cref="ICoachService"/> whose behaviour is driven by a mutable
/// <see cref="Handler"/>. Tests set <see cref="Handler"/> at the start of each case to return a
/// successful coaching reply (default) or a failure, and to capture the inbound
/// <see cref="CoachRequest"/> for assertions about the composed prompt and context.
/// </summary>
public sealed class StubCoachService : ICoachService
{
    /// <summary>
    /// A three-item JSON array that <c>MealSuggestionParser</c> parses into structured options.
    /// Used as the default successful reply.
    /// </summary>
    public const string DefaultSuggestionJson =
        """
        [
          {"name": "Grilled chicken salad", "calories": 420, "proteinGrams": 38, "carbGrams": 18, "fatGrams": 22, "rationale": "Lean protein and greens fit the remaining budget."},
          {"name": "Greek yogurt with berries", "calories": 220, "proteinGrams": 18, "carbGrams": 24, "fatGrams": 5, "rationale": "High protein, low fat snack that stays within budget."},
          {"name": "Lentil and vegetable soup", "calories": 310, "proteinGrams": 16, "carbGrams": 45, "fatGrams": 6, "rationale": "Plant-based and filling while leaving room for the rest of the day."}
        ]
        """;

    /// <summary>The function invoked on each call. Defaults to a successful reply with the disclaimer.</summary>
    public Func<CoachRequest, CoachResult> Handler { get; set; } =
        _ => CoachResult.Success(DefaultSuggestionJson, "claude-sonnet-4-6", CoachPromptBuilder.SafetyDisclaimer);

    public Task<CoachResult> AskAsync(CoachRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Handler(request));
}

/// <summary>
/// Test host for the MAI coach endpoints. Extends <see cref="AuthTestWebApplicationFactory"/> (so it
/// inherits the signed-JWT validation and SQLite in-memory database) and swaps the real
/// <see cref="ICoachService"/> for <see cref="StubService"/> so tests never call Anthropic.
/// </summary>
public sealed class CoachTestWebApplicationFactory : AuthTestWebApplicationFactory
{
    /// <summary>The stubbed coach service; tests mutate its <see cref="StubCoachService.Handler"/>.</summary>
    public StubCoachService StubService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICoachService>();
            services.AddSingleton<ICoachService>(StubService);
        });
    }
}
