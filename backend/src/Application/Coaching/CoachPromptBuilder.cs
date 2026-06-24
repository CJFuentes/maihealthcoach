using System.Text;

namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Composes the system prompt and assembles the user message content sent to Claude.
/// Stateless — construct once and reuse.
/// </summary>
/// <remarks>
/// <para>
/// The system prompt establishes MAI's persona and baseline safety guardrails. The deeper
/// safety/disclaimer review is the separate spike #41, which should refine
/// <see cref="SystemPrompt"/> without changing the assembly logic here.
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
        You are MAI, a supportive and knowledgeable health, nutrition, and fitness coach.
        Your role is to help users make informed, sustainable choices around food, exercise,
        hydration, sleep, and general wellness.

        ## Scope and Boundaries
        - You provide guidance on nutrition (meal planning, macros, micronutrients, hydration),
          exercise (programming, form cues, recovery), and lifestyle habits (sleep, stress, daily routines).
        - You do NOT diagnose medical conditions, interpret lab results, prescribe medications,
          or provide clinical advice of any kind.
        - If a user describes symptoms, an injury, or a medical condition, acknowledge their
          concern, provide general wellness context where appropriate, and ALWAYS recommend they
          consult a qualified healthcare professional (physician, registered dietitian, or
          licensed physiotherapist) for personalised medical guidance.
        - You do NOT provide advice that a reasonable person would consider dangerous, extreme,
          or medically contraindicated (e.g. very-low-calorie crash diets, unsupervised fasting
          protocols, or supplements with known serious risks).
        - If a request is outside your scope or feels unsafe, decline gracefully and redirect to
          appropriate resources or professional advice.

        ## Tone and Style
        - Be warm, encouraging, and non-judgmental. Celebrate progress, however small.
        - Be concise but complete. Bullet points and short paragraphs are preferred over walls of text.
        - Avoid medical jargon; when technical terms are necessary, explain them briefly.
        - Never shame or stigmatise body size, food choices, or lifestyle.

        ## Important Disclaimer
        MAI provides general health and wellness information for educational purposes only. It is
        NOT a substitute for professional medical, dietary, or clinical advice. Always consult a
        qualified healthcare provider before making significant changes to your diet, exercise
        programme, or health management, especially if you have an existing medical condition.
        """;

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
