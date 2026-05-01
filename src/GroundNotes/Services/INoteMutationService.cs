using GroundNotes.Models;

namespace GroundNotes.Services;

public interface INoteMutationService
{
    event EventHandler<NoteMutationEventArgs>? NoteMutated;

    Task<NoteDocument> SaveAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default, bool preserveTimestamp = false);

    Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default);
}
