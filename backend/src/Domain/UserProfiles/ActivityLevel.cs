namespace MAIHealthCoach.Domain.UserProfiles;

/// <summary>
/// Physical activity level used to estimate the user's total daily energy expenditure (TDEE).
/// Stored as a string in the database via <c>HasConversion&lt;string&gt;()</c> for readability.
/// </summary>
public enum ActivityLevel
{
    /// <summary>Little or no exercise; desk job.</summary>
    Sedentary,

    /// <summary>Light exercise or sports 1–3 days per week.</summary>
    LightlyActive,

    /// <summary>Moderate exercise or sports 3–5 days per week.</summary>
    ModeratelyActive,

    /// <summary>Hard exercise or sports 6–7 days per week.</summary>
    VeryActive,

    /// <summary>Very hard exercise, physical job, or twice-daily training.</summary>
    ExtraActive,
}
