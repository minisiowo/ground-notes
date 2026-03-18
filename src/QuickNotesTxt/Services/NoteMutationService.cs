using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class NoteMutationService : INoteMutationService
{
    private readonly INotesRepository _notesRepository;

    public NoteMutationService(INotesRepository notesRepository)
    {
        _notesRepository = notesRepository;
    }

    public event EventHandler<NoteMutationEventArgs>? NoteMutated;

    public async Task<NoteDocument> SaveAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default)
    {
        var previousPath = document.FilePath;
        var saved = await _notesRepository.SaveNoteAsync(folderPath, document, cancellationToken);
        NoteMutated?.Invoke(this, new NoteMutationEventArgs(NoteMutationKind.Saved, previousPath, saved));
        return saved;
    }

    public async Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _notesRepository.DeleteNoteIfExistsAsync(filePath, cancellationToken);
        NoteMutated?.Invoke(this, new NoteMutationEventArgs(NoteMutationKind.Deleted, filePath));
    }
}
