using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ExerciseLogEntry"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class ExerciseLogEntryConfiguration : IEntityTypeConfiguration<ExerciseLogEntry>
{
    public void Configure(EntityTypeBuilder<ExerciseLogEntry> builder)
    {
        builder.ToTable("ExerciseLogEntries");

        builder.HasKey(e => e.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.ExerciseActivityId)
            .IsRequired();

        // Whole minutes. int maps to a native integer column; no precision needed.
        builder.Property(e => e.DurationMinutes)
            .IsRequired();

        // Snapshotted kcal estimate. int maps to a native integer column; no precision needed.
        builder.Property(e => e.CaloriesBurned)
            .IsRequired();

        // DateOnly maps to a native date column (EF Core 10 + Npgsql support DateOnly natively;
        // SQLite-backed tests store it as ISO text). Mirrors DiaryEntry.Date.
        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Primary access pattern: total + list all of a user's entries for one date. Composite
        // index covers the `WHERE UserId = ? AND Date = ?` predicate driving GET /me/exercise.
        builder.HasIndex(e => new { e.UserId, e.Date })
            .HasDatabaseName("IX_ExerciseLogEntries_UserId_Date");

        // Secondary index for user-scoped single-entry lookups (PUT/DELETE) where Date is not
        // part of the predicate.
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_ExerciseLogEntries_UserId");

        // Explicitly declare and name the FK-backing index on ExerciseActivityId. EF Core's
        // convention would create this index automatically for the navigation below, but declaring
        // it here gives EF ownership of the name (matching the project convention of naming every
        // index) so a future model change cannot trigger a spurious index-rename migration.
        builder.HasIndex(e => e.ExerciseActivityId)
            .HasDatabaseName("IX_ExerciseLogEntries_ExerciseActivityId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        // No navigation: there is no need to traverse from an entry back to its user.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ExerciseLogEntries_Users_UserId");

        // FK to ExerciseActivities: Restrict — the catalog activity must outlive any log entry that
        // references it. The navigation is mapped here so read queries can Include the activity to
        // expose its name/category in the day response. The FK-backing index is declared explicitly
        // above so EF owns its name.
        builder.HasOne(e => e.ExerciseActivity)
            .WithMany()
            .HasForeignKey(e => e.ExerciseActivityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ExerciseLogEntries_ExerciseActivities_ExerciseActivityId");
    }
}
