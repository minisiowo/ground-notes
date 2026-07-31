using GroundNotes.Models;

namespace GroundNotes.Services;

public interface ICustomSlashCommandCatalogService
{
    Task<CustomSlashCommandCatalogLoadResult> LoadCommandsAsync(string notesFolder, CancellationToken cancellationToken = default);

    string GetNotesFolderCommandsDirectory(string notesFolder);
}
