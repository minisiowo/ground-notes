using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IAiPromptCatalogService
{
    string BuiltInPromptsDirectory { get; }

    string GetNotesFolderPromptsDirectory(string notesFolder);

    Task<IReadOnlyList<AiPromptDefinition>> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default);
}
