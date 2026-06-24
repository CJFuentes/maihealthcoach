using MAIHealthCoach.Domain.Diary;
using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="DiaryEntry"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c> in <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class DiaryEntryConfiguration : IEntityTypeConfiguration<DiaryEntry>
{
    public void Configure(EntityTypeBuilder<DiaryEntry> builder)
    {
        builder.ToTable("DiaryEntries");

        builder.HasKey(e => e.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.FoodItemId)
            .IsRequired();

        builder.Property(e => e.ServingSizeId)
            .IsRequired();

        // Fractional servings supported (e.g. 1.5 cups). Precision mirrors ServingSizes.Quantity.
        builder.Property(e => e.Quantity)
            .HasPrecision(9, 3)
            .IsRequired();

        // Enum stored as a readable string, consistent with other enums in the model.
        // 32 chars leaves headroom for future MealType members.
        builder.Property(e => e.MealType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        // DateOnly maps to a native date column (EF Core 10 + Npgsql support DateOnly natively;
        // SQLite-backed tests store it as ISO text). Mirrors UserProfile.DateOfBirth.
        builder.Property(e => e.Date)
            .IsRequired();

        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Primary access pattern: list all of a user's entries for one date. Composite index
        // covers the `WHERE UserId = ? AND Date = ?` predicate driving GET /me/diary.
        builder.HasIndex(e => new { e.UserId, e.Date })
            .HasDatabaseName("IX_DiaryEntries_UserId_Date");

        // Secondary index for user-scoped single-entry lookups (PUT/DELETE) and the diary
        // summaries planned for issue #23, where Date is not part of the predicate.
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_DiaryEntries_UserId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        // No navigation: there is no need to traverse from an entry back to its user.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DiaryEntries_Users_UserId");

        // FK to FoodItems: Restrict — food cache rows keep stable Ids (RefreshFromSource) and
        // are never hard-deleted (issue #19), so diary entries must never be cascade-deleted.
        // The navigation lets ListDay eager-load the food (and its servings) for nutrition.
        builder.HasOne(e => e.FoodItem)
            .WithMany()
            .HasForeignKey(e => e.FoodItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DiaryEntries_FoodItems_FoodItemId");

        // FK to ServingSizes: Restrict — the serving's GramsEquivalent is read at query time to
        // compute consumed nutrition; it must not disappear out from under existing entries.
        builder.HasOne(e => e.ServingSize)
            .WithMany()
            .HasForeignKey(e => e.ServingSizeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_DiaryEntries_ServingSizes_ServingSizeId");
    }
}
