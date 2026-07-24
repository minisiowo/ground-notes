using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class NoteMutationEventArgs : EventArgs
{
    public NoteMutationEventArgs(
        NoteMutationKind kind,
        string previousPath,
        NoteDocument? document = null,
        Guid? originId = null,
        string? folderPath = null)
    {
        Kind = kind;
        PreviousPath = previousPath;
        Document = document;
        OriginId = originId;
        FolderPath = folderPath
            ?? Path.GetDirectoryName(document?.FilePath ?? previousPath)
            ?? string.Empty;
    }

    public NoteMutationKind Kind { get; }

    public string PreviousPath { get; }

    public NoteDocument? Document { get; }

    public Guid? OriginId { get; }

    public string FolderPath { get; }
}
