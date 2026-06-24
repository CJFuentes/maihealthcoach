namespace MAIHealthCoach.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the Open Food Facts (OFF) integration, bound from the
/// <c>OpenFoodFacts</c> configuration section. The <c>NutritionLookupService</c> (issue #20)
/// consumes these values. All values are non-secret and ship with safe defaults, so the app
/// builds, starts, and passes health checks without any OFF configuration supplied.
/// </summary>
/// <remarks>
/// Open Food Facts requires a descriptive <c>User-Agent</c> on every request; the
/// <see cref="UserAgent"/> default identifies this application per their API guidelines.
/// </remarks>
public sealed class OpenFoodFactsOptions
{
    /// <summary>Configuration section name this class binds from.</summary>
    public const string SectionName = "OpenFoodFacts";

    /// <summary>Default Open Food Facts API base URL.</summary>
    public const string DefaultBaseUrl = "https://world.openfoodfacts.org";

    /// <summary>Default <c>User-Agent</c> identifying this application to Open Food Facts.</summary>
    public const string DefaultUserAgent = "MAIHealthCoach/1.0 (+https://github.com/CJFuentes/maihealthcoach)";

    /// <summary>Base URL for the Open Food Facts API.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>
    /// <c>User-Agent</c> header sent on every OFF request. Open Food Facts requires a descriptive
    /// value; an empty value is rejected by the validator.
    /// </summary>
    public string UserAgent { get; set; } = DefaultUserAgent;

    /// <summary>HTTP request timeout, in seconds, for calls to Open Food Facts.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Staleness window, in days, applied to a cached food's <c>LastSyncedAt</c>. A cached barcode
    /// row older than this is refreshed from OFF on the next lookup.
    /// </summary>
    public int CacheTtlDays { get; set; } = 30;

    /// <summary>Number of results requested per text-search page.</summary>
    public int SearchPageSize { get; set; } = 20;
}
