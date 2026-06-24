namespace MAIHealthCoach.Application.Nudges;

/// <summary>
/// The input to <see cref="INudgeService"/>: optional recent-activity signals plus optional
/// profile/goal context. Every field is nullable so the service produces a sensible nudge even when
/// nothing is known about the user.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CurrentStreakDays"/> and <see cref="TodayAdherencePercent"/> are the integration seam
/// for the streaks/adherence tracking work (issues #44/#42), which is not yet available. They are
/// intentionally <em>optional</em>: when present, the nudge is tailored to them (celebrate a streak,
/// gently re-motivate after a low-adherence day); when absent, the service asks for general
/// encouragement aligned to the user's goal. Callers from #44/#42 can later pass real values here
/// without any change to this contract.
/// </para>
/// <para>
/// The profile/goal fields are likewise optional. <see cref="HasProfile"/> distinguishes a user with
/// a complete profile (context-rich nudge) from one without (generic encouraging nudge). When
/// <see cref="HasProfile"/> is <see langword="false"/>, the profile/goal fields are ignored.
/// </para>
/// </remarks>
/// <param name="CurrentStreakDays">
/// The user's current adherence streak in whole days, or <see langword="null"/> when unknown.
/// Future integration seam for issue #44.
/// </param>
/// <param name="TodayAdherencePercent">
/// Today's adherence as a percentage (0–100), or <see langword="null"/> when unknown. Future
/// integration seam for issue #42/#44.
/// </param>
/// <param name="HasProfile">
/// <see langword="true"/> when the profile/goal context fields are populated and should inform the
/// nudge; <see langword="false"/> for a generic, profile-free nudge.
/// </param>
/// <param name="PrimaryGoal">
/// The user's stated primary goal (e.g. "Lose weight"), or <see langword="null"/> when unknown.
/// </param>
/// <param name="DailyCalorieTarget">The effective daily calorie target in kcal, or <see langword="null"/>.</param>
/// <param name="DailyProteinTargetGrams">The effective daily protein target in grams, or <see langword="null"/>.</param>
/// <param name="ActivityLevel">The user's activity level description, or <see langword="null"/>.</param>
/// <param name="DietaryPreferences">Free-text dietary preferences and restrictions, or <see langword="null"/>.</param>
public sealed record NudgeRequest(
    int? CurrentStreakDays,
    decimal? TodayAdherencePercent,
    bool HasProfile,
    string? PrimaryGoal = null,
    int? DailyCalorieTarget = null,
    int? DailyProteinTargetGrams = null,
    string? ActivityLevel = null,
    string? DietaryPreferences = null);
