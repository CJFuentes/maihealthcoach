using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Goals;
using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Domain.UserProfiles;
using MAIHealthCoach.Domain.Users;
using MAIHealthCoach.Domain.Water;
using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Infrastructure.Persistence;

/// <summary>
/// The application's single EF Core <see cref="DbContext"/> and unit-of-work.
/// Inject it directly into services that need data access; the change tracker
/// provides the unit-of-work boundary and each <see cref="DbSet{TEntity}"/> acts
/// as the repository for its aggregate. Entity sets are added per milestone; mappings
/// live in <c>Persistence.Configurations</c> and are picked up by
/// <c>ApplyConfigurationsFromAssembly</c> below.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>Local users provisioned from Clerk identities (issue #12).</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>User health and preference profiles (issue #16).</summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>Body weight measurement history (issue #16).</summary>
    public DbSet<WeightMeasurement> WeightMeasurements => Set<WeightMeasurement>();

    /// <summary>Manual goal-target overrides per user (issue #17).</summary>
    public DbSet<UserGoalTargets> UserGoalTargets => Set<UserGoalTargets>();

    /// <summary>Foods with nutrition data, from Open Food Facts or user-created (issue #19).</summary>
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();

    /// <summary>Serving-size portions belonging to a <see cref="FoodItem"/> (issue #19).</summary>
    public DbSet<ServingSize> ServingSizes => Set<ServingSize>();

    /// <summary>Food diary log entries, one per logged food per meal per day (issue #22).</summary>
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();

    /// <summary>User-to-food "favorite" markers for quick re-logging (issue #24).</summary>
    public DbSet<UserFavoriteFood> UserFavoriteFoods => Set<UserFavoriteFood>();

    /// <summary>MAI coach chat conversations, one per chat thread (issue #39).</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Messages within coach chat conversations, user and assistant turns (issue #39).</summary>
    public DbSet<Message> CoachMessages => Set<Message>();

    /// <summary>Water intake log entries, one per logged amount per day (issue #31).</summary>
    public DbSet<WaterLogEntry> WaterLogEntries => Set<WaterLogEntry>();

    /// <summary>Exercise activity catalog: seeded shared activities and user-created custom activities (issue #33).</summary>
    public DbSet<ExerciseActivity> ExerciseActivities => Set<ExerciseActivity>();

    /// <summary>Exercise log entries, one per logged activity per day, with a snapshotted calories-burned estimate (issue #34).</summary>
    public DbSet<ExerciseLogEntry> ExerciseLogEntries => Set<ExerciseLogEntry>();

    /// <summary>Push-notification device registrations, one per device token (issue #48).</summary>
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();

    /// <summary>Per-user push-reminder preferences, one row per user (issue #48).</summary>
    public DbSet<ReminderPreferences> ReminderPreferences => Set<ReminderPreferences>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
