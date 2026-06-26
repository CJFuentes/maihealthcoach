using System.Globalization;
using MAIHealthCoach.Application.Account;
using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Domain.UserProfiles;
using MAIHealthCoach.Domain.Water;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Api.Features.Me;

/// <summary>
/// Registers the authenticated account-management endpoints (issue #46):
/// <list type="bullet">
///   <item><description><c>GET    /me/data-export</c> — GDPR access/portability: a complete JSON export of the user's own data.</description></item>
///   <item><description><c>DELETE /me</c>            — GDPR erasure: permanently delete the user and all owned data.</description></item>
/// </list>
/// Both require authorization and act exclusively on the current user resolved via
/// <see cref="ICurrentUserService"/>; no other user's data is ever read or deleted.
/// </summary>
internal static class MeAccountEndpoints
{
    internal static RouteGroupBuilder MapMeAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me/data-export", ExportDataAsync)
            .WithName("ExportUserData")
            .RequireAuthorization();

        group.MapDelete("/me", DeleteAccountAsync)
            .WithName("DeleteAccount")
            .RequireAuthorization();

        return group;
    }

    // ── GET /api/v1/me/data-export ────────────────────────────────────────────────

    private static async Task<IResult> ExportDataAsync(
        HttpContext httpContext,
        ICurrentUserService currentUser,
        AppDbContext db,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        // Everything is read AsNoTracking and scoped to user.Id. The export is read-only.
        var profile = await db.UserProfiles
            .AsNoTracking()
            .Include(p => p.WeightMeasurements)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

        var goalTargets = await db.UserGoalTargets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == user.Id, ct);

        var water = await db.WaterLogEntries
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(ct);

        var diary = await db.DiaryEntries
            .AsNoTracking()
            .Include(e => e.FoodItem)
            .Include(e => e.ServingSize)
            .Where(e => e.UserId == user.Id)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        var customFoods = await db.FoodItems
            .AsNoTracking()
            .Include(f => f.ServingSizes)
            .Where(f => f.CreatedByUserId == user.Id)
            .ToListAsync(ct);

        var favorites = await db.UserFavoriteFoods
            .AsNoTracking()
            .Where(f => f.UserId == user.Id)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

        // Resolve favorite food names in one round-trip. A favorited food may have been removed
        // (e.g. cache eviction), so a missing id maps to a null name rather than failing the export.
        var favFoodIds = favorites.Select(f => f.FoodItemId).Distinct().ToList();
        var foodNames = await db.FoodItems
            .AsNoTracking()
            .Where(f => favFoodIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var exerciseLog = await db.ExerciseLogEntries
            .AsNoTracking()
            .Include(e => e.ExerciseActivity)
            .Where(e => e.UserId == user.Id)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);

        var customActivities = await db.ExerciseActivities
            .AsNoTracking()
            .Where(a => a.CreatedByUserId == user.Id)
            .ToListAsync(ct);

        var conversations = await db.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var messages = await db.CoachMessages
            .AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .OrderBy(m => m.Sequence)
            .ToListAsync(ct);

        var devices = await db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.UserId == user.Id)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);

        var reminderPrefs = await db.ReminderPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == user.Id, ct);

        // Group messages by conversation in memory (already ordered by Sequence above).
        var messagesByConversation = messages
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dto = new UserDataExportDto(
            ExportedAt: DateTimeOffset.UtcNow.ToString("O"),
            SchemaVersion: "1.0",
            User: MapUser(user),
            Profile: profile is null ? null : MapProfile(profile),
            WeightHistory: profile is null
                ? []
                : profile.WeightMeasurements
                    .OrderBy(w => w.MeasuredAt)
                    .Select(MapWeight)
                    .ToList(),
            GoalOverrides: goalTargets is null ? null : MapGoalTargets(goalTargets),
            WaterLog: water.Select(MapWater).ToList(),
            FoodDiary: diary.Select(MapDiary).ToList(),
            CustomFoods: customFoods.Select(MapFood).ToList(),
            FavoriteFoods: favorites
                .Select(f => MapFavorite(f, foodNames.GetValueOrDefault(f.FoodItemId)))
                .ToList(),
            ExerciseLog: exerciseLog.Select(MapExerciseLog).ToList(),
            CustomExerciseActivities: customActivities.Select(MapActivity).ToList(),
            CoachConversations: conversations
                .Select(c => MapConversation(c, messagesByConversation.GetValueOrDefault(c.Id) ?? []))
                .ToList(),
            Devices: devices.Select(MapDevice).ToList(),
            ReminderPreferences: reminderPrefs is null ? null : MapReminderPreferences(reminderPrefs));

        // Offer the export as a downloadable file. The Content-Disposition is informational; the
        // body is still JSON so API clients can consume it directly.
        var filename = $"mai-health-export-{user.Id}-{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}.json";
        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{filename}\"";

        return Results.Ok(dto);
    }

    // ── DELETE /api/v1/me ─────────────────────────────────────────────────────────

    private static async Task<IResult> DeleteAccountAsync(
        ICurrentUserService currentUser,
        IAccountDeletionService deletionService,
        CancellationToken ct)
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);

        await deletionService.DeleteAccountAsync(user.Id, ct);

        return Results.NoContent();
    }

    // ── Mappers ───────────────────────────────────────────────────────────────────
    // Small per-entity statics keep the handler readable; enums map via .ToString(),
    // DateTimeOffset via "O" (round-trip UTC), and DateOnly via "yyyy-MM-dd".

    private static UserExportDto MapUser(Domain.Users.User user) =>
        new(
            Id: user.Id,
            ClerkUserId: user.ClerkUserId,
            Email: user.Email,
            CreatedAt: user.CreatedAt.ToString("O"),
            UpdatedAt: user.UpdatedAt.ToString("O"));

    private static UserProfileExportDto MapProfile(UserProfile profile) =>
        new(
            Id: profile.Id,
            HeightCm: profile.HeightCm,
            DateOfBirth: profile.DateOfBirth?.ToString("yyyy-MM-dd"),
            BiologicalSex: profile.BiologicalSex?.ToString(),
            ActivityLevel: profile.ActivityLevel?.ToString(),
            PrimaryGoal: profile.PrimaryGoal?.ToString(),
            Units: profile.Units.ToString(),
            DietType: profile.DietaryPreferences?.DietType?.ToString(),
            Allergies: profile.DietaryPreferences?.Allergies ?? string.Empty,
            LatestWeightKg: profile.LatestWeightKg,
            CreatedAt: profile.CreatedAt.ToString("O"),
            UpdatedAt: profile.UpdatedAt.ToString("O"));

    private static WeightMeasurementExportDto MapWeight(WeightMeasurement w) =>
        new(
            Id: w.Id,
            WeightKg: w.WeightKg,
            MeasuredAt: w.MeasuredAt.ToString("O"),
            CreatedAt: w.CreatedAt.ToString("O"));

    private static UserGoalTargetsExportDto MapGoalTargets(UserGoalTargets t) =>
        new(
            CaloriesKcal: t.CaloriesKcal,
            ProteinGrams: t.ProteinGrams,
            CarbohydrateGrams: t.CarbohydrateGrams,
            FatGrams: t.FatGrams,
            WaterMl: t.WaterMl,
            LastOverriddenAt: t.LastOverriddenAt?.ToString("O"));

    private static WaterLogEntryExportDto MapWater(WaterLogEntry e) =>
        new(
            Id: e.Id,
            AmountMl: e.AmountMl,
            Date: e.Date.ToString("yyyy-MM-dd"),
            CreatedAt: e.CreatedAt.ToString("O"));

    private static DiaryEntryExportDto MapDiary(DiaryEntry e) =>
        new(
            Id: e.Id,
            FoodItemId: e.FoodItemId,
            FoodName: e.FoodItem.Name,
            ServingSizeId: e.ServingSizeId,
            ServingLabel: e.ServingSize.Label,
            Quantity: e.Quantity,
            MealType: e.MealType.ToString(),
            Date: e.Date.ToString("yyyy-MM-dd"),
            CreatedAt: e.CreatedAt.ToString("O"));

    private static FoodItemExportDto MapFood(FoodItem food) =>
        new(
            Id: food.Id,
            Name: food.Name,
            Brand: food.Brand,
            Source: food.Source.ToString(),
            NutritionPer100g: MapNutrition(food.NutritionPer100g),
            ServingSizes: food.ServingSizes
                .OrderByDescending(s => s.IsDefault)
                .ThenBy(s => s.GramsEquivalent)
                .ThenBy(s => s.Label, StringComparer.Ordinal)
                .Select(MapServing)
                .ToList(),
            CreatedAt: food.CreatedAt.ToString("O"));

    private static NutritionFactsExportDto MapNutrition(NutritionFacts n) =>
        new(
            EnergyKcal: n.EnergyKcal,
            ProteinG: n.ProteinG,
            CarbohydrateG: n.CarbohydrateG,
            FatG: n.FatG,
            SugarsG: n.SugarsG,
            FiberG: n.FiberG,
            SaturatedFatG: n.SaturatedFatG,
            SodiumMg: n.SodiumMg);

    private static ServingSizeExportDto MapServing(ServingSize s) =>
        new(
            Id: s.Id,
            Label: s.Label,
            Quantity: s.Quantity,
            Unit: s.Unit,
            GramsEquivalent: s.GramsEquivalent,
            IsDefault: s.IsDefault);

    private static UserFavoriteFoodExportDto MapFavorite(UserFavoriteFood f, string? foodName) =>
        new(
            Id: f.Id,
            FoodItemId: f.FoodItemId,
            FoodName: foodName,
            CreatedAt: f.CreatedAt.ToString("O"));

    private static ExerciseLogEntryExportDto MapExerciseLog(ExerciseLogEntry e) =>
        new(
            Id: e.Id,
            ExerciseActivityId: e.ExerciseActivityId,
            ActivityName: e.ExerciseActivity.Name,
            ActivityCategory: e.ExerciseActivity.Category.ToString(),
            DurationMinutes: e.DurationMinutes,
            CaloriesBurned: e.CaloriesBurned,
            Date: e.Date.ToString("yyyy-MM-dd"),
            CreatedAt: e.CreatedAt.ToString("O"));

    private static ExerciseActivityExportDto MapActivity(ExerciseActivity a) =>
        new(
            Id: a.Id,
            Name: a.Name,
            Category: a.Category.ToString(),
            MetValue: a.MetValue,
            CreatedAt: a.CreatedAt.ToString("O"));

    private static ConversationExportDto MapConversation(Conversation c, IReadOnlyList<Message> messages) =>
        new(
            Id: c.Id,
            Title: c.Title,
            MessageCount: c.MessageCount,
            CreatedAt: c.CreatedAt.ToString("O"),
            UpdatedAt: c.UpdatedAt.ToString("O"),
            Messages: messages.Select(MapMessage).ToList());

    private static MessageExportDto MapMessage(Message m) =>
        new(
            Id: m.Id,
            Role: m.Role.ToString(),
            Content: m.Content,
            Sequence: m.Sequence,
            CreatedAt: m.CreatedAt.ToString("O"));

    // The raw push Token is deliberately excluded — it is a delivery credential, not user content.
    private static DeviceRegistrationExportDto MapDevice(DeviceRegistration d) =>
        new(
            Id: d.Id,
            Platform: d.Platform.ToString(),
            Name: d.Name,
            LastSeenAt: d.LastSeenAt.ToString("O"),
            CreatedAt: d.CreatedAt.ToString("O"));

    private static ReminderPreferencesExportDto MapReminderPreferences(ReminderPreferences r) =>
        new(
            MealRemindersEnabled: r.MealRemindersEnabled,
            WaterRemindersEnabled: r.WaterRemindersEnabled,
            MealReminderTimes: r.GetMealReminderTimes()
                .Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))
                .ToList(),
            WaterReminderTime: r.WaterReminderTime,
            QuietHoursStart: r.QuietHoursStart,
            QuietHoursEnd: r.QuietHoursEnd,
            UtcOffsetMinutes: r.UtcOffsetMinutes,
            CreatedAt: r.CreatedAt.ToString("O"),
            UpdatedAt: r.UpdatedAt.ToString("O"));
}
