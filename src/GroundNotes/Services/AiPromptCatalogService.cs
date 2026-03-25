using System.Text.Json;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class AiPromptCatalogService : IAiPromptCatalogService
{
    private static JsonSerializerOptions s_jsonOptions => JsonDefaults.ReadOptions;

    private readonly SemaphoreSlim _builtInCacheLock = new(1, 1);
    private AiPromptCatalogLoadResult? _cachedBuiltInPrompts;

    public string BuiltInPromptsDirectory { get; }

    public AiPromptCatalogService()
        : this(Path.Combine(AppContext.BaseDirectory, "Assets", "AiPrompts"))
    {
    }

    public AiPromptCatalogService(string builtInPromptsDirectory)
    {
        BuiltInPromptsDirectory = builtInPromptsDirectory;
    }

    public string GetNotesFolderPromptsDirectory(string notesFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notesFolder);
        return Path.Combine(notesFolder, ".quicknotestxt", "ai-prompts");
    }

    public async Task<AiPromptCatalogLoadResult> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default)
    {
        var builtInCatalog = await LoadBuiltInPromptsAsync(cancellationToken);
        var promptsById = new Dictionary<string, AiPromptDefinition>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>(builtInCatalog.Warnings);

        foreach (var prompt in builtInCatalog.Prompts)
        {
            promptsById[prompt.Id] = prompt;
        }

        if (!string.IsNullOrWhiteSpace(notesFolder))
        {
            var customDirectory = GetNotesFolderPromptsDirectory(notesFolder);
            var customCatalog = await LoadPromptsFromDirectoryAsync(customDirectory, isBuiltIn: false, cancellationToken);
            warnings.AddRange(customCatalog.Warnings);

            foreach (var prompt in customCatalog.Prompts)
            {
                promptsById[prompt.Id] = prompt;
            }
        }

        return new AiPromptCatalogLoadResult(
            promptsById.Values
            .OrderBy(prompt => prompt.Order)
            .ThenBy(prompt => prompt.Name, StringComparer.OrdinalIgnoreCase)
            .ToList(),
            warnings);
    }

    private async Task<AiPromptCatalogLoadResult> LoadBuiltInPromptsAsync(CancellationToken cancellationToken)
    {
        if (_cachedBuiltInPrompts is not null)
        {
            return _cachedBuiltInPrompts;
        }

        await _builtInCacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedBuiltInPrompts is not null)
            {
                return _cachedBuiltInPrompts;
            }

            _cachedBuiltInPrompts = await LoadPromptsFromDirectoryAsync(BuiltInPromptsDirectory, isBuiltIn: true, cancellationToken);
            return _cachedBuiltInPrompts;
        }
        finally
        {
            _builtInCacheLock.Release();
        }
    }

    private static async Task<AiPromptCatalogLoadResult> LoadPromptsFromDirectoryAsync(string directory, bool isBuiltIn, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return AiPromptCatalogLoadResult.Empty;
        }

        var prompts = new List<AiPromptDefinition>();
        var warnings = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var prompt = await JsonSerializer.DeserializeAsync<AiPromptDefinition>(stream, s_jsonOptions, cancellationToken);
                if (!IsValid(prompt))
                {
                    warnings.Add(BuildWarning(file, "missing required fields."));
                    continue;
                }

                prompts.Add(prompt! with { IsBuiltIn = isBuiltIn });
            }
            catch (IOException)
            {
                warnings.Add(BuildWarning(file, "could not be read."));
            }
            catch (JsonException)
            {
                warnings.Add(BuildWarning(file, "malformed JSON."));
            }
        }

        return new AiPromptCatalogLoadResult(prompts, warnings);
    }

    private static string BuildWarning(string filePath, string reason)
    {
        return $"Skipped prompt file '{filePath}': {reason}";
    }

    private static bool IsValid(AiPromptDefinition? prompt)
    {
        return prompt is not null
               && !string.IsNullOrWhiteSpace(prompt.Id)
               && !string.IsNullOrWhiteSpace(prompt.Name)
               && !string.IsNullOrWhiteSpace(prompt.PromptTemplate);
    }
}
