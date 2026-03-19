namespace QuickNotesTxt.Services;

public sealed class NoteFileChangedEventArgs : EventArgs
{
    public readonly record struct NoteFileChange(NoteFileChangeKind Kind, string Path, string? OldPath = null);

    private readonly IReadOnlyList<NoteFileChange> _changes;

    public NoteFileChangedEventArgs(NoteFileChangeKind kind, string path, string? oldPath = null)
        : this([new NoteFileChange(kind, path, oldPath)])
    {
    }

    public NoteFileChangedEventArgs(IEnumerable<NoteFileChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        _changes = changes as NoteFileChange[] ?? changes.ToArray();

        if (_changes.Count == 0)
        {
            throw new ArgumentException("At least one note file change is required.", nameof(changes));
        }
    }

    public IReadOnlyList<NoteFileChange> Changes => _changes;

    public bool IsBatch => _changes.Count > 1;

    public NoteFileChangeKind Kind => _changes[0].Kind;

    public string Path => _changes[0].Path;

    public string? OldPath => _changes[0].OldPath;
}
