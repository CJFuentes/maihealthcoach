namespace MAIHealthCoach.Api.Features.Coach;

/// <summary>
/// Response body for <c>GET /api/v1/me/coach/nudge</c> (issue #38). Carries the short motivational
/// message (suitable for display or notification), the model's optional self-described tone, and a
/// client-facing safety disclaimer.
/// </summary>
/// <param name="Message">The short, warm, non-judgmental nudge text.</param>
/// <param name="Tone">The model's optional self-described tone, or <see langword="null"/> if absent.</param>
/// <param name="Disclaimer">Client-facing safety disclaimer to surface beneath the nudge.</param>
public sealed record NudgeResponse(
    string Message,
    string? Tone,
    string? Disclaimer);
