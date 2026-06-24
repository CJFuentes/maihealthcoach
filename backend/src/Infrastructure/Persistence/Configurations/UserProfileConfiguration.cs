using MAIHealthCoach.Domain.UserProfiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="UserProfile"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");

        builder.HasKey(p => p.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.UserId)
            .IsRequired();

        // One profile per user — enforced at the DB level.
        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("IX_UserProfiles_UserId");

        // FK to Users.Id with restrict on delete (orphaned profiles should be
        // handled by soft-delete or explicit user deletion logic, not cascade).
        builder.HasOne<Domain.Users.User>()
            .WithOne()
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserProfiles_Users_UserId");

        builder.Property(p => p.HeightCm);    // nullable double, no extra config needed

        builder.Property(p => p.DateOfBirth); // nullable DateOnly — EF10 supports DateOnly natively

        // Nullable enums stored as strings for readability and forward-compatibility.
        builder.Property(p => p.BiologicalSex)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(p => p.ActivityLevel)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(p => p.PrimaryGoal)
            .HasConversion<string>()
            .HasMaxLength(32);

        // Units is non-nullable with a default of Metric. The value is always set by the
        // client (the entity initializes it to Metric), so it must be ValueGeneratedNever
        // — otherwise EF infers ValueGeneratedOnAdd from HasDefaultValue and adds a needless
        // RETURNING round-trip, inconsistent with the rest of this configuration.
        builder.Property(p => p.Units)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(Domain.UserProfiles.UnitsPreference.Metric)
            .ValueGeneratedNever();

        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // DietaryPreferences is an owned type (value object). All its columns land on
        // UserProfiles with explicit column names. The owned navigation is OPTIONAL (the
        // profile may have no DietaryPreferences) — EF signals an absent owned value via
        // the nullable DietType column. When every owned column is NULL, EF materializes the
        // owned nav as null.
        builder.OwnsOne(p => p.DietaryPreferences, dp =>
        {
            // DietType stays nullable so EF can represent an absent DietaryPreferences
            // (all-null owned columns => owned nav is null).
            dp.Property(d => d.DietType)
                .HasColumnName("DietaryPreferences_DietType")
                .HasConversion<string>()
                .HasMaxLength(32);

            // Allergies is a non-nullable C# string (defaults to string.Empty). The DB
            // column must be non-nullable too, otherwise EF could materialize NULL into the
            // non-nullable property (NRE risk). Absence of the owned value is already carried
            // by the nullable DietType column, so Allergies does not need to be nullable.
            dp.Property(d => d.Allergies)
                .HasColumnName("DietaryPreferences_Allergies")
                .HasMaxLength(1024)
                .IsRequired()
                .HasDefaultValue(string.Empty)
                .ValueGeneratedNever();
        });
    }
}
