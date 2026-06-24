using MAIHealthCoach.Application.Coaching;
using MAIHealthCoach.Application.Food;
using MAIHealthCoach.Infrastructure.Auth;
using MAIHealthCoach.Infrastructure.Coaching;
using MAIHealthCoach.Infrastructure.Configuration;
using MAIHealthCoach.Infrastructure.Food;
using MAIHealthCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services with the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Tag applied to health check registrations that gate readiness.</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. Set ConnectionStrings__Postgres via environment variable, user-secrets, or appsettings.Development.json.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddConfigurationOptions(configuration);

        services.AddClerkAuthentication();

        services.AddCoachingServices();

        services.AddNutritionLookupServices();

        return services;
    }

    /// <summary>
    /// Registers the prompt builder, the typed Anthropic <see cref="HttpClient"/>, and the
    /// <see cref="ICoachService"/> implementation (issue #36).
    /// </summary>
    /// <remarks>
    /// The Anthropic API key is <strong>not</strong> required at registration time — it is
    /// resolved lazily on the first call. This lets the application build, start, and pass
    /// health checks with an empty <see cref="AiOptions.ApiKey"/> (CI and local dev without
    /// secrets). The <c>BaseAddress</c> and <c>Timeout</c> are configured here from
    /// <see cref="AiOptions"/>; the secret <c>x-api-key</c> header is attached per request
    /// inside the client, never via <c>DefaultRequestHeaders</c> at startup.
    /// </remarks>
    public static IServiceCollection AddCoachingServices(this IServiceCollection services)
    {
        // Stateless and dependency-free — a singleton avoids per-request allocations.
        services.AddSingleton<CoachPromptBuilder>();

        services.AddHttpClient<AnthropicMessagesClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AiOptions>>().Value;

            // Ensure the base address ends with '/' so the relative path "v1/messages"
            // resolves to "{BaseUrl}/v1/messages" rather than dropping the last segment.
            var baseUrl = options.BaseUrl.TrimEnd('/') + '/';
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddScoped<ICoachService, CoachService>();

        return services;
    }

    /// <summary>
    /// Registers the typed Open Food Facts <see cref="HttpClient"/> and the
    /// <see cref="INutritionLookupService"/> implementation (issue #20).
    /// </summary>
    /// <remarks>
    /// The <c>BaseAddress</c>, <c>Timeout</c>, and required <c>User-Agent</c> are configured here
    /// from <see cref="OpenFoodFactsOptions"/>. The <c>User-Agent</c> is attached with
    /// <c>TryAddWithoutValidation</c> so a malformed value can never throw at startup — Open Food
    /// Facts requires the header but the app must still boot with default/empty configuration.
    /// </remarks>
    public static IServiceCollection AddNutritionLookupServices(this IServiceCollection services)
    {
        services.AddHttpClient<OpenFoodFactsClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<OpenFoodFactsOptions>>().Value;

            // Ensure the base address ends with '/' so relative paths like "api/v2/product/..."
            // resolve under the host rather than dropping the last segment.
            var baseUrl = options.BaseUrl.TrimEnd('/') + '/';
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
        });

        services.AddScoped<INutritionLookupService, NutritionLookupService>();

        return services;
    }

    /// <summary>
    /// Registers Clerk JWT bearer authentication, default authorization, and the scoped
    /// current-user provisioning service (issue #12).
    /// </summary>
    /// <remarks>
    /// The bearer scheme is wired from <see cref="ClerkOptions"/> by
    /// <see cref="ClerkJwtBearerConfigureOptions"/>, which is defensive: with empty Clerk
    /// configuration the handler fails closed (every token -> 401) and never throws at
    /// startup, so the API still builds, starts, and serves anonymous health checks.
    /// </remarks>
    public static IServiceCollection AddClerkAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configure JwtBearerOptions from ClerkOptions. Singleton mirrors the lifetime of
        // the bound options snapshot. Registered as IConfigureOptions so any test host can
        // layer a PostConfigure override (e.g. a local signing key) on top of it.
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ClerkJwtBearerConfigureOptions>();

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }

    /// <summary>
    /// Binds the strongly-typed <see cref="ClerkOptions"/> and <see cref="AiOptions"/> from
    /// configuration and registers format-only validators for them.
    /// </summary>
    /// <remarks>
    /// Validation is intentionally <strong>lazy</strong> — <c>ValidateOnStart()</c> is NOT
    /// used. The Clerk and AI integrations are not wired up yet (#12, #36), so the API must
    /// build, start, and pass health checks with empty Clerk/Claude secrets. The validators
    /// only check the <em>format</em> of values that are actually supplied, so a misconfigured
    /// URL is still caught the first time the options are resolved without forcing every run
    /// to carry real secrets.
    /// </remarks>
    public static IServiceCollection AddConfigurationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ClerkOptions>()
            .Bind(configuration.GetSection(ClerkOptions.SectionName));
        services.AddSingleton<IValidateOptions<ClerkOptions>, ClerkOptionsValidator>();

        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName));
        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

        services.AddOptions<OpenFoodFactsOptions>()
            .Bind(configuration.GetSection(OpenFoodFactsOptions.SectionName));
        services.AddSingleton<IValidateOptions<OpenFoodFactsOptions>, OpenFoodFactsOptionsValidator>();

        return services;
    }

    /// <summary>
    /// Registers the DbContext health check tagged "ready" with a 3-second per-check
    /// timeout so the readiness probe never hangs on an unreachable database.
    /// Must be called after <see cref="AddInfrastructure"/> so <see cref="AppDbContext"/>
    /// is already registered.
    /// </summary>
    public static IServiceCollection AddInfrastructureHealthChecks(
        this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "postgres",
                tags: [ReadyTag]);

        // AddDbContextCheck exposes no timeout parameter, so the 3-second per-check
        // timeout is applied by locating the registration after the fact. This caps
        // how long the readiness probe can block on an unreachable database.
        services.Configure<HealthCheckServiceOptions>(opts =>
        {
            var registration = opts.Registrations.FirstOrDefault(r => r.Name == "postgres");
            if (registration is not null)
            {
                registration.Timeout = TimeSpan.FromSeconds(3);
            }
        });

        return services;
    }
}
