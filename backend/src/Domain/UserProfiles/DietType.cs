namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// Dietary pattern preference. <see cref="None"/> indicates no specific restriction.
/// Stored as a string in the database via <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public enum DietType
{
    /// <summary>No specific dietary restriction (omnivore).</summary>
    None,

    /// <summary>Plant-based with dairy/eggs; excludes meat and fish.</summary>
    Vegetarian,

    /// <summary>Fully plant-based; excludes all animal products.</summary>
    Vegan,

    /// <summary>Vegetarian plus fish and seafood; excludes other meat.</summary>
    Pescatarian,

    /// <summary>Very low-carbohydrate, high-fat diet.</summary>
    Keto,

    /// <summary>Whole-food diet modelled on presumed ancestral eating patterns.</summary>
    Paleo,
}
