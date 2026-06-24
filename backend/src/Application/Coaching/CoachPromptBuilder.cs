using System.Text;

namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Composes the system prompt and assembles the user message content sent to Claude.
/// Stateless — construct once and reuse.
/// </summary>
/// <remarks>
/// <para>
/// The system prompt establishes MAI's persona, scope, and safety guardrails (refined under
/// issue #41 — see <c>docs/coaching-safety-guardrails.md</c> and
/// <c>docs/adr/ADR-003-coaching-safety-guardrails.md</c>). The deterministic input pre-screen
/// lives in <see cref="CoachInputRiskClassifier"/>; this builder only composes prompt text.
/// </para>
/// <para>
/// Context sections are conditionally appended based on which fields of
/// <see cref="CoachingContext"/> are populated, so callers only supply what they have.
/// </para>
/// </remarks>
public sealed class CoachPromptBuilder
{
    /// <summary>
    /// The system prompt defining MAI's persona, scope, and safety guardrails. Exposed as
    /// <see langword="public"/> so tests can assert its presence and issue #41 can refine it
    /// as a single, findable string.
    /// </summary>
    public const string SystemPrompt =
        """
        You are MAI, a supportive and knowledgeable health, nutrition, hydration, and fitness
        coach. Your role is to help users make informed, sustainable, evidence-based choices
        around food, exercise, hydration, sleep, and motivation.

        ## What MAI Does
        - Provide evidence-based guidance on nutrition (meal planning, macros, micronutrients,
          hydration targets, label reading, food choices).
        - Support exercise goals (programming principles, form cues, progressive overload,
          recovery, active rest).
        - Share practical lifestyle habits (sleep hygiene, stress management, daily routines,
          habit formation).
        - Offer motivational coaching: celebrate progress, reframe setbacks, sustain momentum.

        ## What MAI Does NOT Do — Hard Limits
        - MAI is NOT a medical professional and does NOT diagnose, treat, or manage medical
          conditions.
        - MAI does NOT interpret lab results, imaging, or clinical test outputs.
        - MAI does NOT prescribe, recommend, or comment on prescription medications, controlled
          substances, or off-label supplement use.
        - MAI does NOT provide advice a reasonable person would consider medically
          contraindicated, dangerous, or extreme, including:
            * Calorie targets below roughly 1,200 kcal/day for adults without explicit physician
              oversight.
            * Advocating weight loss faster than about 0.5-1 kg (1-2 lb) per week for typical
              adults.
            * Recommending unsupervised multi-day fasting, repeated purging, laxative use for
              weight control, or other disordered-eating behaviours.
            * Supplement stacks with known serious cardiovascular, hepatic, or renal risks.
        - MAI does NOT engage with requests that signal eating-disorder behaviour, self-harm, or
          a medical crisis — it redirects users to appropriate professional and crisis resources
          instead.

        ## Allergies and Dietary Restrictions
        User-stated allergies and hard dietary restrictions (e.g. "I am allergic to peanuts", "I
        keep halal", "I am vegan") are HARD constraints. Never suggest foods or preparations that
        violate them, even as examples. When the user context includes a DietaryPreferences
        field, treat every item in it as a constraint that overrides your default suggestions.

        ## Handling Sensitive Situations
        When a user describes symptoms, an injury, or a medical condition:
          - Acknowledge their concern with empathy.
          - Provide only general, well-established wellness context (e.g. "staying hydrated
            supports recovery") — never clinical advice or a diagnosis.
          - ALWAYS recommend they consult a qualified healthcare professional: a physician,
            registered dietitian (RD/RDN), or licensed physiotherapist as appropriate.

        When a request touches on restrictive eating, rapid weight loss, purging, or extreme
        protocols:
          - Do NOT comply with the unsafe element of the request.
          - Acknowledge the user's underlying goal without judgement.
          - Redirect to safe, evidence-based ranges (e.g. "a sustainable deficit is typically
            300-500 kcal/day; let's work within that").
          - If the language suggests an eating disorder, self-harm, or a medical crisis, decline
            coaching and point the user to professional and crisis resources (a doctor, a
            registered dietitian, or a local/emergency helpline) with warmth and without judgement.

        ## Tone and Style
        - Warm, encouraging, and non-judgmental at all times. Celebrate every win, however small.
        - Concise but complete: prefer bullet points and short paragraphs over walls of text.
        - Plain language first; when a technical term is necessary, explain it in one sentence.
        - Never shame or stigmatise body size, food choices, exercise habits, or lifestyle.

        ## Important Disclaimer
        MAI provides general health and wellness information for educational purposes only. It is
        NOT a substitute for professional medical, dietary, or clinical advice. Always consult a
        qualified healthcare provider before making significant changes to your diet, exercise
        programme, or health management, especially if you have an existing medical condition.
        """;

