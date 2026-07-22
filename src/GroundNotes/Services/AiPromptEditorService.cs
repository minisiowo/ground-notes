using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class AiPromptEditorService : IAiPromptEditorService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly IAiPromptCatalogService _promptCatalogService;

    public AiPromptEditorService(IAiPromptCatalogService promptCatalogService)
    {
        _promptCatalogService = promptCatalogService;
    }

    public async Task SaveCustomPromptAsync(string notesFolder, AiPromptDefinition prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentNullException.ThrowIfNull(prompt);

        if (string.IsNullOrWhiteSpace(prompt.Id))
        {
            throw new InvalidOperationException("Prompt ID is required.");
        }

        if (string.IsNullOrWhiteSpace(prompt.Name))
        {
            throw new InvalidOperationException("Prompt name is required.");
        }

        if (string.IsNullOrWhiteSpace(prompt.PromptTemplate))
        {
            throw new InvalidOperationException("Prompt template is required.");
        }

        if (prompt.Temperature is { } temperature
            && (!double.IsFinite(temperature) || temperature is < 0 or > 2))
        {
            throw new InvalidOperationException("Temperature must be a number from 0 to 2.");
        }

        if (prompt.MaxTokens is <= 0)
        {
            throw new InvalidOperationException("Max tokens must be a positive whole number.");
        }

        var directory = _promptCatalogService.GetNotesFolderPromptsDirectory(notesFolder);
        Directory.CreateDirectory(directory);
        var existingFiles = await FindCustomPromptFilesByIdAsync(directory, prompt.Id, cancellationToken);
        var filePath = existingFiles.FirstOrDefault() ?? GetCustomPromptFilePath(notesFolder, prompt.Id);
        if (existingFiles.Count == 0 && File.Exists(filePath))
        {
            throw new InvalidOperationException("Another prompt uses the same filename. Choose a different prompt ID.");
        }

        var normalized = new PromptWriteRecord(
            prompt.Id.Trim(),
            prompt.Name.Trim(),
            prompt.PromptTemplate.Trim(),
            string.IsNullOrWhiteSpace(prompt.Description) ? null : prompt.Description.Trim(),
            string.IsNullOrWhiteSpace(prompt.Model) ? null : prompt.Model.Trim(),
            prompt.ReplaceSelection,
            prompt.Order,
            prompt.Temperature,
            prompt.MaxTokens,
            string.IsNullOrWhiteSpace(prompt.ReasoningEffort)
                ? null
                : AiReasoningEffortCatalog.Normalize(prompt.ReasoningEffort));

        var temporaryFilePath = filePath + ".tmp";
        try
        {
            await using (var stream = File.Create(temporaryFilePath))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, s_jsonOptions, cancellationToken);
            }

            File.Move(temporaryFilePath, filePath, overwrite: true);
            foreach (var duplicateFile in existingFiles.Skip(1))
            {
                File.Delete(duplicateFile);
            }
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    public async Task DeleteCustomPromptAsync(string notesFolder, string promptId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptId);

        var directory = _promptCatalogService.GetNotesFolderPromptsDirectory(notesFolder);
        var matchingFiles = await FindCustomPromptFilesByIdAsync(directory, promptId, cancellationToken);
        if (matchingFiles.Count == 0)
        {
            var expectedFilePath = GetCustomPromptFilePath(notesFolder, promptId);
            if (File.Exists(expectedFilePath))
            {
                File.Delete(expectedFilePath);
            }

            return;
        }

        foreach (var matchingFile in matchingFiles)
        {
            File.Delete(matchingFile);
        }
    }

    public string GetCustomPromptFilePath(string notesFolder, string promptId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptId);

        var directory = _promptCatalogService.GetNotesFolderPromptsDirectory(notesFolder);
        return Path.Combine(directory, BuildSafeFileName(promptId));
    }

    private static async Task<IReadOnlyList<string>> FindCustomPromptFilesByIdAsync(
        string directory,
        string promptId,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var matches = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var existingPrompt = await JsonSerializer.DeserializeAsync<AiPromptDefinition>(
                    stream,
                    JsonDefaults.ReadOptions,
                    cancellationToken);
                if (string.Equals(existingPrompt?.Id, promptId, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(file);
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return matches;
    }

    private static string BuildSafeFileName(string promptId)
    {
        var builder = new StringBuilder();
        foreach (var character in promptId.Trim())
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (char.IsWhiteSpace(character) || character is '.' or '/')
            {
                builder.Append('-');
            }
        }

        var fileName = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(fileName)
            ? "prompt.json"
            : fileName + ".json";
    }

    private sealed record PromptWriteRecord(
        string Id,
        string Name,
        string PromptTemplate,
        string? Description,
        string? Model,
        bool ReplaceSelection,
        int Order,
        double? Temperature,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens,
        [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort);
}
