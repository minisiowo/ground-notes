using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class NoteMutationEventArgs : EventArgs
{
    public NoteMutationEventArgs(NoteMutationKind kind, string previousPath, NoteDocument? document = null, Guid? originId = null)
    {
        Kind = kind;
        PreviousPath = previousPath;
        Document = document;
        OriginId = originId;
    }

    public NoteMutationKind Kind { get; }

    public string PreviousPath { get; }

    public NoteDocument? Document { get; }

    public Guid? OriginId { get; }
}
