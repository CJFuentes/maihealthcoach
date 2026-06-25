using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Conversation"/> (issue #39). Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.HasKey(c => c.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.Title)
            .HasMaxLength(200);

        builder.Property(c => c.MessageCount)
            .IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt).IsRequired();

        // Primary access pattern: list a user's conversations newest-activity first
        // (WHERE UserId = ? ORDER BY UpdatedAt DESC).
        builder.HasIndex(c => new { c.UserId, c.UpdatedAt })
            .HasDatabaseName("IX_Conversations_UserId_UpdatedAt");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Conversations_Users_UserId");
    }
}
