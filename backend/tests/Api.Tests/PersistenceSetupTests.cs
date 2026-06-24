using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Verifies the EF Core + PostgreSQL wiring introduced for issue #3 without ever
/// opening a database connection. Every assertion here inspects registration,
/// model metadata, or design-time construction — none of which touch Postgres —
/// so the suite runs in CI with no database available.
/// </summary>
public sealed class PersistenceSetupTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PersistenceSetupTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AppDbContext_IsRegisteredAndResolvableFromScope()
    {
        // Resolving the context from a scope proves AddInfrastructure registered it
        // and the Npgsql provider options bind. Construction does not open a connection.
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.NotNull(context);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [Fact]
    public void AppDbContext_IsRegisteredWithScopedLifetime()
    {
        // A DbContext must be scoped: a singleton would leak the change tracker across
        // requests and a transient would defeat the unit-of-work boundary. Inspecting
        // the captured descriptor asserts the lifetime without constructing the context.
        _ = _factory.Services; // Force the host to build so CapturedServices is populated.

        var descriptor = Assert.Single(
            _factory.CapturedServices,
            d => d.ServiceType == typeof(AppDbContext));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AppDbContext_ResolvesADistinctInstancePerScope()
    {
        // Confirms the observable consequence of the scoped registration: each scope
        // gets its own context instance, while resolving twice within one scope does not.
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var first = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstAgain = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Same(first, firstAgain);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void AppDbContext_Model_UsesPublicDefaultSchema()
    {
        // Builds the model directly (no host, no connection) to assert OnModelCreating
        // applied HasDefaultSchema("public"). This guards the migration baseline.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=test")
            .Options;

        using var context = new AppDbContext(options);

        Assert.Equal("public", context.Model.GetDefaultSchema());
    }

    [Fact]
    public void DesignTimeFactory_CreatesNpgsqlContext_WithoutOpeningConnection()
    {
        // The dotnet-ef tooling relies on this factory to build the model for
        // `migrations add`. Constructing the context must succeed offline and be
        // configured for the Npgsql provider. CreateDbContext does not open a connection.
        var factory = new AppDbContextFactory();

        using var context = factory.CreateDbContext([]);

        Assert.NotNull(context);
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal("public", context.Model.GetDefaultSchema());
    }
}
