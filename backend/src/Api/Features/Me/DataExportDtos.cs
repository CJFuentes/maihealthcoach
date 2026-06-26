namespace MAIHealthCoach.Api.Features.Me;

/// <summary>
/// The complete, machine-readable export of a single authenticated user's personal data (issue #46)
/// — the GDPR "right of access" / "right to data portability" payload returned by
/// <c>GET /api/v1/me/data-export</c>. Every collection and value is scoped to the requesting user;
/// the document contains no other user's data and no secrets (no auth tokens, no API keys).
/// </summary>
/// <param name="ExportedAt">UTC instant the export was produced, ISO-8601 round-trip ("O") format.</param>
/// <param name="SchemaVersion">Export schema version, so consumers can detect format changes.</param>
/// <param name="User">The user's account identity.</param>
/// <param name="Profile">The user's health profile, or <see langword="null"/> if none exists.</param>
/// <param name="WeightHistory">The user's recorded body-weight measurements.</param>
/// <param name="GoalOverrides">Manual goal-target overrides, or <see langword="null"/> if none set.</param>
/// <param name="WaterLog">The user's water-intake log entries.</param>
/// <param name="FoodDiary">The user's food diary entries.</param>
/// <param name="CustomFoods">Foods the user created, with their nutrition and serving sizes.</param>
/// <param name="FavoriteFoods">The user's favorite-food markers.</param>
/// <param name="ExerciseLog">The user's exercise-session log entries.</param>
/// <param name="CustomExerciseActivities">Exercise activities the user created.</param>
/// <param name="CoachConversations">The user's coach conversations and their messages.</param>
/// <param name="Devices">The user's registered push-notification devices.</param>
/// <param name="ReminderPreferences">The user's push-reminder preferences, or <see langword="null"/> if none set.</param>
public sealed record UserDataExportDto(
    string ExportedAt,
    string SchemaVersion,
    UserExportDto User,
    UserProfileExportDto? Profile,
    IReadOnlyList<WeightMeasurementExportDto> WeightHistory,
    UserGoalTargetsExportDto? GoalOverrides,
    IReadOnlyList<WaterLogEntryExportDto> WaterLog,
    IReadOnlyList<DiaryEntryExportDto> FoodDiary,
    IReadOnlyList<FoodItemExportDto> CustomFoods,
    IReadOnlyList<UserFavoriteFoodExportDto> FavoriteFoods,
    IReadOnlyList<ExerciseLogEntryExportDto> ExerciseLog,
    IReadOnlyList<ExerciseActivityExportDto> CustomExerciseActivities,
    IReadOnlyList<ConversationExportDto> CoachConversations,
    IReadOnlyList<DeviceRegistrationExportDto> Devices,
    ReminderPreferencesExportDto? ReminderPreferences);

/// <summary>The user's account identity. <c>CreatedAt</c>/<c>UpdatedAt</c> are ISO-8601 ("O") strings.</summary>
public sealed record UserExportDto(
    Guid Id,
    string ClerkUserId,
    string Email,
    string CreatedAt,
    string UpdatedAt);

/// <summary>
/// The user's health profile. <c>DateOfBirth</c> is a <c>yyyy-MM-dd</c> string; enum-derived fields
/// are their string names; <c>DietType</c> and <c>Allergies</c> come from the owned
/// <c>DietaryPreferences</c> value (null/empty when never supplied).
/// </summary>
public sealed record UserProfileExportDto(
    Guid Id,
    double? HeightCm,
    string? DateOfBirth,
    string? BiologicalSex,
    string? ActivityLevel,
    string? PrimaryGoal,
    string Units,
    string? DietType,
    string Allergies,
    double? LatestWeightKg,
    string CreatedAt,
    string UpdatedAt);

/// <summary>A single body-weight measurement. <c>MeasuredAt</c>/<c>CreatedAt</c> are ISO-8601 ("O") strings.</summary>
public sealed record WeightMeasurementExportDto(
    Guid Id,
    double WeightKg,
    string MeasuredAt,
    string CreatedAt);

/// <summary>Manual goal-target overrides. <c>LastOverriddenAt</c> is an ISO-8601 ("O") string or null.</summary>
public sealed record UserGoalTargetsExportDto(
    int? CaloriesKcal,
    int? ProteinGrams,
    int? CarbohydrateGrams,
    int? FatGrams,
    int? WaterMl,
    string? LastOverriddenAt);

