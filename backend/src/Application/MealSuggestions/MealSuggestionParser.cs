using System.Text.Json;

namespace MAIHealthCoach.Application.MealSuggestions;

/// <summary>
/// Parses a model reply into a list of <see cref="MealOption"/>. The model is asked to respond
/// with a JSON array, but replies are inherently untrusted: they may wrap the array in prose,
/// emit malformed JSON, or use variant field names. This parser is defensive — it extracts the
/// first <c>[ … ]</c> span, tolerates several key spellings, and falls back to a single freeform
/// option carrying the raw reply text rather than ever throwing.
/// </summary>
public static class MealSuggestionParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses <paramref name="replyText"/> into meal options. Always returns at least one option:
    /// on any parse failure (or empty/whitespace input), it returns a single freeform option whose
    /// rationale carries the trimmed reply text.
    /// </summary>
    /// <param name="replyText">The raw model reply.</param>
    /// <returns>One or more parsed meal options; never empty.</returns>
    public static IReadOnlyList<MealOption> Parse(string replyText)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            return [new MealOption("Suggestion", null, null, null, null, string.Empty)];
        }

        try
        {
            var firstBracket = replyText.IndexOf('[');
            var lastBracket = replyText.LastIndexOf(']');

            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                var slice = replyText[firstBracket..(lastBracket + 1)];
                var items = JsonSerializer.Deserialize<List<MealOptionJson>>(slice, SerializerOptions);

                if (items is { Count: > 0 })
                {
                    var validOptions = items
                        .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                        .Select(i => new MealOption(
                            Name: i.Name!,
                            Calories: i.Calories ?? i.Kcal,
                            ProteinGrams: i.ProteinGrams ?? i.Protein,
                            CarbGrams: i.CarbGrams ?? i.Carbohydrates ?? i.Carbs,
                            FatGrams: i.FatGrams ?? i.Fat,
                            Rationale: i.Rationale ?? i.Reason ?? i.Description ?? string.Empty))
                        .ToList();

                    if (validOptions.Count > 0)
                    {
                        return validOptions;
                    }
                }
            }
        }
        catch
        {
            // Swallow any parse error and fall through to the freeform fallback below.
        }

        return [new MealOption("Suggestion", null, null, null, null, replyText.Trim())];
    }

    // Tolerant DTO for deserialization: each macro and rationale accepts several key spellings so
    // the parser copes with model output variance. Case-insensitive matching handles casing drift.
    private sealed class MealOptionJson
    {
        public string? Name { get; set; }
        public int? Calories { get; set; }
        public int? Kcal { get; set; }
        public int? ProteinGrams { get; set; }
        public int? Protein { get; set; }
        public int? CarbGrams { get; set; }
        public int? Carbohydrates { get; set; }
        public int? Carbs { get; set; }
        public int? FatGrams { get; set; }
        public int? Fat { get; set; }
        public string? Rationale { get; set; }
        public string? Reason { get; set; }
        public string? Description { get; set; }
    }
}
