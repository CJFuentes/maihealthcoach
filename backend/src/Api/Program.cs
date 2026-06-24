using System.Reflection;
using MAIHealthCoach.Api.Middleware;
using MAIHealthCoach.Application;
using MAIHealthCoach.Infrastructure;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext();

        if (ctx.HostingEnvironment.IsDevelopment())
        {
            // Human-readable console with the correlation ID surfaced in each line so
            // requests are traceable locally. The non-Development path emits compact JSON
            // where every enriched scope property (incl. CorrelationId) is a first-class field.
            cfg.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}");
        }
        else
        {
            cfg.WriteTo.Console(new CompactJsonFormatter());
        }
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddInfrastructureHealthChecks();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddOpenApi();

    var app = builder.Build();

    if (app.Environment.IsDevelopment()
        && app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseExceptionHandler();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.MapHealthChecks("/healthz", new HealthCheckOptions
    {
        Predicate = check => !check.Tags.Contains(MAIHealthCoach.Infrastructure.DependencyInjection.ReadyTag),
    });

    app.MapHealthChecks("/healthz/live", new HealthCheckOptions
    {
        Predicate = check => !check.Tags.Contains(MAIHealthCoach.Infrastructure.DependencyInjection.ReadyTag),
    });

    app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains(MAIHealthCoach.Infrastructure.DependencyInjection.ReadyTag),
    });

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
            timestamp = DateTime.UtcNow,
        });
    })
    .WithName("Ping");

    // Diagnostic endpoint that deliberately throws, used to exercise the global
    // exception handler. Available in Development, or when explicitly opted in via the
    // Testing:ExposeThrowEndpoint flag (used by integration tests that need to verify
    // the handler's behaviour in a non-Development environment). Never exposed in a
    // real production deployment, where neither condition holds.
    if (app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("Testing:ExposeThrowEndpoint"))
    {
        app.MapGet("/api/v1/throw-test", () =>
        {
            throw new InvalidOperationException("GlobalExceptionHandler smoke test.");
        }).WithName("ThrowTest");
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application startup failed.");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program { }
