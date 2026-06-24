using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Infrastructure.Tests.Food;

/// <summary>
/// Format-only validation tests for <see cref="OpenFoodFactsOptionsValidator"/>. All OFF values ship
/// with safe defaults, so the validator only fails a genuinely misconfigured base URL, an empty
/// User-Agent (OFF requires one), or a non-sensible numeric setting.
/// </summary>
public sealed class OpenFoodFactsOptionsValidatorTests
{
    private static OpenFoodFactsOptions ValidOptions() => new()
    {
        BaseUrl = "https://world.openfoodfacts.org",
        UserAgent = "MAIHealthCoach/1.0 (+https://example.com)",
        TimeoutSeconds = 15,
        CacheTtlDays = 30,
        SearchPageSize = 20,
    };

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        var result = new OpenFoodFactsOptionsValidator().Validate(null, new OpenFoodFactsOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithValidExplicitOptions_Succeeds()
    {
        var result = new OpenFoodFactsOptionsValidator().Validate(null, ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyUserAgent_Fails(string userAgent)
    {
        var options = ValidOptions();
        options.UserAgent = userAgent;

        var result = new OpenFoodFactsOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(OpenFoodFactsOptions.UserAgent)));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://world.openfoodfacts.org")]
    [InlineData("world.openfoodfacts.org")]
    public void Validate_WithBadBaseUrl_Fails(string baseUrl)
    {
        var options = ValidOptions();
        options.BaseUrl = baseUrl;

        var result = new OpenFoodFactsOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(OpenFoodFactsOptions.BaseUrl)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_WithNonPositiveTimeout_Fails(int timeoutSeconds)
    {
        var options = ValidOptions();
        options.TimeoutSeconds = timeoutSeconds;

        var result = new OpenFoodFactsOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(OpenFoodFactsOptions.TimeoutSeconds)));
    }

    [Fact]
    public void Validate_WithNegativeCacheTtl_Fails()
    {
        var options = ValidOptions();
        options.CacheTtlDays = -1;

        var result = new OpenFoodFactsOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(OpenFoodFactsOptions.CacheTtlDays)));
    }

    [Fact]
    public void Validate_WithNonPositiveSearchPageSize_Fails()
    {
        var options = ValidOptions();
        options.SearchPageSize = 0;

        var result = new OpenFoodFactsOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains(nameof(OpenFoodFactsOptions.SearchPageSize)));
    }
}
