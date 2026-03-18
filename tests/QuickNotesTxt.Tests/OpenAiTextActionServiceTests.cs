using System.Net;
using System.Text;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class OpenAiTextActionServiceTests
{
    [Fact]
    public async Task RunPromptAsync_UsesPromptModelAndReplacesSelectedPlaceholder()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "Translated text"
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var service = new OpenAiTextActionService(httpClient);

        var result = await service.RunPromptAsync(
            new AiPromptDefinition("translate", "Translate", "Text: {selected}", null, "gpt-5.4-mini"),
            "hello",
            new AiSettings("secret", "gpt-5.4-nano", true));

        Assert.Equal("Translated text", result);
        Assert.Contains("gpt-5.4-mini", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Text: hello", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("secret", handler.AuthorizationParameter);
    }

    [Fact]
    public async Task RunPromptAsync_FallsBackToDefaultModel()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}", Encoding.UTF8, "application/json")
        });
        var service = new OpenAiTextActionService(new HttpClient(handler));

        await service.RunPromptAsync(
            new AiPromptDefinition("translate", "Translate", "{selected}"),
            "czesc",
            new AiSettings("secret", "gpt-5.4", true));

        Assert.Contains("gpt-5.4", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunPromptAsync_ThrowsWhenApiKeyIsMissing()
    {
        var service = new OpenAiTextActionService(new HttpClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call HTTP"))));

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.RunPromptAsync(
            new AiPromptDefinition("translate", "Translate", "{selected}"),
            "czesc",
            new AiSettings(string.Empty, "gpt-5.4", true)));

        Assert.Equal(AiServiceErrorKind.MissingApiKey, ex.Kind);
        Assert.Equal("Set your OpenAI API key first.", ex.Message);
    }

    [Fact]
    public async Task RunPromptAsync_SendsProjectAndOrganizationHeaders()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}", Encoding.UTF8, "application/json")
        });
        var service = new OpenAiTextActionService(new HttpClient(handler));

        await service.RunPromptAsync(
            new AiPromptDefinition("translate", "Translate", "{selected}"),
            "text",
            new AiSettings("secret", "gpt-5.4-mini", true, "proj_123", "org_456"));

        Assert.Equal("proj_123", handler.ProjectId);
        Assert.Equal("org_456", handler.OrganizationId);
    }

    [Fact]
    public async Task RunPromptAsync_UsesHelpfulQuotaMessageFor429()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""
            {
              "error": {
                "message": "You exceeded your current quota.",
                "type": "insufficient_quota",
                "code": "insufficient_quota"
              }
            }
            """, Encoding.UTF8, "application/json")
        };
        response.Headers.Add("x-request-id", "req_123");

        var service = new OpenAiTextActionService(new HttpClient(new FakeHttpMessageHandler(_ => response)));

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.RunPromptAsync(
            new AiPromptDefinition("translate", "Translate", "{selected}"),
            "text",
            new AiSettings("secret", "gpt-5.4-mini", true)));

        Assert.Equal(AiServiceErrorKind.QuotaExceeded, ex.Kind);
        Assert.Equal("req_123", ex.RequestId);
        Assert.Contains("Check billing, Project ID, or model access", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Request ID: req_123", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunPromptAsync_IncludesAdvancedParametersInPayload()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}", Encoding.UTF8, "application/json")
        });
        var service = new OpenAiTextActionService(new HttpClient(handler));

        await service.RunPromptAsync(
            new AiPromptDefinition("test", "Test", "{selected}", null, "o1", true, 0, false, 0.7, 100, "high"),
            "text",
            new AiSettings("secret", "gpt-5.4-mini", true));

        Assert.Contains("\"temperature\":0.7", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"max_tokens\":100", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"reasoning_effort\":\"high\"", handler.RequestBody, StringComparison.Ordinal);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            ProjectId = request.Headers.TryGetValues("OpenAI-Project", out var projectIds) ? projectIds.SingleOrDefault() : null;
            OrganizationId = request.Headers.TryGetValues("OpenAI-Organization", out var organizationIds) ? organizationIds.SingleOrDefault() : null;
            return _responseFactory(request);
        }
    }
}
