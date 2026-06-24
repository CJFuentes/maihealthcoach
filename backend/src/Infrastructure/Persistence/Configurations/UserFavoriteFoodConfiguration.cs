using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="UserFavoriteFood"/>, the user-to-food favorites join row
/// (issue #24). Discovered automatically by <c>ApplyConfigurationsFromAssembly</c> in
/// <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class UserFavoriteFoodConfiguration : IEntityTypeConfiguration<UserFavoriteFood>
{
    public void Configure(EntityTypeBuilder<UserFavoriteFood> builder)
    {
        builder.ToTable("UserFavoriteFoods");

        builder.HasKey(f => f.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        builder.Property(f => f.UserId)
            .IsRequired();

        builder.Property(f => f.FoodItemId)
            .IsRequired();

        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();

        // A user can favorite a given food at most once: enforce the (UserId, FoodItemId) pairing
        // uniqueness at the database level so the API's idempotent favorite is race-safe. This
        // index also serves the favorites-list lookup (WHERE UserId = ?).
        builder.HasIndex(f => new { f.UserId, f.FoodItemId })
            .IsUnique()
            .HasDatabaseName("IX_UserFavoriteFoods_UserId_FoodItemId");

        // FK to Users: Restrict — users are never hard-deleted in v1; this guards orphan rows.
        // No navigation: there is no need to traverse from a favorite back to its user.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_UserFavoriteFoods_Users_UserId");

        // FK to FoodItems: Cascade — deleting a custom food should remove its favorite rows so no
        // dangling favorites survive. (Shared OFF foods are not hard-deleted, so cascade only
        // fires for custom-food deletes.)
        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(f => f.FoodItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_UserFavoriteFoods_FoodItems_FoodItemId");
    }
}
