using MAIHealthCoach.Application.Coaching;

namespace MAIHealthCoach.Application.Tests.Coaching;

/// <summary>
/// Unit tests for <see cref="CoachInputRiskClassifier"/>. The classifier is a deterministic,
/// synchronous pre-screen; each test instantiates it directly (no DI) and asserts the
/// <see cref="InputRiskLevel"/> for a representative phrase. The "None" cases are the most
/// important: they guard against the classifier over-triggering on routine coaching language.
/// </summary>
public sealed class CoachInputRiskClassifierTests
{
    private readonly CoachInputRiskClassifier _sut = new();

    // ── High: unambiguous harm intent (purging, self-harm, suicide, pro-ED, overdose) ──
    [Theory]
    [InlineData("How do I purge after eating?")]
    [InlineData("I make myself throw up after meals")]
    [InlineData("I want to self-harm")]
    [InlineData("I've been thinking about suicide")]
    [InlineData("thinspo tips please")]
    [InlineData("pro-ana advice")]
    [InlineData("how to overdose")]
    public void Classify_HighRiskPhrases_ReturnsHigh(string userMessage)
    {
        Assert.Equal(InputRiskLevel.High, _sut.Classify(userMessage));
    }

    // ── Elevated: extreme-diet phrasing and first-person medical signals ──────────────
    [Theory]
    [InlineData("Is a crash diet okay?")]
    [InlineData("I want to starve myself")]
    [InlineData("tell me about eating 500 calories a day")]
    [InlineData("I want rapid weight loss")]
    [InlineData("I have chest pain after running")]
    [InlineData("I have diabetes, what should I eat")]
    [InlineData("can I take this with my medication")]
    public void Classify_ElevatedRiskPhrases_ReturnsElevated(string userMessage)
    {
        Assert.Equal(InputRiskLevel.Elevated, _sut.Classify(userMessage));
    }

    // ── None: routine coaching language must NOT over-trigger ─────────────────────────
    [Theory]
    [InlineData("How much protein should I eat?")]
    [InlineData("I want to lose weight")]
    [InlineData("What's a good low-calorie dinner?")]
    [InlineData("Suggest a high-calorie smoothie")]
    [InlineData("How much water should I drink?")]
    [InlineData("Hi")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Classify_BenignOrEmptyInput_ReturnsNone(string? userMessage)
    {
        Assert.Equal(InputRiskLevel.None, _sut.Classify(userMessage));
    }

    // ── Precedence: an input matching BOTH a High and an Elevated term → High wins ────
    [Fact]
    public void Classify_InputMatchingBothHighAndElevated_ReturnsHigh()
    {
        Assert.Equal(InputRiskLevel.High, _sut.Classify("I want to crash diet and purge"));
    }

    // ── Case-insensitivity: high-risk terms match regardless of casing ───────────────
    [Fact]
    public void Classify_UppercaseHighRiskPhrase_ReturnsHigh()
    {
        Assert.Equal(InputRiskLevel.High, _sut.Classify("HOW DO I PURGE"));
    }
}
