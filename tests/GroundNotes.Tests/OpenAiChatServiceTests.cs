using System.Net;
using System.Text;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;
using GroundNotes.Tests.Helpers;

namespace GroundNotes.Tests;

public sealed class OpenAiChatServiceTests
{
    [Fact]
    public async Task GetResponseAsync_UsesHistoryAndHeaders()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "Assistant reply"
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        });
        var service = new OpenAiChatService(new HttpClient(handler));

        var result = await service.GetResponseAsync(
            new[]
            {
                new AiChatMessage("system", "System context"),
                new AiChatMessage("user", "Hello")
            },
            new AiSettings("secret", "gpt-5.4-mini", true, "proj_123", "org_456"),
            "gpt-5.4");

        Assert.Equal("Assistant reply", result);
        Assert.Contains("gpt-5.4", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("System context", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("Hello", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("secret", handler.AuthorizationParameter);
        Assert.Equal("proj_123", handler.ProjectId);
        Assert.Equal("org_456", handler.OrganizationId);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsTypedQuotaErrorFor429()
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
        response.Headers.Add("x-request-id", "req_456");

        var service = new OpenAiChatService(new HttpClient(new FakeHttpMessageHandler(_ => response)));

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.GetResponseAsync(
            [new AiChatMessage("user", "Hello")],
            new AiSettings("secret", "gpt-5.4-mini", true),
            "gpt-5.4"));

        Assert.Equal(AiServiceErrorKind.QuotaExceeded, ex.Kind);
        Assert.Equal("req_456", ex.RequestId);
        Assert.Contains("Check billing, Project ID, or model access", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsWhenApiKeyIsMissing()
    {
        var service = new OpenAiChatService(new HttpClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call HTTP"))));

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.GetResponseAsync(
            [new AiChatMessage("user", "Hello")],
            new AiSettings(string.Empty, "gpt-5.4-mini", true),
            "gpt-5.4"));

        Assert.Equal(AiServiceErrorKind.MissingApiKey, ex.Kind);
        Assert.Equal("Set your OpenAI API key first.", ex.Message);
    }

    [Fact]
    public async Task GetResponseAsync_ThrowsWhenModelIsMissing()
    {
        var service = new OpenAiChatService(new HttpClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Should not call HTTP"))));

        var ex = await Assert.ThrowsAsync<AiServiceException>(() => service.GetResponseAsync(
            [new AiChatMessage("user", "Hello")],
            new AiSettings("secret", string.Empty, true),
            string.Empty));

        Assert.Equal(AiServiceErrorKind.MissingModel, ex.Kind);
        Assert.Equal("Choose an AI model first.", ex.Message);
    }
}
