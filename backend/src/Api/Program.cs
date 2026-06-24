using System.Reflection;
using MAIHealthCoach.Application;
using MAIHealthCoach.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

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

app.Run();

// Exposes the implicit Program class to the test assembly for WebApplicationFactory<Program>.
public partial class Program { }
