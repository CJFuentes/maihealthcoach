namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the authenticated coach chat feature (issue #39), bound from
/// the <c>CoachChat</c> configuration section. Controls the per-user send rate limit, the maximum
/// inbound message length, and how much prior history is replayed to the model for context.
/// </summary>
/// <remarks>
/// All values ship with safe defaults so the feature works with no configuration. The validator
/// only range-checks supplied values; nothing here is secret.
/// </remarks>
public sealed class CoachChatOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "CoachChat";

    /// <summary>
    /// Maximum number of chat messages a single user may send within <see cref="WindowSeconds"/>
    /// before being rate-limited (429). Defaults to 10.
    /// </summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// Length of the fixed rate-limit window, in seconds. Defaults to 60.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum allowed length, in characters, of an inbound chat message. Enforced at the API
    /// layer before the message reaches the model or is persisted. Defaults to 4000.
    /// </summary>
    public int MaxMessageLength { get; set; } = 4000;

    /// <summary>
    /// Maximum number of most-recent prior <em>messages</em> (not pairs) replayed to the model as
    /// conversation context, ordered by sequence. Defaults to 20.
    /// </summary>
    public int HistoryTurnLimit { get; set; } = 20;
}
