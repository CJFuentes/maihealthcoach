namespace MAIHealthCoach.Application.Coaching;

/// <summary>
/// Owns the static response text used when <see cref="CoachInputRiskClassifier"/> returns an
/// elevated or high risk level. Centralises all redirect, helpline, and safety-note language so
/// it can be reviewed and updated in one place.
/// </summary>
public static class CoachSafetyResponder
{
    /// <summary>
    /// The sentinel value placed in <see cref="CoachResult.ModelUsed"/> when a response is
    /// produced by the guardrail layer instead of the LLM. Deliberately not a real model id so
    /// downstream billing/analytics can exclude guardrail short-circuits.
    /// </summary>
    public const string GuardrailModelSentinel = "guardrail-safety-sentinel";

    /// <summary>
    /// The reply text returned for high-risk inputs (purging, self-harm, suicidal ideation,
    /// induced vomiting, pro-eating-disorder content, overdose). MAI declines to provide the
    /// requested information, expresses care, and redirects to professional and crisis resources.
    /// The language is intentionally region-generic.
    /// </summary>
    public const string HighRiskRedirectText =
        """
        I'm really glad you reached out, and I want to make sure you get the right kind of support.

        This is outside what MAI can safely help with — I'm a nutrition, hydration, and fitness coach, not a medical or mental-health professional, and I won't provide guidance that could put you at risk. What you're describing deserves care from someone qualified to give it.

        Please consider reaching out to one of these:

        - A doctor, registered dietitian, or a licensed mental-health professional who can support you directly.
        - A local or national helpline. In the US and Canada you can call or text 988 (the Suicide and Crisis Lifeline) at any time, day or night.
        - If you are in immediate danger or having a medical emergency, please contact your local emergency services right away.

        You don't have to navigate this alone. When you're ready to talk about everyday nutrition or fitness goals, I'm here for that.
        """;

    /// <summary>
    /// A safety note appended to LLM-generated replies when the input is classified as
    /// <see cref="InputRiskLevel.Elevated"/>. The LLM still responds within safe ranges; this
    /// note steers the user toward professional guidance.
    /// </summary>
    public const string ElevatedRiskSafetyNote =
        """

        ---
        A note from MAI: some of what you raised — calorie targets, rapid weight change, fasting, or any medical symptom — depends heavily on your individual circumstances. Please keep changes within safe, sustainable ranges and check in with a registered dietitian or your healthcare provider before going further, especially if you have an existing medical condition or take medication.
        """;
}
