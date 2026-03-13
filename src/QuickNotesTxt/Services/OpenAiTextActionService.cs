using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class OpenAiTextActionService : IAiTextActionService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;

    public OpenAiTextActionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> RunPromptAsync(AiPromptDefinition prompt, string selectedText, AiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            throw new InvalidOperationException("Select text first.");
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Set your OpenAI API key first.");
        }

        var model = !string.IsNullOrWhiteSpace(prompt.Model)
            ? prompt.Model
            : settings.DefaultModel;

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidOperationException("Choose an AI model first.");
        }

        var renderedPrompt = prompt.PromptTemplate.Replace("{selected}", selectedText, StringComparison.Ordinal);
        var payload = new ChatCompletionsRequest(
            model,
            [new ChatMessage("user", renderedPrompt)]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, s_jsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.Add("X-Client-Request-Id", Guid.NewGuid().ToString("D"));

        if (!string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            request.Headers.Add("OpenAI-Project", settings.ProjectId);
        }

        if (!string.IsNullOrWhiteSpace(settings.OrganizationId))
        {
            request.Headers.Add("OpenAI-Organization", settings.OrganizationId);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateError(response, responseContent);
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionsResponse>(responseContent, s_jsonOptions);
        var content = completion?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI response was empty.");
        }

        return content.Trim();
    }

    private sealed record ChatCompletionsRequest(string Model, IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionsResponse(IReadOnlyList<Choice>? Choices);

    private sealed record Choice(ChatMessage? Message);

    private sealed record OpenAiErrorEnvelope(OpenAiError? Error);

    private sealed record OpenAiError(string? Message, string? Type, string? Code);

    private static InvalidOperationException CreateError(HttpResponseMessage response, string responseContent)
    {
        var requestId = response.Headers.TryGetValues("x-request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;

        OpenAiError? apiError = null;
        try
        {
            apiError = JsonSerializer.Deserialize<OpenAiErrorEnvelope>(responseContent, s_jsonOptions)?.Error;
        }
        catch (JsonException)
        {
        }

        var details = BuildErrorMessage(response, apiError, requestId);
        return new InvalidOperationException(details);
    }

    private static string BuildErrorMessage(HttpResponseMessage response, OpenAiError? apiError, string? requestId)
    {
        var prefix = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "OpenAI rejected the API key.",
            System.Net.HttpStatusCode.Forbidden => "OpenAI denied access to this model or project.",
            System.Net.HttpStatusCode.TooManyRequests => BuildTooManyRequestsMessage(apiError),
            >= System.Net.HttpStatusCode.InternalServerError => "OpenAI is temporarily unavailable.",
            _ => $"OpenAI request failed ({(int)response.StatusCode})."
        };

        var message = string.IsNullOrWhiteSpace(apiError?.Message)
            ? prefix
            : $"{prefix} {apiError.Message}";

        return string.IsNullOrWhiteSpace(requestId)
            ? message
            : $"{message} Request ID: {requestId}";
    }

    private static string BuildTooManyRequestsMessage(OpenAiError? apiError)
    {
        if (string.Equals(apiError?.Code, "insufficient_quota", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenAI quota is unavailable for this request. Check billing, Project ID, or model access.";
        }

        if (string.Equals(apiError?.Code, "rate_limit_exceeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apiError?.Type, "rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenAI rate limit was exceeded. Wait a moment and try again.";
        }

        return "OpenAI rejected the request with status 429. Check billing, Project ID, model access, or try again shortly.";
    }
}
