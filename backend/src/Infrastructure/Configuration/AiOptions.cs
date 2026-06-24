namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the Claude (Anthropic) AI integration, bound from the
/// <c>Ai</c> configuration section. The <see cref="CoachService"/> (issue #36) will consume
/// these values; this class only establishes the config contract so the AI client can be
/// wired up later without further config plumbing.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ApiKey"/> is a <strong>server-side secret</strong>. It must never be
/// committed to source control or sent to any client. Supply it via an environment variable
/// (<c>Ai__ApiKey</c>) or .NET user-secrets locally; in CI/production it comes from the
/// platform's secret store. appsettings ships an empty placeholder only.
/// </para>
/// <para>
/// The app must build, start, and pass health checks with an empty <see cref="ApiKey"/>,
/// because the AI features are not wired up yet. Validation therefore only checks the
/// <em>format</em> of values that are actually supplied (see the options validators).
/// </para>
/// </remarks>
public sealed class AiOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "Ai";

    /// <summary>Default model used for routine coaching interactions.</summary>
    public const string DefaultModelId = "claude-sonnet-4-6";

    /// <summary>Model used when a conversation is escalated to a more capable model.</summary>
    public const string EscalationModelId = "claude-opus-4-8";

    /// <summary>Default Anthropic API base URL.</summary>
    public const string DefaultBaseUrl = "https://api.anthropic.com";

    /// <summary>
    /// Server-side Anthropic API key. <strong>Secret.</strong> Empty by default so the app
    /// starts without it; the AI client must guard against an empty value until #36 lands.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The model used for standard coaching requests. Defaults to Claude Sonnet.</summary>
    public string DefaultModel { get; set; } = DefaultModelId;

    /// <summary>The model used when a request is escalated. Defaults to Claude Opus.</summary>
    public string EscalationModel { get; set; } = EscalationModelId;

    /// <summary>Base URL for the Anthropic API.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>HTTP request timeout, in seconds, for calls to the AI provider.</summary>
    public int TimeoutSeconds { get; set; } = 100;
}
