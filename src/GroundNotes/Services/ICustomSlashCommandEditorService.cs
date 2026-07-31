using GroundNotes.Models;

namespace GroundNotes.Services;

public interface ICustomSlashCommandEditorService
{
    Task SaveCustomCommandAsync(string notesFolder, CustomSlashCommandDefinition command, CancellationToken cancellationToken = default);

    Task SaveCustomCommandAsync(string notesFolder, CustomSlashCommandDefinition command, string originalCommandId, CancellationToken cancellationToken = default)
        => SaveCustomCommandAsync(notesFolder, command, cancellationToken);

    Task DeleteCustomCommandAsync(string notesFolder, string commandId, CancellationToken cancellationToken = default);

    string GetCustomCommandFilePath(string notesFolder, string commandId);
}
