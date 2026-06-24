namespace MAIHealthCoach.Domain.Food;

/// <summary>
/// Provenance of a <see cref="FoodItem"/> — where its data originated. Stored as a string
/// in the database via <c>HasConversion&lt;string&gt;()</c> so new sources can be added
/// without a numeric remap (forward-compatible with the Open Food Facts integration in
/// issue #20 and future external catalogues in #24).
/// </summary>
public enum FoodSource
{
    /// <summary>Imported from the Open Food Facts public database (issue #20).</summary>
    OpenFoodFacts,

    /// <summary>Created by a user within the application (no external catalogue origin).</summary>
    Custom,
}
