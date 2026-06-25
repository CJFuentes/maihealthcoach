using MAIHealthCoach.Domain.Users;
using MAIHealthCoach.Domain.Water;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="WaterLogEntry"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class WaterLogEntryConfiguration : IEntityTypeConfiguration<WaterLogEntry>
{
    public void Configure(EntityTypeBuilder<WaterLogEntry> builder)
    {
        builder.ToTable("WaterLogEntries");

        builder.HasKey(e => e.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .IsRequired();

        // Whole millilitres. int maps to a native integer column; no precision needed.
        builder.Property(e => e.AmountMl)
            .IsRequired();

        // DateOnly maps to a native date column (EF Core 10 + Npgsql support DateOnly natively;
        // SQLite-backed tests store it as ISO text). Mirrors DiaryEntry.Date.
        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Primary access pattern: total + list all of a user's entries for one date. Composite
        // index covers the `WHERE UserId = ? AND Date = ?` predicate driving GET /me/water.
        builder.HasIndex(e => new { e.UserId, e.Date })
            .HasDatabaseName("IX_WaterLogEntries_UserId_Date");

        // Secondary index for user-scoped single-entry lookups (PUT/DELETE) where Date is not
        // part of the predicate.
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_WaterLogEntries_UserId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        // No navigation: there is no need to traverse from an entry back to its user.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WaterLogEntries_Users_UserId");
    }
}
