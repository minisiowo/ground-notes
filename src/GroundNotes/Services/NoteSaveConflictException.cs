namespace GroundNotes.Services;

public sealed class NoteSaveConflictException : IOException
{
    public NoteSaveConflictException(string filePath)
        : base($"The note changed before it could be saved: {filePath}")
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
}
