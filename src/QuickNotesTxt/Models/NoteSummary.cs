using QuickNotesTxt.Services;

namespace QuickNotesTxt.Models;

public sealed class NoteSummary
{
    public string Id { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public string Preview { get; init; } = string.Empty;

    public string SearchText { get; init; } = string.Empty;

    public string DisplayName => Path.GetFileNameWithoutExtension(FilePath);

    public bool HasPickerTags => Tags.Count > 0;

    public bool HasPickerPreview => !string.IsNullOrWhiteSpace(Preview);

    public string PickerTagsText
    {
        get
        {
            if (Tags.Count == 0)
            {
                return string.Empty;
            }

            var visibleTags = Tags.Take(3).ToArray();
            var text = string.Join(", ", visibleTags);

            return Tags.Count > visibleTags.Length
                ? $"{text} +{Tags.Count - visibleTags.Length}"
                : text;
        }
    }

    public string PickerPreviewText => Preview.Trim();

    public static NoteSummary FromDocument(NoteDocument document, bool includeRenameText = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        return FromContent(
            document.Id,
            document.FilePath,
            document.Title,
            document.Tags,
            document.CreatedAt,
            document.UpdatedAt,
            document.Body,
            includeRenameText);
    }

    public static string BuildSearchText(NoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return BuildSearchText(document.Title, document.Body, document.Tags);
    }

    public static string BuildSearchText(string title, string body, IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return string.Join(' ', new[] { title, body, string.Join(' ', tags) });
    }

    public static NoteSummary FromContent(
        string id,
        string filePath,
        string title,
        IReadOnlyList<string> tags,
        DateTime createdAt,
        DateTime updatedAt,
        string body,
        bool includeRenameText = false)
    {
        var summary = new NoteSummary
        {
            Id = id,
            FilePath = filePath,
            Title = title,
            Tags = [.. tags],
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Preview = NotePreviewFormatter.Build(body),
            SearchText = BuildSearchText(title, body, tags)
        };

        return summary;
    }
}