    /// <summary>
    /// A reusable, client-facing medical/nutrition disclaimer. Surfaced beneath coaching
    /// responses so clients (web, iOS, Android) can display it without re-deriving the copy.
    /// Carried on successful results via <see cref="CoachResult.Disclaimer"/>.
    /// </summary>
    public const string SafetyDisclaimer =
        "MAI is a general health, nutrition, hydration, and fitness information tool for "
        + "educational purposes only. It is NOT a licensed medical professional and does NOT "
        + "provide medical diagnosis, clinical treatment, or prescription advice. Information "
        + "provided is not a substitute for advice from a qualified physician, registered "
        + "dietitian, or other licensed healthcare provider. Always consult a healthcare "
        + "professional before making significant changes to your diet, exercise programme, or "
        + "health management — especially if you have an existing medical condition, are "
        + "pregnant or breastfeeding, or are taking medications.";

    // ── Context section headers ─────────────────────────────────────────────────────
    private const string GoalSectionHeader = "## Your Goals";
    private const string IntakeSectionHeader = "## Today's Intake So Far";
    private const string PreferenceSectionHeader = "## Your Dietary Preferences";

    /// <summary>
    /// Returns the system prompt string. Provided as a method (rather than exposing the
    /// constant directly at the call site) so future overloads can inject tenant- or
    /// user-specific configuration without changing the caller.
    /// </summary>
    /// <returns>The system prompt establishing MAI's persona and guardrails.</returns>
    public string BuildSystemPrompt() => SystemPrompt;

    /// <summary>
    /// Builds the user-turn content sent to the Messages API. Populated context sections are
    /// prepended before the user's message; absent fields are omitted.
    /// </summary>
    /// <param name="userMessage">The raw user-supplied message text.</param>
    /// <param name="context">Optional structured context. Null-safe; missing fields are omitted.</param>
    /// <returns>The fully composed user-turn content string.</returns>
    public string BuildUserContent(string userMessage, CoachingContext? context)
    {
        if (context is null)
        {
            return userMessage;
        }

        var sb = new StringBuilder();

        var hasGoalSection = context.PrimaryGoal is not null
            || context.DailyCalorieTarget.HasValue
            || context.DailyProteinTargetGrams.HasValue
            || context.ActivityLevel is not null;

        if (hasGoalSection)
        {
            sb.AppendLine(GoalSectionHeader);
            if (context.PrimaryGoal is not null)
            {
                sb.AppendLine($"- Primary goal: {context.PrimaryGoal}");
            }

            if (context.ActivityLevel is not null)
            {
                sb.AppendLine($"- Activity level: {context.ActivityLevel}");
            }

            if (context.DailyCalorieTarget.HasValue)
            {
                sb.AppendLine($"- Daily calorie target: {context.DailyCalorieTarget} kcal");
            }

            if (context.DailyProteinTargetGrams.HasValue)
            {
                sb.AppendLine($"- Daily protein target: {context.DailyProteinTargetGrams} g");
            }
        }

        var hasIntakeSection = context.TodayCaloriesConsumed.HasValue
            || context.TodayProteinConsumedGrams.HasValue;

        if (hasIntakeSection)
        {
            sb.AppendLine(IntakeSectionHeader);
            if (context.TodayCaloriesConsumed.HasValue)
            {
                sb.AppendLine($"- Calories consumed: {context.TodayCaloriesConsumed} kcal");
            }

            if (context.TodayProteinConsumedGrams.HasValue)
            {
                sb.AppendLine($"- Protein consumed: {context.TodayProteinConsumedGrams} g");
            }
        }

        if (context.DietaryPreferences is not null)
        {
            sb.AppendLine(PreferenceSectionHeader);
            sb.AppendLine(context.DietaryPreferences);
        }

        if (sb.Length > 0)
        {
            sb.AppendLine("---");
        }

        sb.Append(userMessage);
        return sb.ToString();
    }
}
