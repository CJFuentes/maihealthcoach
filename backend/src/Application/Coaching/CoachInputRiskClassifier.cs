using System.Text.RegularExpressions;

namespace MAIHealthCoach.Application.Coaching;

/// <summary>Severity categories returned by <see cref="CoachInputRiskClassifier"/>.</summary>
public enum InputRiskLevel
{
    /// <summary>No red-flag signals detected; the request may proceed normally.</summary>
    None = 0,

    /// <summary>
    /// Language suggesting extreme dieting, very-low-calorie targets, rapid weight loss, or
    /// medical symptoms. MAI should still respond but steer to safe ranges and recommend
    /// professional help.
    /// </summary>
    Elevated = 1,

    /// <summary>
    /// Unambiguous harm intent: purging, self-harm, suicidal ideation, induced vomiting,
    /// pro-eating-disorder content, or overdose. MAI must decline detailed coaching and
    /// redirect to crisis resources without calling the LLM.
    /// </summary>
    High = 2,
}

/// <summary>
/// Lightweight, deterministic, synchronous pre-screen classifier for user coaching input.
/// Uses source-generated regular expressions to detect red-flag categories without external
/// calls or allocations beyond the matched input. Stateless and cheap — instantiate once and
/// reuse, or construct on demand.
/// </summary>
/// <remarks>
/// <para>
/// The classifier is intentionally conservative on <b>unambiguous</b> harm signals and
/// deliberately narrow elsewhere: routine phrases such as "lose weight", "diet", "calorie",
/// and "protein" on their own must classify as <see cref="InputRiskLevel.None"/>. The patterns
/// are flat, word-boundary-anchored alternations with no nested quantifiers, so they cannot
/// exhibit catastrophic backtracking.
/// </para>
/// <para>
/// Evaluation order is precedence-significant: <see cref="InputRiskLevel.High"/> patterns are
/// tested first and short-circuit, then <see cref="InputRiskLevel.Elevated"/>; otherwise the
/// result is <see cref="InputRiskLevel.None"/>. The full policy rationale lives in
/// <c>docs/coaching-safety-guardrails.md</c>.
/// </para>
/// </remarks>
public sealed partial class CoachInputRiskClassifier
{
    /// <summary>
    /// Matches unambiguous harm intent: purging/induced vomiting, self-harm, suicide,
    /// pro-eating-disorder ("pro-ana"/"thinspo") content, and overdose. Word-boundary anchored,
    /// flat alternation — no nested quantifiers.
    /// </summary>
    /// <remarks>
    /// Covers phrasings such as "how do I purge after eating", "make myself throw up",
    /// "make myself vomit", "self harm", "kill myself".
    /// </remarks>
    [GeneratedRegex(
        @"\b(purge|purging|purged|self[ -]?harm|self[ -]?harming|suicide|suicidal|thinspo|thinspiration|pro[ -]?ana|pro[ -]?mia|overdose|overdosing)\b|\bmake myself (throw up|vomit|sick)\b|\bthrow(ing)? up after (eat|eating|meals?|food)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HighRiskRegex();

    /// <summary>
    /// Matches extreme-diet phrasing and medical-symptom signals that warrant a safety note but
    /// not a pre-LLM intercept: crash dieting, deliberate starvation/not eating, very-low-calorie
    /// targets, rapid weight loss, plus first-person medical signals (chest pain, diabetes,
    /// "my medication"). Word-boundary anchored, flat alternation — no nested quantifiers.
    /// </summary>
    [GeneratedRegex(
        @"\b(crash diet|crash dieting|starve myself|starving myself|stop eating|not eating|rapid weight loss|very low calorie|extremely low calorie)\b|\b\d{2,4} calories? (a|per) day\b|\b(chest pain|i have diabetes|i'?m diabetic|my medication|my medications|my prescription)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElevatedRiskRegex();

    /// <summary>
    /// Classifies the given user message into a risk level. Pure and deterministic — no I/O.
    /// </summary>
    /// <param name="userMessage">
    /// The raw user-supplied message text. <see langword="null"/> or whitespace-only input is
    /// treated as <see cref="InputRiskLevel.None"/>.
    /// </param>
    /// <returns>
    /// <see cref="InputRiskLevel.High"/> if any high-risk (unambiguous harm) pattern matches;
    /// otherwise <see cref="InputRiskLevel.Elevated"/> if any elevated-risk pattern matches;
    /// otherwise <see cref="InputRiskLevel.None"/>. High wins precedence over Elevated.
    /// </returns>
    public InputRiskLevel Classify(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return InputRiskLevel.None;
        }

        // High wins precedence: evaluate unambiguous harm first and return on the first hit.
        if (HighRiskRegex().IsMatch(userMessage))
        {
            return InputRiskLevel.High;
        }

        if (ElevatedRiskRegex().IsMatch(userMessage))
        {
            return InputRiskLevel.Elevated;
        }

        return InputRiskLevel.None;
    }
}
