namespace MAIHealthCoach.Api.Middleware;

/// <summary>
/// Reads the <c>X-Correlation-ID</c> request header (or generates a new value when absent)
/// and makes it available for the lifetime of the request: stored in HttpContext.Items,
/// added to the ILogger scope, and echoed in the response header (including 500 responses).
/// Must be registered before UseExceptionHandler so the scope is active when the handler logs.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.NewGuid().ToString("D");

        context.Items[HttpContextItemKey] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [HttpContextItemKey] = correlationId,
        }))
        {
            await _next(context);
        }
    }
}
