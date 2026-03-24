using System.Net;
using System.Text;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class OpenAiTitleSuggestionServiceTests
{
    [Fact]
    public async Task GetSuggestionsAsync_UsesPinnedMiniModelAndOmitsOptionalTuningParams()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "[\"project-outline\",\"meeting-summary\",\"deployment-checklist\"]"
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        });
        var service = new OpenAiTitleSuggestionService(new HttpClient(handler));
        var document = new NoteDocument
        {
            Title = "draft",
            Body = "Body text",
            Tags = ["work", "ops"]
        };

        var result = await service.GetSuggestionsAsync(document, new AiSettings("secret", "gpt-5", true));

        Assert.Equal(["project-outline", "meeting-summary", "deployment-checklist"], result);
        Assert.Contains("gpt-5-mini", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"temperature\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"max_tokens\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"reasoning_effort\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Current title: draft", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Tags: work, ops", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Body text", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSuggestionsAsync_IncludesAdditionalContextWhenProvided()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "[\"project-outline\",\"meeting-summary\",\"deployment-checklist\"]"
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        });
        var service = new OpenAiTitleSuggestionService(new HttpClient(handler));

        await service.GetSuggestionsAsync(
            new NoteDocument
            {
                Title = "draft",
                Body = "Body text"
            },
            new AiSettings("secret", "gpt-5", true),
            "Prefer something short and focused on release planning.");

        Assert.Contains("Additional naming guidance:", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Prefer something short and focused on release planning.", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseSuggestions_DeduplicatesAndNormalizesNewlineResponse()
    {
        var result = OpenAiTitleSuggestionService.ParseSuggestions("""
            1. project-outline
            - project-outline
            * meeting-summary

            deployment-checklist
            """);

        Assert.Equal(["project-outline", "meeting-summary", "deployment-checklist"], result);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ThrowsWhenApiKeyIsMissing()
    {
        var service = new OpenAiTitleSuggestionService(new HttpClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call HTTP"))));

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.GetSuggestionsAsync(
            new NoteDocument
            {
                Title = "draft",
                Body = "Body text"
            },
            new AiSettings(string.Empty, "gpt-5.4-mini", true)));

        Assert.Equal(AiServiceErrorKind.MissingApiKey, ex.Kind);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responseFactory(request);
        }
    }
}
