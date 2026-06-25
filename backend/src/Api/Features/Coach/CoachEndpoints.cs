using MAIHealthCoach.Api.RateLimiting;
using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Application.Goals;
using MAIHealthCoach.Application.MealSuggestions;
using MAIHealthCoach.Application.Nudges;
using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Domain.UserProfiles;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Api.Features.Coach;

/// <summary>
/// Registers the MAI coach endpoints on the supplied versioned route builder.
/// <list type="bullet">
///   <item><description><c>GET /me/coach/meal-suggestions</c> — suggest meals that fit the user's remaining nutrition budget and dietary preferences (issue #37, per-user coach rate limited per #45).</description></item>
///   <item><description><c>GET /me/coach/nudge</c> — return a short, personalised motivational nudge based on optional streak/adherence signals (issue #38, per-user coach rate limited per #45).</description></item>
///   <item><description><c>POST /me/coach/chat</c> — send a chat message and get a contextual reply, persisting the turn (issue #39, per-user coach rate limited — consolidated under #45).</description></item>
///   <item><description><c>GET /me/coach/chat</c> — list the user's conversations, newest first (issue #39).</description></item>
///   <item><description><c>GET /me/coach/chat/{conversationId}</c> — retrieve a conversation and its ordered messages (issue #39).</description></item>
/// </list>
/// Goals are recomputed from the profile per request (computation-first); any stored overrides are
/// layered on top before the remaining budget is derived.
/// </summary>
internal static class CoachEndpoints
{
    internal static RouteGroupBuilder MapCoachEndpoints(this RouteGroupBuilder group)
    {
        var coach = group.MapGroup("/me/coach").RequireAuthorization();

        // The three coach LLM endpoints (meal-suggestions / nudge / chat) hit Anthropic and are the
        // prime abuse target, so each carries the stricter per-user coach policy (issue #45). This
        // consolidates issue #39's chat-only limiter into the shared mechanism — chat still enforces
        // a per-user budget that yields 429 when exceeded. The read-only chat listing/detail GETs
        // below are cheap and rely on the global limiter only.
        coach.MapGet("/meal-suggestions", GetMealSuggestionsAsync)
            .WithName("GetMealSuggestions")
            .RequireRateLimiting(ApiRateLimiting.CoachPolicyName);

        coach.MapGet("/nudge", GetNudgeAsync)
            .WithName("GetNudge")
            .RequireRateLimiting(ApiRateLimiting.CoachPolicyName);

        coach.MapPost("/chat", SendChatMessageAsync)
            .WithName("SendCoachChatMessage")
            .RequireRateLimiting(ApiRateLimiting.CoachPolicyName);

        coach.MapGet("/chat", ListConversationsAsync)
            .WithName("ListCoachConversations");

        coach.MapGet("/chat/{conversationId:guid}", GetConversationAsync)
            .WithName("GetCoachConversation");

        return group;
    }

    // ── GET /api/v1/me/coach/meal-suggestions ─────────────────────────────────────

    private static async Task<IResult> GetMealSuggestionsAsync(
        string? mealType,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        IMealSuggestionService mealSuggestionService,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var profile = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        if (BuildCalculatorInput(profile) is not { } input)
        {
            return ProfileProblem(profile);
        }

        var computed = calculator.Compute(input);

        var overrides = await db.UserGoalTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

        var suggestionRequest = RemainingBudgetCalculator.Compute(
            computed,
            overrides,
            profile!.DietaryPreferences,
            mealType);

        var result = await mealSuggestionService.SuggestAsync(suggestionRequest, ct);

        if (!result.IsSuccess)
        {
            return MapCoachFailure(result);
        }

        return Results.Ok(MapToResponse(result));
    }

    // ── GET /api/v1/me/coach/nudge ────────────────────────────────────────────────

