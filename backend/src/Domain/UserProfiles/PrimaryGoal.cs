namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// The user's primary weight / body-composition goal. Drives caloric-target calculations.
/// Stored as a string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum PrimaryGoal
{
    /// <summary>Caloric deficit to reduce body weight.</summary>
    Lose,

    /// <summary>Caloric balance to hold current weight.</summary>
    Maintain,

    /// <summary>Caloric surplus to increase body weight or muscle mass.</summary>
    Gain,
}
