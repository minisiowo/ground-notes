namespace QuickNotesTxt.Models;

public sealed record NoteDocument
{
    public string Id { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }

    public bool IsAutoCreated { get; init; }
}
