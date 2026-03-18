using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface INoteMutationService
{
    event EventHandler<NoteMutationEventArgs>? NoteMutated;

    Task<NoteDocument> SaveAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default);
}
