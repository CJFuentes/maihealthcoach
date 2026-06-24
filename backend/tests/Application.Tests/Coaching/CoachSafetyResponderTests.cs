using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.Tests.Coaching;

/// <summary>
/// Unit tests for <see cref="CoachSafetyResponder"/>. These guard the static guardrail copy:
/// the sentinel must be recognisably non-model, the high-risk redirect must decline and point
/// to help, and the elevated note must steer toward professional guidance.
/// </summary>
public sealed class CoachSafetyResponderTests
{
    // ── Sentinel must be present and NOT look like a real model id ───────────────────
    [Fact]
    public void GuardrailModelSentinel_IsNonEmptyAndNotARealModelId()
    {
        Assert.False(string.IsNullOrWhiteSpace(CoachSafetyResponder.GuardrailModelSentinel));
        Assert.DoesNotContain(
            "claude-", CoachSafetyResponder.GuardrailModelSentinel, StringComparison.OrdinalIgnoreCase);
    }

    // ── High-risk redirect must decline and route to professional/crisis resources ───
    [Fact]
    public void HighRiskRedirectText_DeclinesAndPointsToHelp()
    {
        var text = CoachSafetyResponder.HighRiskRedirectText;

        Assert.False(string.IsNullOrWhiteSpace(text));

        var mentionsHelp =
            text.Contains("988", StringComparison.OrdinalIgnoreCase)
            || text.Contains("professional", StringComparison.OrdinalIgnoreCase)
            || text.Contains("helpline", StringComparison.OrdinalIgnoreCase)
            || text.Contains("emergency", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsHelp, "High-risk redirect should reference 988/professional/helpline/emergency help.");
    }

    // ── Elevated note must mention professional dietary/healthcare guidance ───────────
    [Fact]
    public void ElevatedRiskSafetyNote_IsNonEmptyAndMentionsProfessionalGuidance()
    {
        var note = CoachSafetyResponder.ElevatedRiskSafetyNote;

        Assert.False(string.IsNullOrWhiteSpace(note));

        var mentionsProfessional =
            note.Contains("dietitian", StringComparison.OrdinalIgnoreCase)
            || note.Contains("healthcare", StringComparison.OrdinalIgnoreCase);
        Assert.True(mentionsProfessional, "Elevated note should mention a dietitian or healthcare provider.");
    }
}
