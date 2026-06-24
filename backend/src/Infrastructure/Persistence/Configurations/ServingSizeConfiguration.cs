using MAIHealthCoach.Domain.Food;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="ServingSize"/>. Discovered automatically by
/// <c>ApplyConfigurationsFromAssembly</c>.
/// </summary>
internal sealed class ServingSizeConfiguration : IEntityTypeConfiguration<ServingSize>
{
    public void Configure(EntityTypeBuilder<ServingSize> builder)
    {
        builder.ToTable("ServingSizes");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.FoodItemId)
            .IsRequired();

        builder.Property(s => s.Label)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(s => s.Unit)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(s => s.Quantity)
            .HasPrecision(9, 3)
            .IsRequired();

        builder.Property(s => s.GramsEquivalent)
            .HasPrecision(9, 3)
            .IsRequired();

        builder.Property(s => s.IsDefault)
            .IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // Retrieves all servings for a food efficiently.
        builder.HasIndex(s => s.FoodItemId)
            .HasDatabaseName("IX_ServingSizes_FoodItemId");

        // Owned by FoodItem; cascade delete removes servings when the food is deleted. Bind the
        // inverse to FoodItem.ServingSizes so EF treats this as a single relationship keyed on
        // FoodItemId rather than synthesizing a second shadow FK.
        builder.HasOne<FoodItem>()
            .WithMany(nameof(FoodItem.ServingSizes))
            .HasForeignKey(s => s.FoodItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ServingSizes_FoodItems_FoodItemId");
    }
}
