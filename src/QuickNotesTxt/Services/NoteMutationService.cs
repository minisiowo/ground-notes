using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class NoteMutationService : INoteMutationService
{
    private static readonly AsyncLocal<Guid?> s_currentOriginId = new();
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
        NoteMutated?.Invoke(this, new NoteMutationEventArgs(NoteMutationKind.Saved, previousPath, saved, s_currentOriginId.Value));
        return saved;
    }

    public async Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _notesRepository.DeleteNoteIfExistsAsync(filePath, cancellationToken);
        NoteMutated?.Invoke(this, new NoteMutationEventArgs(NoteMutationKind.Deleted, filePath, originId: s_currentOriginId.Value));
    }

    public static IDisposable BeginMutationScope(Guid originId)
    {
        var previousOriginId = s_currentOriginId.Value;
        s_currentOriginId.Value = originId;
        return new MutationScope(previousOriginId);
    }

    private sealed class MutationScope : IDisposable
    {
        private readonly Guid? _previousOriginId;
        private bool _disposed;

        public MutationScope(Guid? previousOriginId)
        {
            _previousOriginId = previousOriginId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            s_currentOriginId.Value = _previousOriginId;
            _disposed = true;
        }
    }
}
