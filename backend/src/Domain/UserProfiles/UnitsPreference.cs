namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// Display unit system preference. Defaults to <see cref="Metric"/> when not explicitly set.
/// Stored as a string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum UnitsPreference
{
    /// <summary>SI units: kilograms, centimetres.</summary>
    Metric,

    /// <summary>Imperial units: pounds, inches/feet.</summary>
    Imperial,
}
