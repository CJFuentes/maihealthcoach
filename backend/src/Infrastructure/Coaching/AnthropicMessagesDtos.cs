using System.Text.Json.Serialization;

namespace MAIHealthCoach.Infrastructure.Coaching;

/// <summary>Request body sent to <c>POST {BaseUrl}/v1/messages</c>.</summary>
internal sealed class AnthropicRequest
{
    /// <summary>The model identifier (e.g. "claude-sonnet-4-6").</summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>The maximum number of tokens to generate.</summary>
    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    /// <summary>The system prompt establishing persona and guardrails.</summary>
    [JsonPropertyName("system")]
    public required string System { get; init; }

    /// <summary>The conversation turns. For this foundational service, a single user turn.</summary>
    [JsonPropertyName("messages")]
    public required IReadOnlyList<AnthropicMessage> Messages { get; init; }
}

/// <summary>A single turn in the messages array.</summary>
internal sealed class AnthropicMessage
{
    /// <summary>The role of the turn, e.g. "user".</summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    /// <summary>The textual content of the turn.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>
/// Top-level response envelope from <c>POST /v1/messages</c> (success path). Only the fields
/// needed for coaching are mapped; unmapped fields are ignored by the default deserializer.
/// </summary>
internal sealed class AnthropicResponse
{
    /// <summary>The message identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The model that produced the response.</summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>The content blocks. The first block of type "text" carries the reply.</summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<AnthropicContentBlock>? Content { get; init; }

    /// <summary>The reason generation stopped (e.g. "end_turn").</summary>
    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    /// <summary>Token usage reported by the API.</summary>
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; init; }
}

/// <summary>A content block in the response. <c>type == "text"</c> is the relevant kind.</summary>
internal sealed class AnthropicContentBlock
{
    /// <summary>The block type (e.g. "text").</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The text content when <see cref="Type"/> is "text".</summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>Token usage reported by the API.</summary>
internal sealed class AnthropicUsage
{
    /// <summary>Input tokens consumed.</summary>
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    /// <summary>Output tokens generated.</summary>
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}
