namespace QuickNotesTxt.Services;

public sealed class NoteFileChangedEventArgs : EventArgs
{
    public NoteFileChangedEventArgs(NoteFileChangeKind kind, string path, string? oldPath = null)
    {
        Kind = kind;
        Path = path;
        OldPath = oldPath;
    }

    public NoteFileChangeKind Kind { get; }

    public string Path { get; }

    public string? OldPath { get; }
}
