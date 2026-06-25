using MAIHealthCoach.Domain.Exercise;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ExerciseActivity"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// Includes catalog seed data for common activities with standard MET values from the
/// 2011 Compendium of Physical Activities (Ainsworth et al.).
/// </summary>
internal sealed class ExerciseActivityConfiguration : IEntityTypeConfiguration<ExerciseActivity>
{
    // Fixed seed timestamp: HasData requires fully deterministic values. Using a compile-time
    // constant instant (never DateTimeOffset.UtcNow) keeps the scaffolded migration stable so
    // re-running `migrations add` never produces a spurious data churn.
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<ExerciseActivity> builder)
    {
        builder.ToTable("ExerciseActivities");

        builder.HasKey(e => e.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(256);

        // Category stored as a readable string, consistent with other enums in the model.
        // 32 chars leaves headroom for future ExerciseCategory members.
        builder.Property(e => e.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        // MET values range from ~0.9 (sleeping) to ~23 (sprinting). precision(4,2) gives a max
        // value of 99.99 with two decimal places — sufficient for all published MET values and
        // user-entered custom activities. The API validator caps input at 99.99 to match.
        builder.Property(e => e.MetValue)
            .IsRequired()
            .HasPrecision(4, 2);

        // Nullable owner FK: null for seeded shared activities; set for custom user activities.
        builder.Property(e => e.CreatedByUserId);

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Name search index: supports the optional ?q= text search on GET /exercises.
        builder.HasIndex(e => e.Name)
            .HasDatabaseName("IX_ExerciseActivities_Name");

        // Category filter index: supports the optional ?category= filter on GET /exercises.
        builder.HasIndex(e => e.Category)
            .HasDatabaseName("IX_ExerciseActivities_Category");

        // Owner index: supports "list my custom activities" (WHERE CreatedByUserId = ?). Most
        // rows are seeded shared activities with a null owner.
        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("IX_ExerciseActivities_CreatedByUserId");

        // FK to Users for the custom-activity owner. The FK column is nullable, so the
        // relationship is optional (seeded rows have a null owner). Restrict mirrors every other
        // config in this codebase: users are never hard-deleted in v1, so this guards orphans.
        // No navigation: there is no need to traverse from an activity back to its owner.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ExerciseActivities_Users_CreatedByUserId");

        // ── Catalog seed data (Ainsworth et al. 2011 Compendium of Physical Activities) ──
        // Fixed Guid literals are hardcoded — never Guid.CreateVersion7() or any runtime call —
        // because HasData requires fully deterministic seed objects. The anonymous objects
        // include every mapped column; CreatedByUserId is explicitly (Guid?)null on every row so
        // the column nullability is unambiguous. All seeded rows are shared (null owner).
        builder.HasData(
            // ── Cardio ──────────────────────────────────────────────────────────────────────
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000001"),
                Name = "Walking (3.5 mph / moderate pace)",
                Category = ExerciseCategory.Cardio,
                MetValue = 4.3m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000002"),
                Name = "Running (6 mph / 10 min per mile)",
                Category = ExerciseCategory.Cardio,
                MetValue = 9.8m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000003"),
                Name = "Running (8 mph / 7.5 min per mile)",
                Category = ExerciseCategory.Cardio,
                MetValue = 13.5m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000004"),
                Name = "Cycling (moderate, 12-14 mph)",
                Category = ExerciseCategory.Cardio,
                MetValue = 8.0m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000005"),
                Name = "Swimming (freestyle, moderate effort)",
                Category = ExerciseCategory.Cardio,
                MetValue = 5.8m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000006"),
                Name = "Rowing (moderate effort)",
                Category = ExerciseCategory.Cardio,
                MetValue = 7.0m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000007"),
                Name = "Jump Rope (moderate pace)",
                Category = ExerciseCategory.Cardio,
                MetValue = 11.8m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000008"),
                Name = "Elliptical Trainer (moderate effort)",
                Category = ExerciseCategory.Cardio,
                MetValue = 5.0m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            // ── Strength ────────────────────────────────────────────────────────────────────
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000009"),
                Name = "Weightlifting (general)",
                Category = ExerciseCategory.Strength,
                MetValue = 3.5m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-00000000000a"),
                Name = "Weightlifting (vigorous effort)",
                Category = ExerciseCategory.Strength,
                MetValue = 6.0m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-00000000000b"),
                Name = "Bodyweight Exercises (push-ups, pull-ups)",
                Category = ExerciseCategory.Strength,
                MetValue = 3.8m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            // ── Flexibility ─────────────────────────────────────────────────────────────────
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-00000000000c"),
                Name = "Yoga (Hatha)",
                Category = ExerciseCategory.Flexibility,
                MetValue = 2.5m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-00000000000d"),
                Name = "Yoga (Power / Vinyasa)",
                Category = ExerciseCategory.Flexibility,
                MetValue = 4.0m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-00000000000e"),
                Name = "Pilates (general)",
                Category = ExerciseCategory.Flexibility,
                MetValue = 3.0m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            // ── Sports ──────────────────────────────────────────────────────────────────────
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-00000000000f"),
                Name = "Basketball (general game)",
                Category = ExerciseCategory.Sports,
                MetValue = 6.5m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000010"),
                Name = "Tennis (singles)",
                Category = ExerciseCategory.Sports,
                MetValue = 7.3m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            },
            // ── Other ───────────────────────────────────────────────────────────────────────
            new
            {
                Id = new Guid("01975a00-0001-7000-8000-000000000011"),
                Name = "Dancing (general)",
                Category = ExerciseCategory.Other,
                MetValue = 4.8m,
                CreatedByUserId = (Guid?)null,
                CreatedAt = SeedTimestamp,
                UpdatedAt = SeedTimestamp,
            });
    }
}
