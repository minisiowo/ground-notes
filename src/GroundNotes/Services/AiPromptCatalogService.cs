using System.Text.Json;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class AiPromptCatalogService : IAiPromptCatalogService
{
    private const string AppDataDirectoryName = ".groundnotes";
    private const string LegacyAppDataDirectoryName = ".quicknotestxt";
    private const string PromptsDirectoryName = "ai-prompts";
    private const string LegacyMigrationMarkerFileName = ".quicknotestxt-migrated";
    private const string StarterPromptsMarkerFileName = ".starter-prompts-created";

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
        return Path.Combine(notesFolder, AppDataDirectoryName, PromptsDirectoryName);
    }

    public async Task<AiPromptCatalogLoadResult> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default)
    {
        var builtInCatalog = await LoadBuiltInPromptsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(notesFolder))
        {
            return builtInCatalog;
        }

        var warnings = new List<string>(builtInCatalog.Warnings);
        warnings.AddRange(MigrateLegacyPrompts(notesFolder));
        warnings.AddRange(CreateStarterPrompts(notesFolder));

        var customDirectory = GetNotesFolderPromptsDirectory(notesFolder);
        var customCatalog = await LoadPromptsFromDirectoryAsync(customDirectory, isBuiltIn: false, cancellationToken);
        warnings.AddRange(customCatalog.Warnings);

        return new AiPromptCatalogLoadResult(
            customCatalog.Prompts
                .OrderBy(prompt => prompt.Order)
                .ThenBy(prompt => prompt.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            warnings);
    }

    private IReadOnlyList<string> MigrateLegacyPrompts(string notesFolder)
    {
        var legacyDirectory = Path.Combine(notesFolder, LegacyAppDataDirectoryName, PromptsDirectoryName);
        if (!Directory.Exists(legacyDirectory))
        {
            return [];
        }

        var targetDirectory = GetNotesFolderPromptsDirectory(notesFolder);
        var markerPath = Path.Combine(targetDirectory, LegacyMigrationMarkerFileName);
        if (File.Exists(markerPath))
        {
            return [];
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var warnings = new List<string>();
            var targetPromptIds = Directory.EnumerateFiles(targetDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(TryReadPrompt)
                .Where(static prompt => prompt is not null)
                .Select(static prompt => prompt!.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var legacyFile in Directory.EnumerateFiles(legacyDirectory, "*.json", SearchOption.TopDirectoryOnly)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var legacyPrompt = TryReadPrompt(legacyFile);
                if (!IsValid(legacyPrompt))
                {
                    warnings.Add(BuildWarning(legacyFile, "could not be migrated because it is malformed or invalid."));
                    continue;
                }

                if (targetPromptIds.Contains(legacyPrompt!.Id))
                {
                    continue;
                }

                var targetFile = GetMigrationTargetPath(targetDirectory, legacyFile, legacyPrompt.Id);
                File.Copy(legacyFile, targetFile);
                targetPromptIds.Add(legacyPrompt.Id);
            }

            File.WriteAllText(markerPath, "Legacy AI prompts migrated to .groundnotes.");
            return warnings;
        }
        catch (IOException)
        {
            return [$"Could not migrate legacy AI prompts from '{legacyDirectory}'."];
        }
        catch (UnauthorizedAccessException)
        {
            return [$"Could not migrate legacy AI prompts from '{legacyDirectory}'."];
        }
    }

    private IReadOnlyList<string> CreateStarterPrompts(string notesFolder)
    {
        var targetDirectory = GetNotesFolderPromptsDirectory(notesFolder);
        var markerPath = Path.Combine(targetDirectory, StarterPromptsMarkerFileName);
        if (File.Exists(markerPath))
        {
            return [];
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
            var targetPromptIds = Directory.EnumerateFiles(targetDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(TryReadPrompt)
                .Where(IsValid)
                .Select(static prompt => prompt!.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(BuiltInPromptsDirectory))
            {
                foreach (var starterFile in Directory.EnumerateFiles(BuiltInPromptsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    var starterPrompt = TryReadPrompt(starterFile);
                    if (!IsValid(starterPrompt) || targetPromptIds.Contains(starterPrompt!.Id))
                    {
                        continue;
                    }

                    var targetFile = GetMigrationTargetPath(targetDirectory, starterFile, starterPrompt.Id);
                    File.Copy(starterFile, targetFile);
                    targetPromptIds.Add(starterPrompt.Id);
                }
            }

            File.WriteAllText(markerPath, "Starter AI prompts created for this GroundNotes folder.");
            return [];
        }
        catch (IOException)
        {
            return [$"Could not create starter AI prompts in '{targetDirectory}'."];
        }
        catch (UnauthorizedAccessException)
        {
            return [$"Could not create starter AI prompts in '{targetDirectory}'."];
        }
    }

    private static AiPromptDefinition? TryReadPrompt(string filePath)
    {
        try
        {
            return JsonSerializer.Deserialize<AiPromptDefinition>(File.ReadAllText(filePath), s_jsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetMigrationTargetPath(string targetDirectory, string legacyFile, string promptId)
    {
        var originalTarget = Path.Combine(targetDirectory, Path.GetFileName(legacyFile));
        if (!File.Exists(originalTarget))
        {
            return originalTarget;
        }

        var safeId = new string(promptId
            .Trim()
            .ToLowerInvariant()
            .Select(static character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray())
            .Trim('-');
        if (string.IsNullOrWhiteSpace(safeId))
        {
            safeId = "legacy-prompt";
        }

        var candidate = Path.Combine(targetDirectory, safeId + ".json");
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(targetDirectory, $"{safeId}-{suffix}.json");
            suffix++;
        }

        return candidate;
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
