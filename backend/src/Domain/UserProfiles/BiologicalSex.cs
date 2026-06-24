namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// Biological sex, used in nutrition and metabolic calculations (e.g. Mifflin-St Jeor).
/// Stored as a string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum BiologicalSex
{
    /// <summary>Biological male.</summary>
    Male,

    /// <summary>Biological female.</summary>
    Female,
}
