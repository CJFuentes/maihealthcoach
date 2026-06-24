using MAIHealthCoach.Domain.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="UserGoalTargets"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class UserGoalTargetsConfiguration : IEntityTypeConfiguration<UserGoalTargets>
{
    public void Configure(EntityTypeBuilder<UserGoalTargets> builder)
    {
        builder.ToTable("UserGoalTargets");

        builder.HasKey(t => t.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.UserId)
            .IsRequired();

        // One override row per user — enforced at the DB level.
        builder.HasIndex(t => t.UserId)
            .IsUnique()
            .HasDatabaseName("IX_UserGoalTargets_UserId");

        // FK to Users.Id with restrict on delete, consistent with the UserProfile mapping.
        builder.HasOne<Domain.Users.User>()
            .WithOne()
            .HasForeignKey<UserGoalTargets>(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserGoalTargets_Users_UserId");

        // All five override columns are nullable integers — null means "use computed value".
        builder.Property(t => t.CaloriesKcal);
        builder.Property(t => t.ProteinGrams);
        builder.Property(t => t.CarbohydrateGrams);
        builder.Property(t => t.FatGrams);
        builder.Property(t => t.WaterMl);

        builder.Property(t => t.LastOverriddenAt); // nullable DateTimeOffset

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt).IsRequired();
    }
}
