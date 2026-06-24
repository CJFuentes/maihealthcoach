namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// Owned value object capturing dietary pattern and allergy information.
/// Persisted as columns on the <c>UserProfiles</c> table via EF Core <c>OwnsOne</c>.
/// </summary>
public sealed class DietaryPreferences
{
    /// <summary>
    /// The user's primary dietary pattern. <see langword="null"/> when not yet set.
    /// </summary>
    public DietType? DietType { get; private set; }

    /// <summary>
    /// Free-text description of known food allergies or intolerances (e.g. "peanuts, gluten").
    /// Empty string when none are recorded; never <see langword="null"/>.
    /// </summary>
    public string Allergies { get; private set; } = string.Empty;

    /// <summary>Parameterless constructor reserved for EF Core materialization.</summary>
    private DietaryPreferences() { }

    /// <summary>
    /// Creates a <see cref="DietaryPreferences"/> value with the given diet type and allergies.
    /// </summary>
    /// <param name="dietType">Dietary pattern, or <see langword="null"/> to leave unset.</param>
    /// <param name="allergies">Allergy text. Pass <see cref="string.Empty"/> for none.</param>
    public static DietaryPreferences Create(DietType? dietType, string allergies)
    {
        ArgumentNullException.ThrowIfNull(allergies);
        return new DietaryPreferences { DietType = dietType, Allergies = allergies };
    }
}
