using MAIHealthCoach.Domain.UserProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="WeightMeasurement"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c>.
/// </summary>
internal sealed class WeightMeasurementConfiguration : IEntityTypeConfiguration<WeightMeasurement>
{
    public void Configure(EntityTypeBuilder<WeightMeasurement> builder)
    {
        builder.ToTable("WeightMeasurements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.UserProfileId)
            .IsRequired();

        builder.Property(m => m.WeightKg)
            .IsRequired();

        builder.Property(m => m.MeasuredAt)
            .IsRequired();

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        // Composite index: retrieves all measurements for a profile ordered by date
        // efficiently. The index supports both "latest weight" lookups and the 90-row
        // history query (.OrderByDescending(m => m.MeasuredAt).Take(90)).
        builder.HasIndex(m => new { m.UserProfileId, m.MeasuredAt })
            .HasDatabaseName("IX_WeightMeasurements_UserProfileId_MeasuredAt");

        // The collection is owned by UserProfile; cascade delete removes measurements
        // when the profile is deleted. Bind the inverse to UserProfile's
        // WeightMeasurements navigation so EF treats this as a single relationship keyed
        // on UserProfileId — otherwise it would synthesize a second shadow FK.
        builder.HasOne<UserProfile>()
            .WithMany(nameof(UserProfile.WeightMeasurements))
            .HasForeignKey(m => m.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_WeightMeasurements_UserProfiles_UserProfileId");
    }
}
