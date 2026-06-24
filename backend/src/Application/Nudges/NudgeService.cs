using System.Globalization;
using System.Text;
using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.Nudges;

/// <summary>
/// Default <see cref="INudgeService"/> implementation. Composes a coaching prompt from the optional
/// streak/adherence signals and optional profile context, delegates to <see cref="ICoachService"/>
/// (so #41 guardrails and the safety disclaimer apply), and parses the reply via
/// <see cref="NudgeParser"/>. Internal because its DI registration lives in the same assembly; the
/// interface and supporting types are public for cross-assembly testing.
/// </summary>
internal sealed class NudgeService : INudgeService
{
    private readonly ICoachService _coachService;

    public NudgeService(ICoachService coachService)
    {
        _coachService = coachService;
    }

    /// <inheritdoc />
    public async Task<NudgeResult> GetNudgeAsync(NudgeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userMessage = BuildUserMessage(request);

        var context = request.HasProfile
            ? new CoachingContext(
                PrimaryGoal: request.PrimaryGoal,
                DailyCalorieTarget: request.DailyCalorieTarget,
                DailyProteinTargetGrams: request.DailyProteinTargetGrams,
                DietaryPreferences: request.DietaryPreferences,
                ActivityLevel: request.ActivityLevel)
            : CoachingContext.Empty;

        var coachRequest = new CoachRequest(userMessage, context, CoachModelTier.Default);

        var coachResult = await _coachService.AskAsync(coachRequest, cancellationToken);

        if (!coachResult.IsSuccess)
        {
            return NudgeResult.Failure(
                coachResult.ErrorCategory,
                coachResult.FallbackMessage ?? "Motivational nudges are unavailable right now.");
        }

        var nudge = NudgeParser.Parse(coachResult.ReplyText!);

        return NudgeResult.Success(nudge, coachResult.Disclaimer);
    }

    // Builds the user-turn message. Streak and adherence are surfaced only when present; when both
    // are absent the model is asked for general encouragement aligned to the user's goal. The model
    // is asked to respond with a strict JSON object so the reply can be parsed deterministically by
    // NudgeParser.
    private static string BuildUserMessage(NudgeRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("I'd like a short motivational nudge to keep me going with my health journey.");
        sb.AppendLine();

        var hasStreak = request.CurrentStreakDays is not null;
        var hasAdherence = request.TodayAdherencePercent is not null;

        if (hasStreak || hasAdherence)
        {
            sb.AppendLine("My recent activity:");

            if (request.CurrentStreakDays is { } streak)
            {
                sb.AppendLine($"- Current streak: {streak} days");
            }

            if (request.TodayAdherencePercent is { } adherence)
            {
                sb.AppendLine($"- Today's adherence: {adherence.ToString("0.#", CultureInfo.InvariantCulture)}%");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("I don't have recent streak or adherence stats to share right now, so please give me some general encouragement aligned with my goal.");
            sb.AppendLine();
        }

        sb.AppendLine("Please write a nudge that:");
        sb.AppendLine("- Is 1-2 short sentences only.");
        sb.AppendLine("- Is warm, positive, and non-judgmental.");
        sb.AppendLine("- Celebrates my progress when I'm doing well, or gently re-motivates me after a lapse.");
        sb.AppendLine("- Stays within general wellness encouragement and contains no medical advice.");
        sb.AppendLine();

        sb.AppendLine("Respond ONLY with a JSON object in this exact format (no markdown code fences, no extra text before or after the object):");
        sb.AppendLine("{\"message\": \"...\", \"tone\": \"...\"}");

        return sb.ToString();
    }
}
