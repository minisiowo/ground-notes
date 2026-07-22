using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IAiPromptEditorService
{
    Task SaveCustomPromptAsync(string notesFolder, AiPromptDefinition prompt, CancellationToken cancellationToken = default);

    Task DeleteCustomPromptAsync(string notesFolder, string promptId, CancellationToken cancellationToken = default);

    string GetCustomPromptFilePath(string notesFolder, string promptId);
}
