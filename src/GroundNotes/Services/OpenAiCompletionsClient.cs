using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroundNotes.Models;

namespace GroundNotes.Services;

internal sealed class OpenAiCompletionsClient : IOpenAiCompletionsClient
{
    private const string CompletionsEndpoint = "https://api.openai.com/v1/chat/completions";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public OpenAiCompletionsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> CompleteAsync(
        IReadOnlyList<AiChatMessage> messages,
        string model,
        AiSettings settings,
        OpenAiCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new AiServiceException(AiServiceErrorKind.MissingApiKey, "Set your OpenAI API key first.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new AiServiceException(AiServiceErrorKind.MissingModel, "Choose an AI model first.");
        }

        var payload = new OpenAiChatCompletionsRequest(
            model,
            messages,
            options?.Temperature,
            options?.MaxTokens,
            options?.ReasoningEffort);

        using var request = CreateRequest(payload, settings);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateError(response, responseContent);
        }

        var completion = DeserializeResponse(response, responseContent);
        var content = completion?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        if (string.IsNullOrWhiteSpace(content))
        {
            throw CreateEmptyResponse(response);
        }

        return content.Trim();
    }

    private static HttpRequestMessage CreateRequest(OpenAiChatCompletionsRequest payload, AiSettings settings)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, CompletionsEndpoint)
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

        return request;
    }

    private static OpenAiChatCompletionsResponse? DeserializeResponse(HttpResponseMessage response, string responseContent)
    {
        try
        {
            return JsonSerializer.Deserialize<OpenAiChatCompletionsResponse>(responseContent, s_jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AiServiceException(
                AiServiceErrorKind.InvalidResponse,
                BuildInvalidResponseMessage(response),
                GetRequestId(response),
                innerException: ex);
        }
    }

    private static AiServiceException CreateEmptyResponse(HttpResponseMessage response)
    {
        return new AiServiceException(
            AiServiceErrorKind.EmptyResponse,
            BuildEmptyResponseMessage(response),
            GetRequestId(response));
    }

    private static AiServiceException CreateError(HttpResponseMessage response, string responseContent)
    {
        var requestId = GetRequestId(response);

        OpenAiError? apiError = null;
        try
        {
            apiError = JsonSerializer.Deserialize<OpenAiErrorEnvelope>(responseContent, s_jsonOptions)?.Error;
        }
        catch (JsonException)
        {
        }

        var kind = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => AiServiceErrorKind.Unauthorized,
            System.Net.HttpStatusCode.Forbidden => AiServiceErrorKind.Forbidden,
            System.Net.HttpStatusCode.TooManyRequests => BuildTooManyRequestsKind(apiError),
            >= System.Net.HttpStatusCode.InternalServerError => AiServiceErrorKind.TransientServerError,
            _ => AiServiceErrorKind.RequestFailed
        };

        var details = BuildErrorMessage(response, apiError, requestId);
        return new AiServiceException(kind, details, requestId, apiError?.Message);
    }

    private static AiServiceErrorKind BuildTooManyRequestsKind(OpenAiError? apiError)
    {
        if (string.Equals(apiError?.Code, "insufficient_quota", StringComparison.OrdinalIgnoreCase))
        {
            return AiServiceErrorKind.QuotaExceeded;
        }

        return AiServiceErrorKind.RateLimited;
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

    private static string BuildEmptyResponseMessage(HttpResponseMessage response)
    {
        var requestId = GetRequestId(response);
        return string.IsNullOrWhiteSpace(requestId)
            ? "AI response was empty."
            : $"AI response was empty. Request ID: {requestId}";
    }

    private static string BuildInvalidResponseMessage(HttpResponseMessage response)
    {
        var requestId = GetRequestId(response);
        return string.IsNullOrWhiteSpace(requestId)
            ? "AI response was invalid."
            : $"AI response was invalid. Request ID: {requestId}";
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

    private static string? GetRequestId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("x-request-id", out var requestIds)
            ? requestIds.FirstOrDefault()
            : null;
    }

    private sealed record OpenAiChatCompletionsRequest(
        string Model,
        IReadOnlyList<AiChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double? Temperature = null,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens = null,
        [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort = null);

    private sealed record OpenAiChatCompletionsResponse(IReadOnlyList<OpenAiChatCompletionsResponse.Choice>? Choices)
    {
        public sealed record Choice(AiChatMessage? Message);
    }

    private sealed record OpenAiErrorEnvelope(OpenAiError? Error);

    private sealed record OpenAiError(string? Message, string? Type, string? Code);
}