/// <summary>A single water-intake log entry. <c>Date</c> is <c>yyyy-MM-dd</c>; <c>CreatedAt</c> is ISO-8601 ("O").</summary>
public sealed record WaterLogEntryExportDto(
    Guid Id,
    int AmountMl,
    string Date,
    string CreatedAt);

/// <summary>
/// A single food diary entry, with the logged food's and serving's denormalized names for a
/// self-contained export. <c>Date</c> is <c>yyyy-MM-dd</c>; <c>CreatedAt</c> is ISO-8601 ("O").
/// </summary>
public sealed record DiaryEntryExportDto(
    Guid Id,
    Guid FoodItemId,
    string FoodName,
    Guid ServingSizeId,
    string ServingLabel,
    decimal Quantity,
    string MealType,
    string Date,
    string CreatedAt);

/// <summary>A user-created custom food, with its per-100 g nutrition and serving sizes.</summary>
public sealed record FoodItemExportDto(
    Guid Id,
    string Name,
    string? Brand,
    string Source,
    NutritionFactsExportDto NutritionPer100g,
    IReadOnlyList<ServingSizeExportDto> ServingSizes,
    string CreatedAt);

/// <summary>Per-100 g nutrition facts of a food. Micros are null when the source did not provide them.</summary>
public sealed record NutritionFactsExportDto(
    decimal EnergyKcal,
    decimal ProteinG,
    decimal CarbohydrateG,
    decimal FatG,
    decimal? SugarsG,
    decimal? FiberG,
    decimal? SaturatedFatG,
    decimal? SodiumMg);

/// <summary>A serving-size portion of a food.</summary>
public sealed record ServingSizeExportDto(
    Guid Id,
    string Label,
    decimal Quantity,
    string Unit,
    decimal GramsEquivalent,
    bool IsDefault);

/// <summary>A favorite-food marker. <c>FoodName</c> is null if the referenced food row no longer exists.</summary>
public sealed record UserFavoriteFoodExportDto(
    Guid Id,
    Guid FoodItemId,
    string? FoodName,
    string CreatedAt);

/// <summary>
/// A single exercise-session log entry, with the activity's denormalized name and category.
/// <c>Date</c> is <c>yyyy-MM-dd</c>; <c>CreatedAt</c> is ISO-8601 ("O").
/// </summary>
public sealed record ExerciseLogEntryExportDto(
    Guid Id,
    Guid ExerciseActivityId,
    string ActivityName,
    string ActivityCategory,
    int DurationMinutes,
    int CaloriesBurned,
    string Date,
    string CreatedAt);

/// <summary>A user-created custom exercise activity. <c>Category</c> is the enum's string name.</summary>
public sealed record ExerciseActivityExportDto(
    Guid Id,
    string Name,
    string Category,
    decimal MetValue,
    string CreatedAt);

/// <summary>A coach conversation and its ordered messages.</summary>
public sealed record ConversationExportDto(
    Guid Id,
    string? Title,
    int MessageCount,
    string CreatedAt,
    string UpdatedAt,
    IReadOnlyList<MessageExportDto> Messages);

/// <summary>A single coach message. <c>Role</c> is the enum's string name; <c>Sequence</c> orders the thread.</summary>
public sealed record MessageExportDto(
    Guid Id,
    string Role,
    string Content,
    int Sequence,
    string CreatedAt);

/// <summary>
/// A registered push-notification device. <c>Platform</c> is the enum's string name;
/// <c>LastSeenAt</c>/<c>CreatedAt</c> are ISO-8601 ("O") strings. The raw push <c>Token</c> is
/// intentionally omitted — it is a delivery credential, not user content.
/// </summary>
public sealed record DeviceRegistrationExportDto(
    Guid Id,
    string Platform,
    string? Name,
    string LastSeenAt,
    string CreatedAt);

/// <summary>
/// The user's push-reminder preferences. Time-of-day values are <c>"HH:mm"</c> strings (or null);
/// <c>MealReminderTimes</c> is the parsed list of meal-reminder times.
/// </summary>
public sealed record ReminderPreferencesExportDto(
    bool MealRemindersEnabled,
    bool WaterRemindersEnabled,
    IReadOnlyList<string> MealReminderTimes,
    string? WaterReminderTime,
    string? QuietHoursStart,
    string? QuietHoursEnd,
    int UtcOffsetMinutes,
    string CreatedAt,
    string UpdatedAt);
