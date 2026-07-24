using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class NoteMutationService : INoteMutationService
{
    private static readonly AsyncLocal<Guid?> s_currentOriginId = new();
    private readonly INotesRepository _notesRepository;
    private readonly SemaphoreSlim _mutationLock = new(1, 1);

    public NoteMutationService(INotesRepository notesRepository)
    {
        _notesRepository = notesRepository;
    }

    public event EventHandler<NoteMutationEventArgs>? NoteMutated;

    public async Task<NoteDocument> SaveAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default, bool preserveTimestamp = false)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            var previousPath = document.FilePath;
            await EnsureCurrentVersionAsync(document, cancellationToken);
            var saved = await _notesRepository.SaveNoteAsync(folderPath, document, cancellationToken, preserveTimestamp);
            NoteMutated?.Invoke(this, new NoteMutationEventArgs(
                NoteMutationKind.Saved,
                previousPath,
                saved,
                s_currentOriginId.Value,
                folderPath));
            return saved;
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    public async Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try
        {
            await _notesRepository.DeleteNoteIfExistsAsync(filePath, cancellationToken);
            NoteMutated?.Invoke(this, new NoteMutationEventArgs(
                NoteMutationKind.Deleted,
                filePath,
                originId: s_currentOriginId.Value,
                folderPath: Path.GetDirectoryName(filePath)));
        }
        finally
        {
            _mutationLock.Release();
        }
    }

    private async Task EnsureCurrentVersionAsync(NoteDocument document, CancellationToken cancellationToken)
    {
        var current = await _notesRepository.LoadNoteAsync(document.FilePath, cancellationToken);
        if (current is null)
        {
            if (!document.IsAutoCreated)
            {
                throw new NoteSaveConflictException(document.FilePath);
            }
            return;
        }

        var hasContentVersions = !string.IsNullOrWhiteSpace(current.SourceContentHash)
                                 && !string.IsNullOrWhiteSpace(document.SourceContentHash);
        var changed = hasContentVersions
            ? !string.Equals(current.SourceContentHash, document.SourceContentHash, StringComparison.Ordinal)
            : current.UpdatedAt != document.UpdatedAt;
        if (changed)
        {
            throw new NoteSaveConflictException(document.FilePath);
        }
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
