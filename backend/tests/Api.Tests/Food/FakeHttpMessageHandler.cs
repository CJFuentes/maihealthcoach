using System.Net;

namespace MAIHealthCoach.Api.Tests.Food;

/// <summary>
/// Replaces the real HTTP transport so tests never call the Open Food Facts API. Configure
/// <see cref="ResponseFactory"/> before use; the last outgoing request and its buffered body are
/// captured for assertions. A duplicate of the Infrastructure.Tests handler — these are separate
/// test assemblies, so the duplication is expected.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// Supplies the response for each call. Receives the request and its already-buffered body (the
    /// request content is consumed before this runs). Defaults to a 200 with an empty body.
    /// </summary>
    public Func<HttpRequestMessage, string, HttpResponseMessage> ResponseFactory { get; set; } =
        (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

    /// <summary>The last request received. <see langword="null"/> before any call is made.</summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>The buffered body of the last request. <see langword="null"/> before any call.</summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>Number of times the transport was invoked. Useful to assert "no HTTP call was made".</summary>
    public int CallCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return ResponseFactory(request, LastRequestBody ?? string.Empty);
    }
}
