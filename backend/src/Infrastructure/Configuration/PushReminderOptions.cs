namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the push-reminder background service (issue #48), bound from the
/// <c>PushReminder</c> configuration section. Controls whether the periodic reminder sweep runs, how
/// often it ticks, and how many users it considers per tick. All values ship with safe defaults and
/// none are secret.
/// </summary>
/// <remarks>
/// The feature is <see cref="Enabled"/>=false by default: the hosted service is always registered and
/// loops, but each tick is a no-op until reminders are explicitly turned on for an environment. This
/// keeps the server-side scheduling infra inert (and the no-op sender silent) until real push
/// delivery is wired up.
/// </remarks>
public sealed class PushReminderOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "PushReminder";

    /// <summary>
    /// Master switch for the reminder sweep. When <see langword="false"/> (the default) the hosted
    /// service still runs but every tick returns immediately without querying or sending anything.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// How often the reminder sweep runs, in seconds. Also doubles as the per-user/per-kind
    /// de-duplication window so a reminder is sent at most once per tick interval. Defaults to 300
    /// (5 minutes), matching the decider's match window granularity.
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of users with reminders enabled considered in a single tick, bounding the
    /// per-tick query and send fan-out. Defaults to 1000.
    /// </summary>
    public int MaxUsersPerTick { get; set; } = 1000;
}
