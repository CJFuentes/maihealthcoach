using MAIHealthCoach.Domain.Notifications;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="DeviceRegistration"/> (issue #48). Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("DeviceRegistrations");

        builder.HasKey(e => e.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .IsRequired();

        // The platform push token. 1024 chars comfortably covers FCM/APNs/Web Push token lengths.
        builder.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(1024);

        // Persist the platform as its string name (stable across member reordering) rather than its
        // ordinal. 16 chars covers the longest member name with headroom.
        builder.Property(e => e.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(e => e.Name)
            .HasMaxLength(128);

        builder.Property(e => e.LastSeenAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // A push token identifies exactly one device install — unique globally. The register path
        // relies on this to detect handoff (token owned by another user) via the unique-index race.
        builder.HasIndex(e => e.Token)
            .IsUnique()
            .HasDatabaseName("IX_DeviceRegistrations_Token");

        // Primary access pattern: list/sweep a user's devices.
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_DeviceRegistrations_UserId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        // No navigation: there is no need to traverse from a registration back to its user.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DeviceRegistrations_Users_UserId");
    }
}
