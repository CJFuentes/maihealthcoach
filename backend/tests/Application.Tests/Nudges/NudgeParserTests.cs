using MAIHealthCoach.Application.Nudges;

namespace MAIHealthCoach.Application.Tests.Nudges;

/// <summary>
/// Unit tests for <see cref="NudgeParser"/>. The parser is defensive by design — it must never throw
/// and always return a usable nudge with a non-empty message — so these cover a well-formed JSON
/// object, an object wrapped in prose, malformed JSON, plain prose, empty input, variant message and
/// tone key spellings, and a JSON object that carries no recognised message field.
/// </summary>
public sealed class NudgeParserTests
{
    private const string DefaultMessage = "Keep going — you're doing great!";

    [Fact]
    public void Parse_ValidJsonObject_ReturnsMessageAndTone()
    {
        const string reply = """{"message": "You're on a roll — keep it up!", "tone": "celebratory"}""";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal("You're on a roll — keep it up!", nudge.Message);
        Assert.Equal("celebratory", nudge.Tone);
    }

    [Fact]
    public void Parse_JsonEmbeddedInProse_ExtractsAndParses()
    {
        const string reply =
            "Sure, here's a nudge for you:\n" +
            "{\"message\": \"Every healthy choice counts — nice work today.\", \"tone\": \"encouraging\"}\n" +
            "Hope that helps!";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal("Every healthy choice counts — nice work today.", nudge.Message);
        Assert.Equal("encouraging", nudge.Tone);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsFreeformFallbackWithFullText()
    {
        const string reply = "{message: this is not valid json at all";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal(reply.Trim(), nudge.Message);
        Assert.Null(nudge.Tone);
    }

    [Fact]
    public void Parse_PlainProseNoBraces_ReturnsProseVerbatim()
    {
        const string reply = "You showed up today, and that's what matters most. Keep it going.";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal(reply.Trim(), nudge.Message);
        Assert.Null(nudge.Tone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public void Parse_EmptyOrWhitespace_ReturnsNonEmptyDefaultMessage(string reply)
    {
        var nudge = NudgeParser.Parse(reply);

        Assert.Equal(DefaultMessage, nudge.Message);
        Assert.False(string.IsNullOrWhiteSpace(nudge.Message));
        Assert.NotEqual(reply, nudge.Message);
        Assert.Null(nudge.Tone);
    }

    [Theory]
    [InlineData("nudge")]
    [InlineData("text")]
    public void Parse_VariantMessageKeySpellings_Parses(string key)
    {
        var reply = $$"""{"{{key}}": "Small steps add up — proud of you."}""";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal("Small steps add up — proud of you.", nudge.Message);
    }

    [Fact]
    public void Parse_VariantToneKey_ParsesIntoTone()
    {
        const string reply = """{"message": "Great momentum today!", "mood": "upbeat"}""";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal("Great momentum today!", nudge.Message);
        Assert.Equal("upbeat", nudge.Tone);
    }

    [Fact]
    public void Parse_JsonObjectWithNoRecognizedMessageField_ReturnsDefaultNotRawJson()
    {
        const string reply = """{"mood": "happy"}""";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal(DefaultMessage, nudge.Message);
        Assert.DoesNotContain("{", nudge.Message);
        Assert.DoesNotContain("mood", nudge.Message);
    }

    [Fact]
    public void Parse_EmptyTone_ReturnsNullTone()
    {
        const string reply = """{"message": "Keep showing up for yourself.", "tone": "   "}""";

        var nudge = NudgeParser.Parse(reply);

        Assert.Equal("Keep showing up for yourself.", nudge.Message);
        Assert.Null(nudge.Tone);
    }
}
