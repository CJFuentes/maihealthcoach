using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace MAIHealthCoach.Api.Tests.Auth;

/// <summary>
/// Test host for authentication integration tests. Two overrides make the protected
/// <c>/api/v1/me</c> endpoint testable without external dependencies:
/// <list type="number">
///   <item><description>
///   <c>JwtBearerOptions</c> are post-configured with the test public signing key and a
///   fixed issuer, and all remote metadata is disabled — so the real bearer handler
///   validates locally-minted tokens (see <see cref="JwtTestHelper"/>) and never calls Clerk.
///   </description></item>
///   <item><description>
///   The Npgsql <c>AppDbContext</c> registration is replaced with a SQLite in-memory
///   database (a real relational provider that enforces the <c>ClerkUserId</c> unique
///   index) so user provisioning can be exercised without Postgres. The schema is created
///   once via <c>EnsureCreated</c>.
///   </description></item>
/// </list>
/// </summary>
public sealed class AuthTestWebApplicationFactory : WebApplicationFactory<Program>
{
    // A single open connection keeps the SQLite in-memory database alive for the factory's
    // lifetime (the DB is dropped when the last connection closes).
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // AddInfrastructure throws if ConnectionStrings:Postgres is absent. Supply a dummy
        // so the host builds; the real provider is swapped for SQLite in ConfigureTestServices.
        builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=ignored");
        builder.UseSetting("Database:AutoMigrate", "false");

        base.ConfigureWebHost(builder);

        _connection.Open();

        builder.ConfigureTestServices(services =>
        {
            // PostConfigure runs after ClerkJwtBearerConfigureOptions, so it overrides the
            // production token-validation parameters with the local test key and issuer.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    // Disable remote metadata discovery so the handler validates locally
                    // against the injected test key. MetadataAddress is left unset; clearing
                    // Authority is sufficient to stop the OIDC discovery fetch.
                    options.Authority = null;
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = JwtTestHelper.TestIssuer,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = JwtTestHelper.PublicSigningKey,
                        NameClaimType = "sub",
                        RoleClaimType = "role",
                        ClockSkew = TimeSpan.Zero,
                    };
                });

            ReplaceDbContextWithSqlite(services);
        });
    }

    private void ReplaceDbContextWithSqlite(IServiceCollection services)
    {
        // Remove every trace of the Npgsql AppDbContext registration. On .NET 10 the
        // per-context options live in IDbContextOptionsConfiguration<AppDbContext>, which the
        // .NET 8-era DbContextOptions removal recipe misses — leaving it causes the
        // "only a single database provider can be registered" failure.
        services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
        services.RemoveAll<DbContextOptions<AppDbContext>>();
        services.RemoveAll<DbContextOptions>();
        services.RemoveAll<AppDbContext>();

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Create the schema on the shared SQLite connection once the real host (and its
        // service provider) exists. Doing this here — rather than building a throwaway
        // provider inside ConfigureTestServices — keeps the deferred host builder intact.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }
}
