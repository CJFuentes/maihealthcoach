using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="User"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        // The key is a client-generated UUIDv7 (see EntityBase), so the database must not
        // attempt to generate it.
        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.ClerkUserId)
            .IsRequired()
            .HasMaxLength(256);

        // One local user per Clerk identity. The unique index also makes the
        // get-or-create race fail closed at the database (duplicate insert -> 23505).
        builder.HasIndex(u => u.ClerkUserId)
            .IsUnique()
            .HasDatabaseName("IX_Users_ClerkUserId");

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .IsRequired();
    }
}
