using System.Text.Json.Serialization;

namespace GroundNotes.Models;

public sealed record AiPromptDefinition(
    string Id,
    string Name,
    string PromptTemplate,
    string? Description = null,
    string? Model = null,
    bool ReplaceSelection = true,
    int Order = 0,
    bool IsBuiltIn = false,
    [property: JsonPropertyName("temperature")] double? Temperature = null,
    [property: JsonPropertyName("max_tokens")] int? MaxTokens = null,
    [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort = null);
