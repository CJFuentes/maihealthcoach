using MAIHealthCoach.Application.MealSuggestions;

namespace MAIHealthCoach.Application.Tests.MealSuggestions;

/// <summary>
/// Unit tests for <see cref="MealSuggestionParser"/>. The parser is defensive by design — it must
/// never throw and always return at least one option — so these cover well-formed JSON, JSON wrapped
/// in prose, malformed JSON, plain prose, an empty array, and variant field-name spellings.
/// </summary>
public sealed class MealSuggestionParserTests
{
    [Fact]
    public void Parse_ValidJsonArray_ReturnsThreeOptionsWithCorrectFields()
    {
        const string reply =
            """
            [
              {"name": "Grilled chicken salad", "calories": 420, "proteinGrams": 38, "carbGrams": 18, "fatGrams": 22, "rationale": "Lean and filling."},
              {"name": "Greek yogurt with berries", "calories": 220, "proteinGrams": 18, "carbGrams": 24, "fatGrams": 5, "rationale": "High protein snack."},
              {"name": "Lentil soup", "calories": 310, "proteinGrams": 16, "carbGrams": 45, "fatGrams": 6, "rationale": "Plant-based and hearty."}
            ]
            """;

        var options = MealSuggestionParser.Parse(reply);

        Assert.Equal(3, options.Count);

        var first = options[0];
        Assert.Equal("Grilled chicken salad", first.Name);
        Assert.Equal(420, first.Calories);
        Assert.Equal(38, first.ProteinGrams);
        Assert.Equal(18, first.CarbGrams);
        Assert.Equal(22, first.FatGrams);
        Assert.Equal("Lean and filling.", first.Rationale);
    }

    [Fact]
    public void Parse_JsonEmbeddedInProse_ExtractsAndParses()
    {
        const string reply =
            "Here are some ideas for you!\n" +
            "[{\"name\": \"Oatmeal\", \"calories\": 300, \"proteinGrams\": 10, \"carbGrams\": 54, \"fatGrams\": 5, \"rationale\": \"Slow-release carbs.\"}]\n" +
            "Let me know if you'd like more.";

        var options = MealSuggestionParser.Parse(reply);

        Assert.Single(options);
        Assert.Equal("Oatmeal", options[0].Name);
        Assert.Equal(300, options[0].Calories);
        Assert.Equal("Slow-release carbs.", options[0].Rationale);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsSingleFreeformOptionWithFullText()
    {
        const string reply = "[{bad json that will not parse";

        var options = MealSuggestionParser.Parse(reply);

        Assert.Single(options);
        Assert.Equal("Suggestion", options[0].Name);
        Assert.Null(options[0].Calories);
        Assert.Equal(reply.Trim(), options[0].Rationale);
    }

    [Fact]
    public void Parse_PlainProseNoBrackets_ReturnsSingleFreeformOption()
    {
        const string reply = "I would suggest a balanced plate of grilled fish, rice, and vegetables.";

        var options = MealSuggestionParser.Parse(reply);

        Assert.Single(options);
        Assert.Equal("Suggestion", options[0].Name);
        Assert.Equal(reply.Trim(), options[0].Rationale);
    }

    [Fact]
    public void Parse_EmptyArray_FallsBackToFreeform()
    {
        const string reply = "[]";

        var options = MealSuggestionParser.Parse(reply);

        Assert.Single(options);
        Assert.Equal("Suggestion", options[0].Name);
    }

    [Fact]
    public void Parse_CaseVariantFieldNames_Parses()
    {
        const string reply =
            """
            [
              {"name": "Tuna wrap", "kcal": 350, "protein": 28, "carbs": 30, "fat": 12, "reason": "Quick and protein-rich."}
            ]
            """;

        var options = MealSuggestionParser.Parse(reply);

        Assert.Single(options);
        var option = options[0];
        Assert.Equal("Tuna wrap", option.Name);
        Assert.Equal(350, option.Calories);
        Assert.Equal(28, option.ProteinGrams);
        Assert.Equal(30, option.CarbGrams);
        Assert.Equal(12, option.FatGrams);
        Assert.Equal("Quick and protein-rich.", option.Rationale);
    }
}
