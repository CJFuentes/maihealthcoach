namespace MAIHealthCoach.Application.Nudges;

/// <summary>
/// A single parsed motivational nudge. <see cref="Message"/> is always present (the parser falls
/// back to the raw reply, or a sensible default, rather than ever yielding an empty message).
/// <see cref="Tone"/> is the model's optional self-described tone (e.g. "celebratory",
/// "encouraging") and may be omitted.
/// </summary>
/// <param name="Message">The short, warm, non-judgmental nudge text suitable for display or notification.</param>
/// <param name="Tone">The model's optional self-described tone, or <see langword="null"/> if absent.</param>
public sealed record Nudge(
    string Message,
    string? Tone);
