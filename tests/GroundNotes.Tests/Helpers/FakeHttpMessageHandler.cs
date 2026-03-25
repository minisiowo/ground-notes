namespace GroundNotes.Tests.Helpers;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public string RequestBody { get; private set; } = string.Empty;

    public string? AuthorizationScheme { get; private set; }

    public string? AuthorizationParameter { get; private set; }

    public string? ProjectId { get; private set; }

    public string? OrganizationId { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBody = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        AuthorizationScheme = request.Headers.Authorization?.Scheme;
        AuthorizationParameter = request.Headers.Authorization?.Parameter;
        ProjectId = request.Headers.TryGetValues("OpenAI-Project", out var projectIds)
            ? projectIds.SingleOrDefault()
            : null;
        OrganizationId = request.Headers.TryGetValues("OpenAI-Organization", out var organizationIds)
            ? organizationIds.SingleOrDefault()
            : null;
        return _responseFactory(request);
    }
}
