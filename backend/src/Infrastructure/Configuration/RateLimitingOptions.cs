namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the global API rate limiting and abuse protection (issue #45),
/// bound from the <c>RateLimiting</c> configuration section. Controls the <em>global</em> limiter
/// applied to every request — partitioned by authenticated user when present, else by client IP so
/// anonymous abuse is bounded too.
/// </summary>
/// <remarks>
/// Per-user limits for the expensive coach LLM endpoints (chat / meal-suggestions / nudge) are
/// driven by <see cref="CoachChatOptions"/> (consolidated with issue #39's chat limiter), not by
/// this class. All values here ship with safe defaults so the feature works with no configuration;
/// nothing here is secret.
/// </remarks>
public sealed class RateLimitingOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Master kill-switch for all API rate limiting. When <see langword="false"/>, both the global
    /// limiter and the named coach LLM policy resolve to a no-op partition, so no request is ever
    /// throttled. Defaults to <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of requests a single partition (authenticated user, else client IP) may make
    /// within <see cref="GlobalWindowSeconds"/> across all rate-limited endpoints before being
    /// rejected with a 429. Defaults to 100.
    /// </summary>
    public int GlobalPermitLimit { get; set; } = 100;

    /// <summary>
    /// Length of the global fixed-window, in seconds. Defaults to 60.
    /// </summary>
    public int GlobalWindowSeconds { get; set; } = 60;
}
