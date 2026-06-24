namespace MAIHealthCoach.Domain.Diary;

/// <summary>
/// The meal slot within a day for which a diary entry is recorded. Stored as a readable
/// string in the database (see <c>DiaryEntryConfiguration</c>).
/// </summary>
public enum MealType
{
    /// <summary>The first meal of the day.</summary>
    Breakfast,

    /// <summary>The midday meal.</summary>
    Lunch,

    /// <summary>The evening meal.</summary>
    Dinner,

    /// <summary>A between-meals snack.</summary>
    Snack,
}
