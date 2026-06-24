using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MAIHealthCoach.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tooling (<c>dotnet ef</c>) to construct an
/// <see cref="AppDbContext"/> outside the running application. It is never registered
/// in DI and never executes at runtime.
/// <para>
/// Configuration is resolved the same way the application resolves it, so that
/// <c>dotnet ef database update</c> targets the SAME database the developer configured
/// (avoiding a dual source-of-truth between the app and the tooling). Resolution order:
/// <list type="number">
///   <item><c>ConnectionStrings:Postgres</c> from configuration — i.e. <c>appsettings.json</c>,
///   <c>appsettings.{Environment}.json</c>, or the <c>ConnectionStrings__Postgres</c>
///   environment variable (the standard ASP.NET Core override, and the recommended way
///   to point the tooling at a real database).</item>
///   <item>The <c>EFCORE_CONNECTION_STRING</c> environment variable — a last-resort fallback
///   reached only when <c>ConnectionStrings:Postgres</c> is absent from every configuration
///   source (note: the committed <c>appsettings.json</c> always supplies a placeholder value
///   for that key, so to use this variable you must run the tooling from a directory without
///   that <c>appsettings.json</c> on the base path).</item>
///   <item>A localhost fallback, so that <c>dotnet ef migrations add</c> can run with no
///   database available — <c>migrations add</c> does not open a connection, it only needs a
///   provider configured to build the model.</item>
/// </list>
/// </para>
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile(
                $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json",
                optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            config.GetConnectionString("Postgres")
            ?? Environment.GetEnvironmentVariable("EFCORE_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=maihealthcoach_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
