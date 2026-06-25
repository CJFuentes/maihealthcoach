using System.Reflection;
using Asp.Versioning;
using MAIHealthCoach.Api.Features.Coach;
using MAIHealthCoach.Api.Features.Diary;
using MAIHealthCoach.Api.Features.Exercises;
using MAIHealthCoach.Api.Features.Foods;
using MAIHealthCoach.Api.Features.Goals;
using MAIHealthCoach.Api.Features.Profile;
using MAIHealthCoach.Api.Features.Summary;
using MAIHealthCoach.Api.Features.Water;
using MAIHealthCoach.Api.Middleware;
using MAIHealthCoach.Application;
using MAIHealthCoach.Infrastructure;
using MAIHealthCoach.Infrastructure.Auth;
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

    // Per-user rate limiting for the coach chat send endpoint (issue #39). The policy reads
    // CoachChatOptions at request time so configuration overrides apply.
    builder.Services.AddCoachChatRateLimiter();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // API versioning — URL-segment reader so /api/v{n}/... routes are authoritative.
    // AddApiExplorer wires Asp.Versioning into the API description provider so
    // Microsoft.AspNetCore.OpenApi can discover versioned endpoints.
    // SubstituteApiVersionInUrl=true replaces the {version:apiVersion} token with the
    // literal version number in each ApiDescription.RelativePath, so the OpenAPI
    // document shows /api/v1/ping instead of /api/v{version}/ping.
    builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    // Named OpenAPI document "v1" from Microsoft.AspNetCore.OpenApi, served at
    // /openapi/v1.json. Asp.Versioning's ApiExplorer sets ApiDescription.GroupName to
    // "v1" for versioned endpoints. The `GroupName == null` clause is a safety net for any
    // FUTURE unversioned minimal-API endpoint (e.g. a /status route registered outside a
    // versioned group). Note: health-check endpoints (MapHealthChecks) are not part of the
    // API description pipeline, so /healthz never appears in this document — by design.
    builder.Services.AddOpenApi("v1", options =>
    {
        options.ShouldInclude = desc =>
            desc.GroupName == null || desc.GroupName == "v1";
    });

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

    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseExceptionHandler();

    app.UseSerilogRequestLogging();

    // Authentication + authorization sit after request logging (so 401s are still logged
    // with their correlation ID) and before the endpoints they guard. Anonymous endpoints
    // (health checks, /api/v1/ping) carry no authorization metadata, so the authorization
    // middleware lets them through untouched.
    app.UseAuthentication();
    app.UseAuthorization();

    // Rate limiting sits after authentication/authorization so the per-user partition key can
    // read the authenticated principal's 'sub' claim (issue #39). Endpoints opt in via
    // RequireRateLimiting; everything else passes through unthrottled.
    app.UseRateLimiter();

    if (app.Environment.IsDevelopment())
    {
        // Serves /openapi/v1.json. Gated on Development so the production surface
        // stays minimal. Integration tests run as Development (see TestWebApplicationFactory).
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

    // ── Versioned API endpoints ───────────────────────────────────────────────────
    // NewVersionedApi creates a versioned route group tracked by the API explorer.
    // HasApiVersion declares the version that owns the group; the route prefix includes
    // {version:apiVersion} so UrlSegmentApiVersionReader can match it. The literal
    // request /api/v1/ping resolves here, and (with SubstituteApiVersionInUrl) the
    // OpenAPI document reports the path as /api/v1/ping.
    var api = app.NewVersionedApi("MAIHealthCoach");
    var v1 = api.MapGroup("api/v{version:apiVersion}").HasApiVersion(new ApiVersion(1, 0));

    v1.MapGet("/ping", () =>
    {
        var version = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.0.0";

        return Results.Ok(new
        {
            service = "MAIHealthCoach.Api",
            version,
            timestamp = DateTimeOffset.UtcNow,
        });
    })
    .WithName("Ping");

    // Protected endpoint: returns the current user profile stub, provisioning the local
    // User row from the Clerk JWT on first authenticated request. RequireAuthorization
    // makes the auth middleware enforce the default policy, so a missing/invalid token
    // yields a 401 before the handler runs.
    v1.MapGet("/me", async (ICurrentUserService currentUser, CancellationToken ct) =>
    {
        var user = await currentUser.GetOrCreateCurrentUserAsync(ct);
        return Results.Ok(new
        {
            id = user.Id,
            clerkUserId = user.ClerkUserId,
            email = user.Email,
            createdAt = user.CreatedAt,
        });
    })
    .WithName("GetCurrentUser")
    .RequireAuthorization();

    // User profile endpoints (issue #16): authenticated GET/PUT /api/v1/me/profile.
    v1.MapProfileEndpoints();

    // Goals engine endpoints (issue #17): authenticated GET /api/v1/me/goals and
    // PUT /api/v1/me/goals/overrides.
    v1.MapGoalsEndpoints();

    // MAI coach endpoints (issue #37): authenticated GET /api/v1/me/coach/meal-suggestions.
    v1.MapCoachEndpoints();

    // Food search & detail endpoints (issue #21): authenticated GET /api/v1/foods,
    // GET /api/v1/foods/{id}, and GET /api/v1/foods/barcode/{code} over INutritionLookupService.
    v1.MapFoodsEndpoints();

    // Food diary endpoints (issue #22): authenticated POST /api/v1/me/diary,
    // GET /api/v1/me/diary?date=, PUT/DELETE /api/v1/me/diary/{id}.
    v1.MapDiaryEndpoints();

    // Daily nutrition summary endpoint (issue #23): authenticated
    // GET /api/v1/me/summary?date= — consumed vs goal targets for calories + macros.
    v1.MapSummaryEndpoints();

    // Custom foods, favorites & recents endpoints (issue #24): authenticated CRUD over
    // /api/v1/me/foods plus /me/foods/{id}/favorite and the /me/foods/favorites and
    // /me/foods/recents listings.
    v1.MapMyFoodsEndpoints();

    // Water log endpoints (issue #31): authenticated POST /api/v1/me/water,
    // GET /api/v1/me/water?date=, PUT/DELETE /api/v1/me/water/{id} — log amounts and
    // report the day's consumed total vs the daily water goal (with remaining).
    v1.MapWaterEndpoints();

    // Exercise catalog endpoints (issue #33): authenticated GET /api/v1/exercises (list/search
    // seeded shared + the caller's own custom activities) and POST /api/v1/exercises (create a
    // custom activity). Exercise logging (#34) and UI (#35) build on this catalog.
    v1.MapExercisesEndpoints();

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
