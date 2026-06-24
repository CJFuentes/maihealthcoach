using System.Text.Json;

namespace MAIHealthCoach.Application.Nudges;

/// <summary>
/// Parses a model reply into a <see cref="Nudge"/>. The model is asked to respond with a small JSON
/// object, but replies are inherently untrusted: they may wrap the object in prose, emit malformed
/// JSON, or use variant field names. This parser is defensive — it extracts the first <c>{ … }</c>
/// span, tolerates several key spellings, and falls back to a freeform nudge carrying the raw reply
/// text (or a sensible default) rather than ever throwing.
/// </summary>
public static class NudgeParser
{
    private const string DefaultMessage = "Keep going — you're doing great!";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses <paramref name="replyText"/> into a nudge. Always returns a usable nudge: on any parse
    /// failure (or empty/whitespace input), it returns a freeform nudge whose message is the trimmed
    /// reply text, or a default encouragement when the reply is empty.
    /// </summary>
    /// <param name="replyText">The raw model reply.</param>
    /// <returns>A nudge with a non-empty message; never throws.</returns>
    public static Nudge Parse(string replyText)
    {
        if (string.IsNullOrWhiteSpace(replyText))
        {
            return new Nudge(DefaultMessage, null);
        }

        try
        {
            var firstBrace = replyText.IndexOf('{');
            var lastBrace = replyText.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                var slice = replyText[firstBrace..(lastBrace + 1)];
                var parsed = JsonSerializer.Deserialize<NudgeJson>(slice, SerializerOptions);

                var message = parsed?.Message ?? parsed?.Nudge ?? parsed?.Text;

                if (!string.IsNullOrWhiteSpace(message))
                {
                    var tone = parsed?.Tone ?? parsed?.Mood;
                    return new Nudge(
                        message.Trim(),
                        string.IsNullOrWhiteSpace(tone) ? null : tone.Trim());
                }

                // A JSON object was found and parsed but carries no recognised message field; return
                // the default encouragement rather than surfacing the raw JSON (e.g. {"mood":"excited"}).
                return new Nudge(DefaultMessage, null);
            }
        }
        catch
        {
            // Swallow any parse error and fall through to the freeform fallback below.
        }

        // No JSON object span was found (or deserialization threw): surface the plain-prose reply
        // verbatim, or the default encouragement when the reply is empty.
        return new Nudge(replyText.Trim(), null);
    }

    // Tolerant DTO for deserialization: the message and tone each accept several key spellings so
    // the parser copes with model output variance. Case-insensitive matching handles casing drift.
    private sealed class NudgeJson
    {
        public string? Message { get; set; }
        public string? Nudge { get; set; }
        public string? Text { get; set; }
        public string? Tone { get; set; }
        public string? Mood { get; set; }
    }
}
