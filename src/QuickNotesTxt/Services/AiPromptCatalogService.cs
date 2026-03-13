using System.Text.Json;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class AiPromptCatalogService : IAiPromptCatalogService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    public async Task<IReadOnlyList<AiPromptDefinition>> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default)
    {
        var promptsById = new Dictionary<string, AiPromptDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var prompt in await LoadPromptsFromDirectoryAsync(BuiltInPromptsDirectory, isBuiltIn: true, cancellationToken))
        {
            promptsById[prompt.Id] = prompt;
        }

        if (!string.IsNullOrWhiteSpace(notesFolder))
        {
            var customDirectory = GetNotesFolderPromptsDirectory(notesFolder);
            foreach (var prompt in await LoadPromptsFromDirectoryAsync(customDirectory, isBuiltIn: false, cancellationToken))
            {
                promptsById[prompt.Id] = prompt;
            }
        }

        return promptsById.Values
            .OrderBy(prompt => prompt.Order)
            .ThenBy(prompt => prompt.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<IReadOnlyList<AiPromptDefinition>> LoadPromptsFromDirectoryAsync(string directory, bool isBuiltIn, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var prompts = new List<AiPromptDefinition>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var prompt = await JsonSerializer.DeserializeAsync<AiPromptDefinition>(stream, s_jsonOptions, cancellationToken);
                if (!IsValid(prompt))
                {
                    continue;
                }

                prompts.Add(prompt! with { IsBuiltIn = isBuiltIn });
            }
            catch (IOException)
            {
                // Skip unreadable files.
            }
            catch (JsonException)
            {
                // Skip malformed files.
            }
        }

        return prompts;
    }

    private static bool IsValid(AiPromptDefinition? prompt)
    {
        return prompt is not null
               && !string.IsNullOrWhiteSpace(prompt.Id)
               && !string.IsNullOrWhiteSpace(prompt.Name)
               && !string.IsNullOrWhiteSpace(prompt.PromptTemplate);
    }
}
