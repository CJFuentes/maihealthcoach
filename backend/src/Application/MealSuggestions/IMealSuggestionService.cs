namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// Produces meal suggestions that fit a user's remaining nutrition budget and dietary preferences
/// (issue #37). Orchestrates a coaching call via <c>ICoachService</c> and parses the reply into
/// structured options. Lives entirely in the Application layer — it performs no HTTP work itself
/// and never throws on expected coaching failures (inspect <see cref="MealSuggestionResult.IsSuccess"/>).
/// </summary>
public interface IMealSuggestionService
{
    /// <summary>
    /// Builds a coaching prompt from the supplied remaining-budget request, asks MAI for meal
    /// ideas, and parses the reply into a <see cref="MealSuggestionResult"/>.
    /// </summary>
    /// <param name="request">The resolved remaining budget and dietary constraints.</param>
    /// <param name="cancellationToken">Propagates caller cancellation.</param>
    /// <returns>A success result with parsed options, or a graceful failure with a fallback message.</returns>
    Task<MealSuggestionResult> SuggestAsync(MealSuggestionRequest request, CancellationToken cancellationToken = default);
}
