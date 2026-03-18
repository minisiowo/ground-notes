using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IAiPromptCatalogService
{
    string BuiltInPromptsDirectory { get; }

    string GetNotesFolderPromptsDirectory(string notesFolder);

    Task<AiPromptCatalogLoadResult> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default);
}
