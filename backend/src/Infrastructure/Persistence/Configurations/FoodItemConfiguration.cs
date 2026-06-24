using MAIHealthCoach.Domain.Food;
using MAIHealthCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MAIHealthCoach.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="FoodItem"/> and its owned per-100 g <see cref="NutritionFacts"/>.
/// Discovered automatically by <c>ApplyConfigurationsFromAssembly</c> in
/// <see cref="AppDbContext.OnModelCreating"/>.
/// </summary>
internal sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("FoodItems");

        builder.HasKey(f => f.Id);

        // Client-generated UUIDv7 — the database must not override it.
        builder.Property(f => f.Id)
            .ValueGeneratedNever();

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(f => f.Brand)
            .HasMaxLength(256);

        // GTIN/EAN as text (preserves leading zeros). Max GTIN-14 length is 14 digits.
        builder.Property(f => f.Barcode)
            .HasMaxLength(14);

        // Provenance enum stored as a readable string, consistent with other enums in the model.
        // 32 chars leaves headroom for future FoodSource members (issue #24 catalogues).
        builder.Property(f => f.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(f => f.SourceReference)
            .HasMaxLength(128);

        builder.Property(f => f.LastSyncedAt);

        // Nullable owner FK: set for custom foods (issue #24), null for shared OFF foods.
        builder.Property(f => f.CreatedByUserId);

        builder.Property(f => f.CreatedAt).IsRequired();
        builder.Property(f => f.UpdatedAt).IsRequired();

        // Barcode lookup index. NON-UNIQUE: regional variants and re-used GTINs legitimately
        // collide, and #20 will upsert-by-barcode without failing on duplicates. The Postgres
        // partial filter (WHERE barcode IS NOT NULL) is applied in the migration only — keeping
        // it out of the model so SQLite-backed tests (EnsureCreated) build a plain index.
        // NOTE: any future migration that drops/recreates IX_FoodItems_Barcode must re-add the
        // partial filter by hand, since the EF model snapshot does not carry it.
        builder.HasIndex(f => f.Barcode)
            .HasDatabaseName("IX_FoodItems_Barcode");

        // Name search index (prefix/equality lookups for #21).
        builder.HasIndex(f => f.Name)
            .HasDatabaseName("IX_FoodItems_Name");

        // Owner index: supports listing a user's own custom foods (WHERE CreatedByUserId = ?)
        // for GET /me/foods (issue #24). Most rows are shared OFF foods with a null owner.
        builder.HasIndex(f => f.CreatedByUserId)
            .HasDatabaseName("IX_FoodItems_CreatedByUserId");

        // FK to Users for the custom-food owner. Nullable FK => optional relationship; users are
        // never hard-deleted in v1, so Restrict is fine (it never cascade-deletes foods).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(f => f.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FoodItems_Users_CreatedByUserId");

        // Per-100 g nutrition is a REQUIRED owned value object. Marking the navigation required
        // (plus non-nullable macro columns below) prevents EF's "all owned columns null =>
        // owned nav null" materialization from yielding a null NutritionPer100g for legitimate
        // all-zero-macro foods (e.g. water 0/0/0/0).
        builder.OwnsOne(f => f.NutritionPer100g, n =>
        {
            n.Property(p => p.EnergyKcal)
                .HasColumnName("Nutrition_EnergyKcal")
                .HasPrecision(7, 2)
                .IsRequired();

            n.Property(p => p.ProteinG)
                .HasColumnName("Nutrition_ProteinG")
                .HasPrecision(6, 2)
                .IsRequired();

            n.Property(p => p.CarbohydrateG)
                .HasColumnName("Nutrition_CarbohydrateG")
                .HasPrecision(6, 2)
                .IsRequired();

            n.Property(p => p.FatG)
                .HasColumnName("Nutrition_FatG")
                .HasPrecision(6, 2)
                .IsRequired();

            n.Property(p => p.SugarsG)
                .HasColumnName("Nutrition_SugarsG")
                .HasPrecision(6, 2);

            n.Property(p => p.FiberG)
                .HasColumnName("Nutrition_FiberG")
                .HasPrecision(6, 2);

            n.Property(p => p.SaturatedFatG)
                .HasColumnName("Nutrition_SaturatedFatG")
                .HasPrecision(6, 2);

            n.Property(p => p.SodiumMg)
                .HasColumnName("Nutrition_SodiumMg")
                .HasPrecision(8, 2);
        });

        builder.Navigation(f => f.NutritionPer100g).IsRequired();
    }
}
