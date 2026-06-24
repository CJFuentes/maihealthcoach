using MAIHealthCoach.Infrastructure;
using MAIHealthCoach.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MAIHealthCoach.Api.Tests;

/// <summary>
/// Verifies that <see cref="ClerkOptions"/> and <see cref="AiOptions"/> bind correctly from
/// configuration, expose the expected defaults, and that the format-only validators allow the
/// app to run without any secrets while still rejecting genuinely malformed values.
/// </summary>
public sealed class ConfigurationOptionsTests
{
    private static IServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddConfigurationOptions(configuration);
        return services.BuildServiceProvider();
    }

    private static ClerkOptions GetClerk(Dictionary<string, string?> settings) =>
        BuildProvider(settings).GetRequiredService<IOptions<ClerkOptions>>().Value;

    private static AiOptions GetAi(Dictionary<string, string?> settings) =>
        BuildProvider(settings).GetRequiredService<IOptions<AiOptions>>().Value;

    [Fact]
    public void AiOptions_Defaults_AreSonnetAndOpus()
    {
        var ai = GetAi([]);

        Assert.Equal("claude-sonnet-4-6", ai.DefaultModel);
        Assert.Equal("claude-opus-4-8", ai.EscalationModel);
        Assert.Equal("https://api.anthropic.com", ai.BaseUrl);
        Assert.Equal(100, ai.TimeoutSeconds);
        Assert.Equal(string.Empty, ai.ApiKey);
    }

    [Fact]
    public void AiOptions_BindFromConfiguration_MapsEverySetting()
    {
        var ai = GetAi(new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = "sk-ant-test-123",
            ["Ai:DefaultModel"] = "claude-sonnet-4-6",
            ["Ai:EscalationModel"] = "claude-opus-4-8",
            ["Ai:BaseUrl"] = "https://example.test",
            ["Ai:TimeoutSeconds"] = "42",
        });

        Assert.Equal("sk-ant-test-123", ai.ApiKey);
        Assert.Equal("claude-sonnet-4-6", ai.DefaultModel);
        Assert.Equal("claude-opus-4-8", ai.EscalationModel);
        Assert.Equal("https://example.test", ai.BaseUrl);
        Assert.Equal(42, ai.TimeoutSeconds);
    }

    [Fact]
    public void AiOptions_EnvVarStyleDoubleUnderscore_Binds()
    {
        // The double-underscore convention is how the host maps env vars to nested config
        // keys. Exercise the real environment-variable provider so the Ai__ApiKey -> Ai:ApiKey
        // translation is genuinely verified (in-memory config does NOT perform this mapping).
        const string envKey = "Ai__ApiKey";
        var previous = Environment.GetEnvironmentVariable(envKey);
        try
        {
            Environment.SetEnvironmentVariable(envKey, "sk-ant-from-env");

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();
            services.AddConfigurationOptions(configuration);
            var ai = services.BuildServiceProvider()
                .GetRequiredService<IOptions<AiOptions>>().Value;

            Assert.Equal("sk-ant-from-env", ai.ApiKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envKey, previous);
        }
    }

    [Fact]
    public void ClerkOptions_BindFromConfiguration_MapsEverySetting()
    {
        var clerk = GetClerk(new Dictionary<string, string?>
        {
            ["Clerk:Authority"] = "https://acme.clerk.accounts.dev",
            ["Clerk:Issuer"] = "https://acme.clerk.accounts.dev",
            ["Clerk:JwksUrl"] = "https://acme.clerk.accounts.dev/.well-known/jwks.json",
            ["Clerk:Audience"] = "mai-api",
        });

        Assert.Equal("https://acme.clerk.accounts.dev", clerk.Authority);
        Assert.Equal("https://acme.clerk.accounts.dev", clerk.Issuer);
        Assert.Equal("https://acme.clerk.accounts.dev/.well-known/jwks.json", clerk.JwksUrl);
        Assert.Equal("mai-api", clerk.Audience);
    }

    [Fact]
    public void ClerkOptions_Defaults_AreEmpty()
    {
        var clerk = GetClerk([]);

        Assert.Equal(string.Empty, clerk.Authority);
        Assert.Equal(string.Empty, clerk.Issuer);
        Assert.Equal(string.Empty, clerk.JwksUrl);
        Assert.Equal(string.Empty, clerk.Audience);
    }

    [Fact]
    public void Options_ResolveWithNoSecrets_DoNotThrow()
    {
        // The core guarantee: with NO Clerk/Claude values supplied, resolving the options
        // (which triggers the lazy validators) must succeed so the app starts green.
        var provider = BuildProvider([]);

        var clerk = provider.GetRequiredService<IOptions<ClerkOptions>>().Value;
        var ai = provider.GetRequiredService<IOptions<AiOptions>>().Value;

        Assert.NotNull(clerk);
        Assert.NotNull(ai);
    }

    [Fact]
    public void AiOptions_InvalidBaseUrl_FailsValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Ai:BaseUrl"] = "not-a-url",
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AiOptions>>().Value);
    }

    [Fact]
    public void AiOptions_NonPositiveTimeout_FailsValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Ai:TimeoutSeconds"] = "0",
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AiOptions>>().Value);
    }

    [Fact]
    public void ClerkOptions_InvalidAuthority_FailsValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["Clerk:Authority"] = "::::not a url::::",
        });

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ClerkOptions>>().Value);
    }
}
