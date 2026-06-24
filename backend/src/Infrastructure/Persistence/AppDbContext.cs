using Microsoft.EntityFrameworkCore;

namespace MAIHealthCoach.Infrastructure.Persistence;

/// <summary>
/// The application's single EF Core <see cref="DbContext"/> and unit-of-work.
/// Inject it directly into services that need data access; the change tracker
/// provides the unit-of-work boundary and each <see cref="DbSet{TEntity}"/> acts
/// as the repository for its aggregate. No DbSets are declared yet — entity sets
/// are added in later milestones. The initial migration therefore creates only the
/// <c>__EFMigrationsHistory</c> table and the <c>public</c> schema, establishing the
/// migration baseline from which the schema is built up.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
