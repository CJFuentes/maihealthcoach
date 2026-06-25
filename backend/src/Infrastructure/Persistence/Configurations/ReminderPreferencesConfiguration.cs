using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ReminderPreferences"/> (issue #48). Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>. All
/// time-of-day values are mapped as <c>"HH:mm"</c> string columns (the EF model deliberately avoids
/// <c>TimeOnly</c> for SQLite test-harness compatibility).
/// </summary>
internal sealed class ReminderPreferencesConfiguration : IEntityTypeConfiguration<ReminderPreferences>
{
    public void Configure(EntityTypeBuilder<ReminderPreferences> builder)
    {
        builder.ToTable("ReminderPreferences");

        builder.HasKey(e => e.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.MealRemindersEnabled).IsRequired();
        builder.Property(e => e.WaterRemindersEnabled).IsRequired();

        // JSON array of "HH:mm" strings (at most five times); 256 chars is ample headroom.
        builder.Property(e => e.MealReminderTimesJson)
            .IsRequired()
            .HasMaxLength(256);

        // Single "HH:mm" values (nullable). 8 chars covers the 5-char value with headroom.
        builder.Property(e => e.WaterReminderTime)
            .HasMaxLength(8);

        builder.Property(e => e.QuietHoursStart)
            .HasMaxLength(8);

        builder.Property(e => e.QuietHoursEnd)
            .HasMaxLength(8);

        builder.Property(e => e.UtcOffsetMinutes).IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // One preferences row per user — unique on UserId. The upsert path relies on this to detect
        // the first-create race via the unique-index violation.
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("IX_ReminderPreferences_UserId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        // One-to-one with no navigation: there is no need to traverse from preferences to the user.
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<ReminderPreferences>(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ReminderPreferences_Users_UserId");
    }
}