    /// <summary>
    /// Returns a short, personalised motivational nudge (issue #38). The <paramref name="streakDays"/>
    /// and <paramref name="adherencePercent"/> query parameters are the optional integration seam for
    /// the streaks/adherence tracking work (issues #44/#42); when omitted, the nudge service produces
    /// general encouragement.
    /// </summary>
    /// <remarks>
    /// Unlike the meal-suggestions endpoint, a missing or incomplete profile is <em>not</em> an error
    /// here: rather than returning 404/409, this endpoint requests a generic encouraging nudge so the
    /// user always receives a friendly message (the chosen friendlier behaviour). It still requires
    /// authentication — callers without a token receive 401.
    /// </remarks>
    private static async Task<IResult> GetNudgeAsync(
        decimal? adherencePercent,
        int? streakDays,
        ICurrentUserService currentUser,
        AppDbContext db,
        GoalsCalculator calculator,
        INudgeService nudgeService,
        CancellationToken ct)
    {
        if (adherencePercent is < 0 or > 100)
        {
            return Results.Problem(
                title: "Invalid parameter.",
                detail: "adherencePercent must be between 0 and 100.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (streakDays is < 0)
        {
            return Results.Problem(
                title: "Invalid parameter.",
                detail: "streakDays must be a non-negative integer.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var profile = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        NudgeRequest nudgeRequest;

        if (BuildCalculatorInput(profile) is { } input)
        {
            // Complete profile — compute goals and build a context-rich nudge request.
            var computed = calculator.Compute(input);

            var overrides = await db.UserGoalTargets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

            nudgeRequest = new NudgeRequest(
                CurrentStreakDays: streakDays,
                TodayAdherencePercent: adherencePercent,
                HasProfile: true,
                PrimaryGoal: input.PrimaryGoal.ToString(),
                DailyCalorieTarget: overrides?.CaloriesKcal ?? computed.CaloriesKcal,
                DailyProteinTargetGrams: overrides?.ProteinGrams ?? computed.ProteinGrams,
                ActivityLevel: input.ActivityLevel.ToString(),
                DietaryPreferences: FormatDietaryText(profile!.DietaryPreferences));
        }
        else
        {
            // Missing/incomplete profile — request a generic encouraging nudge (friendlier behaviour),
            // still passing any optional streak/adherence signals.
            nudgeRequest = new NudgeRequest(
                CurrentStreakDays: streakDays,
                TodayAdherencePercent: adherencePercent,
                HasProfile: false);
        }

        var result = await nudgeService.GetNudgeAsync(nudgeRequest, ct);

        if (!result.IsSuccess)
        {
            return MapNudgeFailure(result);
        }

        return Results.Ok(new NudgeResponse(
            result.Nudge!.Message,
            result.Nudge.Tone,
            result.Disclaimer));
    }

    // ── POST /api/v1/me/coach/chat ────────────────────────────────────────────────

    /// <summary>
    /// Sends a chat message to MAI and returns a contextual reply (issue #39). Loads recent
    /// conversation history and (best-effort) the user's nutrition context, calls the coach, and
    /// — only on success — persists the user message and the assistant reply atomically. The
    /// endpoint is per-user rate limited; over-budget requests are rejected with a 429 before the
    /// handler runs.
    /// </summary>
    /// <remarks>
    /// Building the nutrition context is non-fatal (mirroring the nudge endpoint): a missing or
    /// incomplete profile yields a null context rather than a 404/409 — chat must always work. On a
    /// coach failure nothing is persisted (no <c>SaveChanges</c> is called), so a transient upstream
    /// error never leaves a dangling user message or empty conversation.
    /// </remarks>
    private static async Task<IResult> SendChatMessageAsync(
        SendChatMessageRequest request,
        ICurrentUserService currentUser,
        AppDbContext db,
        ICoachService coachService,
        GoalsCalculator calculator,
        IOptions<CoachChatOptions> chatOptions,
        CancellationToken ct)
    {
        var options = chatOptions.Value;

        // 1. Validate the inbound message.
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = ["Message must not be empty."],
            });
        }

        if (request.Message.Length > options.MaxMessageLength)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["message"] = [$"Message exceeds the maximum length of {options.MaxMessageLength} characters."],
            });
        }

        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // 2. Resolve the conversation: load an existing one (scoped to the user) or create a new one.
        Conversation conversation;
        if (request.ConversationId is { } conversationId)
        {
            var existing = await db.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == user.Id, ct);

            // Missing or owned by another user — indistinguishable to the caller by design (404).
            if (existing is null)
            {
                return Results.NotFound();
            }

            conversation = existing;
        }
        else
        {
            conversation = Conversation.Create(user.Id);
            db.Conversations.Add(conversation);
        }

        // 3. Load prior turns for context (empty for a brand-new conversation). Keep the most-recent
        //    N messages in chronological order, trimming a leading assistant turn so the window
        //    handed to the model always begins on a user turn and alternates.
        var history = await BuildHistoryAsync(db, conversation.Id, user.Id, options.HistoryTurnLimit, ct);

        // 4. Build optional nutrition context (non-fatal — null when the profile is incomplete).
        var context = await BuildChatContextAsync(db, calculator, user.Id, ct);

        // 5. Call the coach.
        var coachRequest = new CoachRequest(request.Message, context, CoachModelTier.Default, history);
        var result = await coachService.AskAsync(coachRequest, ct);

        // 6. On failure, persist nothing (the new conversation, if any, is never saved).
        if (!result.IsSuccess)
        {
            return MapCoachResultFailure(result);
        }

        // 7. On success, append both turns and persist atomically.
        var userMessage = conversation.AddMessage(CoachMessageRole.User, request.Message);
        var assistantMessage = conversation.AddMessage(CoachMessageRole.Assistant, result.ReplyText!);
        db.CoachMessages.Add(userMessage);
        db.CoachMessages.Add(assistantMessage);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new SendChatMessageResponse(
            conversation.Id,
            assistantMessage.Id,
            result.ReplyText!,
            result.Disclaimer,
            result.ModelUsed,
            assistantMessage.CreatedAt));
    }

    // ── GET /api/v1/me/coach/chat ─────────────────────────────────────────────────

    /// <summary>
    /// Lists the authenticated user's conversations, newest activity first (issue #39).
    /// </summary>
    private static async Task<IResult> ListConversationsAsync(
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var conversations = await db.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationSummaryResponse(
                c.Id,
                c.Title,
                c.MessageCount,
                c.CreatedAt,
                c.UpdatedAt))
            .ToListAsync(ct);

        return Results.Ok(new ConversationListResponse(conversations));
    }

    // ── GET /api/v1/me/coach/chat/{conversationId} ────────────────────────────────

    /// <summary>
    /// Retrieves a single conversation and its messages in chronological order (issue #39).
    /// Conversations owned by another user are indistinguishable from missing ones (404).
    /// </summary>
    private static async Task<IResult> GetConversationAsync(
        Guid conversationId,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        var conversation = await db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == user.Id, ct);

        if (conversation is null)
        {
            return Results.NotFound();
        }

        var messages = await db.CoachMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.UserId == user.Id)
            .OrderBy(m => m.Sequence)
            .Select(m => new ChatMessageResponse(
                m.Id,
                m.Role == CoachMessageRole.User ? "user" : "assistant",
                m.Content,
                m.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new ConversationDetailResponse(conversation.Id, conversation.Title, messages));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the most-recent <paramref name="limit"/> messages for a conversation in chronological
    /// order and maps them to <see cref="CoachConversationTurn"/>s. Trims a leading assistant turn
    /// so the returned window always begins on a user turn (the model requires the first turn to be
    /// from the user). Returns an empty list for a brand-new conversation.
    /// </summary>
    private static async Task<IReadOnlyList<CoachConversationTurn>> BuildHistoryAsync(
        AppDbContext db,
        Guid conversationId,
        Guid userId,
        int limit,
        CancellationToken ct)
    {
        if (limit <= 0)
        {
            return [];
        }

        var prior = await db.CoachMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.UserId == userId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(ct);

        // Keep the last `limit` messages while preserving chronological order.
        var window = prior.Count > limit
            ? prior.Skip(prior.Count - limit).ToList()
            : prior;

        // The model requires the first turn to be a user turn; drop a leading assistant turn that
        // a trimmed window may begin with.
        if (window.Count > 0 && window[0].Role == CoachMessageRole.Assistant)
        {
            window = window.Skip(1).ToList();
        }

        return window
            .Select(m => new CoachConversationTurn(m.Role, m.Content))
            .ToList();
    }

    /// <summary>
    /// Builds a best-effort <see cref="CoachingContext"/> from the user's profile, computed goals,
    /// and any stored overrides (mirroring the nudge endpoint's context fields). Returns
    /// <see langword="null"/> when the profile is missing or incomplete — chat must always work, so
    /// this never produces a 404/409.
    /// </summary>
    private static async Task<CoachingContext?> BuildChatContextAsync(
        AppDbContext db,
        GoalsCalculator calculator,
        Guid userId,
        CancellationToken ct)
    {
        var profile = await db.UserProfiles
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (BuildCalculatorInput(profile) is not { } input)
        {
            return null;
        }

        var computed = calculator.Compute(input);

        var overrides = await db.UserGoalTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        return new CoachingContext(
            PrimaryGoal: input.PrimaryGoal.ToString(),
            DailyCalorieTarget: overrides?.CaloriesKcal ?? computed.CaloriesKcal,
            DailyProteinTargetGrams: overrides?.ProteinGrams ?? computed.ProteinGrams,
            DietaryPreferences: FormatDietaryText(profile!.DietaryPreferences),
            ActivityLevel: input.ActivityLevel.ToString());
    }

    /// <summary>
    /// Builds a <see cref="GoalsCalculatorInput"/> from a profile, or returns
    /// <see langword="null"/> when the profile is missing or lacks any required biometric.
    /// Callers translate a <see langword="null"/> result via <see cref="ProfileProblem"/>.
    /// </summary>
    private static GoalsCalculatorInput? BuildCalculatorInput(UserProfile? profile)
    {
        if (profile is null
            || profile.LatestWeightKg is not { } weightKg
            || profile.HeightCm is not { } heightCm
            || profile.DateOfBirth is not { } dob
            || profile.BiologicalSex is not { } sex
            || profile.ActivityLevel is not { } activity
            || profile.PrimaryGoal is not { } goal)
        {
            return null;
        }

        return new GoalsCalculatorInput(
            WeightKg: weightKg,
            HeightCm: heightCm,
            AgeYears: AgeFrom(dob),
            BiologicalSex: sex,
            ActivityLevel: activity,
            PrimaryGoal: goal);
    }

    /// <summary>
    /// Produces the 404/409 ProblemDetails for a missing profile or one with insufficient
    /// biometrics. Mirrors the field-completeness check in <see cref="BuildCalculatorInput"/>.
    /// </summary>
    private static IResult ProfileProblem(UserProfile? profile)
    {
        if (profile is null)
        {
            return Results.Problem(
                title: "Profile not found.",
                detail: "No profile exists for this user. Create a profile via PUT /api/v1/me/profile before requesting meal suggestions.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var missing = new List<string>();
        if (!profile.LatestWeightKg.HasValue) missing.Add("weight");
        if (!profile.HeightCm.HasValue) missing.Add("heightCm");
        if (!profile.DateOfBirth.HasValue) missing.Add("dateOfBirth");
        if (!profile.BiologicalSex.HasValue) missing.Add("biologicalSex");
        if (!profile.ActivityLevel.HasValue) missing.Add("activityLevel");
        if (!profile.PrimaryGoal.HasValue) missing.Add("primaryGoal");

        return Results.Problem(
            title: "Incomplete profile.",
            detail: $"The following profile fields are required for goals computation but are not set: {string.Join(", ", missing)}. " +
                    "Update your profile via PUT /api/v1/me/profile.",
            statusCode: StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Computes whole-year age from <paramref name="dob"/> relative to the server's UTC date.
    /// Mirrors the algorithm in <c>ProfileValidator</c> exactly.
    /// </summary>
    private static int AgeFrom(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static MealSuggestionsResponse MapToResponse(MealSuggestionResult result)
    {
        return new MealSuggestionsResponse(
            Options: result.Options
                .Select(o => new MealSuggestionOptionResponse(
                    o.Name,
                    o.Calories,
                    o.ProteinGrams,
                    o.CarbGrams,
                    o.FatGrams,
                    o.Rationale))
                .ToList(),
            RemainingCalories: result.RemainingCalories,
            RemainingProteinGrams: result.RemainingProteinGrams,
            RemainingCarbGrams: result.RemainingCarbGrams,
            RemainingFatGrams: result.RemainingFatGrams,
            Disclaimer: result.Disclaimer);
    }

    private static IResult MapCoachFailure(MealSuggestionResult result) =>
        result.ErrorCategory == CoachErrorCategory.ConfigurationError
            ? Results.Problem(
                title: "Coaching service not configured.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Problem(
                title: "Coaching service unavailable.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status502BadGateway);

    private static IResult MapNudgeFailure(NudgeResult result) =>
        result.ErrorCategory == CoachErrorCategory.ConfigurationError
            ? Results.Problem(
                title: "Coaching service not configured.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Problem(
                title: "Coaching service unavailable.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status502BadGateway);

    private static IResult MapCoachResultFailure(CoachResult result) =>
        result.ErrorCategory == CoachErrorCategory.ConfigurationError
            ? Results.Problem(
                title: "Coaching service not configured.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Problem(
                title: "Coaching service unavailable.",
                detail: result.FallbackMessage,
                statusCode: StatusCodes.Status502BadGateway);

    /// <summary>
    /// Collapses the structured dietary preferences into a single free-text constraint string, or
    /// <see langword="null"/> when nothing meaningful is recorded (DietType.None and no allergies).
    /// Mirrors the formatting in <see cref="RemainingBudgetCalculator"/>.
    /// </summary>
    private static string? FormatDietaryText(DietaryPreferences? preferences)
    {
        if (preferences is null)
        {
            return null;
        }

        var parts = new List<string>();

        if (preferences.DietType.HasValue && preferences.DietType.Value != DietType.None)
        {
            parts.Add(preferences.DietType.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(preferences.Allergies))
        {
            parts.Add($"allergies: {preferences.Allergies}");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }
}
