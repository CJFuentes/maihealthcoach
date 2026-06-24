using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MAIHealthCoach.Api.Tests;

public sealed class GlobalExceptionHandlerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GlobalExceptionHandlerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnhandledException_Returns500WithProblemJson()
    {
        var response = await _client.GetAsync("/api/v1/throw-test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnhandledException_ResponseBody_ContainsStatusAndTitle()
    {
        var response = await _client.GetAsync("/api/v1/throw-test");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(500, json.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_IncludesExceptionTypeExtension()
    {
        var response = await _client.GetAsync("/api/v1/throw-test");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.TryGetProperty("exceptionType", out var exType),
            "exceptionType extension field must be present in Development.");
        Assert.False(string.IsNullOrWhiteSpace(exType.GetString()));
    }

    [Fact]
    public async Task UnhandledException_ResponseBody_DoesNotContainStackTrace()
    {
        var response = await _client.GetAsync("/api/v1/throw-test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnhandledException_InProduction_OmitsExceptionDetailAndStackTrace()
    {
        // A Production-environment host must never leak exception detail. The throw-test
        // endpoint is exposed via the Testing:ExposeThrowEndpoint flag (not the Development
        // environment) so the handler runs under IsDevelopment() == false.
        await using var prodFactory = new ProductionThrowFactory();
        var client = prodFactory.CreateClient();

        var response = await client.GetAsync("/api/v1/throw-test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.TryGetProperty("exceptionType", out _),
            "exceptionType must be absent outside Development.");
        Assert.False(json.TryGetProperty("exceptionMessage", out _),
            "exceptionMessage must be absent outside Development.");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("   at ", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Test host running in the Production environment with the diagnostic throw endpoint
    /// exposed via configuration so the global exception handler can be exercised with
    /// IsDevelopment() == false.
    /// </summary>
    private sealed class ProductionThrowFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("Testing:ExposeThrowEndpoint", "true");
        }
    }
}
