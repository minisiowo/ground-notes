using System.Text.Json;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class OpenAiTitleSuggestionService : IAiTitleSuggestionService
{
    private const string SuggestionModel = "gpt-5-mini";
    private const int SuggestionCount = 3;

    private readonly IOpenAiCompletionsClient _completionsClient;

    public OpenAiTitleSuggestionService(IOpenAiCompletionsClient completionsClient)
    {
        _completionsClient = completionsClient;
    }

    public OpenAiTitleSuggestionService(HttpClient httpClient)
        : this(new OpenAiCompletionsClient(httpClient))
    {
    }

    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        NoteDocument document,
        AiSettings settings,
        string? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var prompt = BuildPrompt(document, additionalContext);
        var response = await _completionsClient.CompleteAsync(
            [new AiChatMessage("user", prompt)],
            SuggestionModel,
            settings,
            options: null,
            cancellationToken);

        var suggestions = ParseSuggestions(response);
        if (suggestions.Count == 0)
        {
            throw new AiServiceException(AiServiceErrorKind.InvalidResponse, "AI returned no usable title suggestions.");
        }

        return suggestions;
    }

    internal static IReadOnlyList<string> ParseSuggestions(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return [];
        }

        if (TryParseJsonArray(response, out var jsonSuggestions))
        {
            return NormalizeSuggestions(jsonSuggestions);
        }

        var lines = response
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static line => line.Trim())
            .Select(static line => line.TrimStart('-', '*', ' ', '\t'))
            .Select(static line => line.Trim())
            .ToList();

        return NormalizeSuggestions(lines);
    }

    private static bool TryParseJsonArray(string response, out IReadOnlyList<string> suggestions)
    {
        suggestions = [];

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(response);
            if (parsed is null)
            {
                return false;
            }

            suggestions = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> NormalizeSuggestions(IEnumerable<string?> suggestions)
    {
        return suggestions
            .Select(static suggestion => suggestion?.Trim())
            .Where(static suggestion => !string.IsNullOrWhiteSpace(suggestion))
            .Select(static suggestion => suggestion!)
            .Select(RemoveListMarker)
            .Where(static suggestion => !string.IsNullOrWhiteSpace(suggestion))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(SuggestionCount)
            .ToList();
    }

    private static string RemoveListMarker(string value)
    {
        var candidate = value.Trim();
        if (candidate.Length >= 3
            && char.IsDigit(candidate[0])
            && candidate[1] == '.'
            && char.IsWhiteSpace(candidate[2]))
        {
            return candidate[3..].Trim();
        }

        return candidate;
    }

    private static string BuildPrompt(NoteDocument document, string? additionalContext)
    {
        var tags = document.Tags.Count == 0
            ? "(none)"
            : string.Join(", ", document.Tags);

        var normalizedContext = string.IsNullOrWhiteSpace(additionalContext)
            ? null
            : additionalContext.Trim();

        var prompt = $$"""
        Generate exactly 3 distinct filename-safe note title suggestions for this note.
        Requirements:
        - Return only a JSON array with 3 strings.
        - Each suggestion should be concise, descriptive, and suitable as a note filename.
        - Avoid quotation marks inside suggestions.
        - Do not add commentary.

        Current title: {{document.Title}}
        Tags: {{tags}}
        Body:
        {{document.Body}}
        """;

        if (string.IsNullOrWhiteSpace(normalizedContext))
        {
            return prompt;
        }

        return $$"""
        {{prompt}}

        Additional naming guidance:
        {{normalizedContext}}
        """;
    }
}
