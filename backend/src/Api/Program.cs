using System.Reflection;
using MAIHealthCoach.Application;
using MAIHealthCoach.Infrastructure;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Apply pending EF Core migrations automatically in Development when opted in.
// Guarded by Database:AutoMigrate (default false in appsettings.json) so integration
// tests and CI never attempt a database connection. Dev-only by design — production
// applies migrations via `dotnet ef database update` in the deploy pipeline.
if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // HTTPS redirection is unnecessary in Development and would interfere with
    // in-memory integration tests (WebApplicationFactory serves plain HTTP).
    app.UseHttpsRedirection();
}

// Liveness probe. Maps to /healthz per the acceptance criteria.
app.MapHealthChecks("/healthz");

// Version ping endpoint.
app.MapGet("/api/v1/ping", () =>
{
    var version = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion
        ?? "0.0.0";

    return Results.Ok(new
    {
        service = "MAIHealthCoach.Api",
        version,
        timestamp = DateTime.UtcNow
    });
})
.WithName("Ping");

await app.RunAsync();

// Exposes the implicit Program class to the test assembly for WebApplicationFactory<Program>.
public partial class Program { }
