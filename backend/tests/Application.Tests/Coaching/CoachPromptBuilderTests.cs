using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.Tests.Coaching;

/// <summary>
/// Unit tests for <see cref="CoachPromptBuilder"/>. These assert the guardrail content of the
/// system prompt and disclaimer (refined under issue #41) and the user-content composition.
/// All Contains checks are case-insensitive so they survive minor copy edits.
/// </summary>
public sealed class CoachPromptBuilderTests
{
    private readonly CoachPromptBuilder _sut = new();

    [Fact]
    public void SystemPrompt_ContainsPersonaAndMedicalDisclaimerGuardrail()
    {
        var prompt = CoachPromptBuilder.SystemPrompt;

        Assert.Contains("MAI", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a substitute for professional medical", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_ForbidsDiagnosisAndPrescription()
    {
        var prompt = CoachPromptBuilder.SystemPrompt;

        Assert.Contains("diagnose", prompt, StringComparison.OrdinalIgnoreCase);

        var mentionsMeds =
            prompt.Contains("prescription", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("medication", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsMeds, "System prompt should forbid prescription/medication advice.");
    }

    [Fact]
    public void SystemPrompt_AddressesAllergiesDietaryRestrictionsAndUnsafeProtocols()
    {
        var prompt = CoachPromptBuilder.SystemPrompt;

        Assert.Contains("allerg", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dietary", prompt, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("purg", prompt, StringComparison.OrdinalIgnoreCase);
        var mentionsExtreme =
            prompt.Contains("rapid weight loss", StringComparison.OrdinalIgnoreCase)
            || prompt.Contains("extreme", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsExtreme, "System prompt should reference rapid weight loss / extreme protocols.");
    }

    [Fact]
    public void BuildSystemPrompt_ReturnsSystemPromptConstant()
    {
        Assert.Equal(CoachPromptBuilder.SystemPrompt, _sut.BuildSystemPrompt());
    }

    [Fact]
    public void SafetyDisclaimer_IsNonEmptyAndDirectsToHealthcareProfessional()
    {
        var disclaimer = CoachPromptBuilder.SafetyDisclaimer;

        Assert.False(string.IsNullOrWhiteSpace(disclaimer));

        var notSubstituteOrConsult =
            disclaimer.Contains("not a substitute", StringComparison.OrdinalIgnoreCase)
            || disclaimer.Contains("consult", StringComparison.OrdinalIgnoreCase);
        Assert.True(notSubstituteOrConsult, "Disclaimer should say it is not a substitute or to consult.");

        var mentionsProvider =
            disclaimer.Contains("healthcare", StringComparison.OrdinalIgnoreCase)
            || disclaimer.Contains("physician", StringComparison.OrdinalIgnoreCase)
            || disclaimer.Contains("dietitian", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsProvider, "Disclaimer should name a healthcare provider/physician/dietitian.");
    }

    [Fact]
    public void BuildUserContent_WithNullContext_ReturnsRawMessage()
    {
        const string message = "What should I eat for dinner?";

        Assert.Equal(message, _sut.BuildUserContent(message, context: null));
    }

    [Fact]
    public void BuildUserContent_WithPopulatedContext_IncludesGoalIntakeAndPreferenceSections()
    {
        var context = new CoachingContext(
            PrimaryGoal: "Lose weight",
            DailyCalorieTarget: 1800,
            TodayCaloriesConsumed: 600,
            DietaryPreferences: "Vegan, tree-nut allergy");

        var content = _sut.BuildUserContent("Suggest a dinner", context);

        Assert.Contains("Primary goal: Lose weight", content);
        Assert.Contains("1800 kcal", content);
        Assert.Contains("Calories consumed: 600 kcal", content);
        Assert.Contains("Vegan, tree-nut allergy", content);
        // The raw user message is always appended after the context sections.
        Assert.Contains("Suggest a dinner", content);
    }
}
