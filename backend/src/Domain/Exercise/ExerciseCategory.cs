namespace MAIHealthCoach.Domain.Exercise;

/// <summary>
/// Broad activity category for an <see cref="ExerciseActivity"/>. Stored as a readable
/// string in the database via <c>HasConversion&lt;string&gt;()</c> so new categories can be
/// added without a numeric remap (forward-compatible with future exercise features such as
/// the exercise log in issue #34 and the UI in issue #35).
/// </summary>
public enum ExerciseCategory
{
    /// <summary>
    /// Rhythmic, aerobic activities that primarily elevate heart rate and cardiovascular
    /// output (e.g. walking, running, cycling, swimming, rowing).
    /// </summary>
    Cardio,

    /// <summary>
    /// Resistance and weight-bearing exercises that primarily target muscular strength and
    /// hypertrophy (e.g. weightlifting, bodyweight training, resistance machines).
    /// </summary>
    Strength,

    /// <summary>
    /// Stretching, mobility, and mind-body practices that emphasize range of motion and
    /// relaxation (e.g. yoga, pilates, static stretching).
    /// </summary>
    Flexibility,

    /// <summary>
    /// Competitive or recreational sports activities not covered by the other categories
    /// (e.g. basketball, tennis, soccer, volleyball).
    /// </summary>
    Sports,

    /// <summary>
    /// Activities that do not fit neatly into the other categories (e.g. housework,
    /// gardening, dancing, martial arts).
    /// </summary>
    Other,
}
