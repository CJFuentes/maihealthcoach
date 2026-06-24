using MAIHealthCoach.Domain.Common;

namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// A single point-in-time body weight measurement belonging to a <see cref="UserProfile"/>.
/// Extends <see cref="EntityBase"/> for its own stable <c>Id</c> and system-audit timestamps.
/// </summary>
/// <remarks>
/// Two timestamps exist for different purposes:
/// <list type="bullet">
///   <item>
///     <see cref="MeasuredAt"/> — the user-domain time of measurement.
///     In v1 this is set to server UTC at the moment the PUT /me/profile request is processed.
///     Future versions may allow the client to supply a past date.
///   </item>
///   <item>
///     <see cref="EntityBase.CreatedAt"/> — the system insert time (row-created-at in the DB).
///     Useful for auditing and backfill detection; always server UTC.
///   </item>
/// </list>
/// </remarks>
public sealed class WeightMeasurement : EntityBase
{
    /// <summary>Foreign key referencing the owning <see cref="UserProfile.Id"/>.</summary>
    public Guid UserProfileId { get; private set; }

    /// <summary>Body weight in kilograms at the time of measurement.</summary>
    public double WeightKg { get; private set; }

    /// <summary>
    /// UTC instant representing when the measurement was taken (user-domain time).
    /// In v1, set to <see cref="DateTimeOffset.UtcNow"/> server-side at request time.
    /// </summary>
    public DateTimeOffset MeasuredAt { get; private set; }

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private WeightMeasurement() { }

    /// <summary>
    /// Creates a new <see cref="WeightMeasurement"/> for the given profile.
    /// </summary>
    internal static WeightMeasurement Create(Guid userProfileId, double weightKg, DateTimeOffset measuredAt)
    {
        return new WeightMeasurement
        {
            UserProfileId = userProfileId,
            WeightKg = weightKg,
            MeasuredAt = measuredAt,
            CreatedAt = measuredAt,
            UpdatedAt = measuredAt,
        };
    }
}
