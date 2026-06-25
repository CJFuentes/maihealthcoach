using MAIHealthCoach.Domain.Coaching;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Message"/> (issue #39). Mapped to the <c>CoachMessages</c> table
/// to keep the coaching-chat intent explicit. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("CoachMessages");

        builder.HasKey(m => m.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.ConversationId)
            .IsRequired();

        builder.Property(m => m.UserId)
            .IsRequired();

        // Enum stored as a readable string, consistent with other enums in the model.
        // 32 chars leaves headroom for future role members.
        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        // No length cap — chat content is free-form prose mapped to a text column.
        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.Sequence)
            .IsRequired();

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.UpdatedAt).IsRequired();

        // Primary read pattern: replay a conversation in order
        // (WHERE ConversationId = ? ORDER BY Sequence).
        builder.HasIndex(m => new { m.ConversationId, m.Sequence })
            .HasDatabaseName("IX_CoachMessages_ConversationId_Sequence");

        // Secondary index for user-scoped predicates (the denormalized authorization filter).
        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_CoachMessages_UserId");

        // FK to Conversations: Cascade — messages are owned children of their conversation, so
        // deleting a conversation removes its messages with it.
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CoachMessages_Conversations_ConversationId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CoachMessages_Users_UserId");
    }
}
