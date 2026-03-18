using CommunityToolkit.Mvvm.ComponentModel;
using QuickNotesTxt.Services;

namespace QuickNotesTxt.Models;

public sealed partial class NoteSummary : ObservableObject
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

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    public static NoteSummary FromDocument(NoteDocument document, bool includeRenameText = false)
    {
        ArgumentNullException.ThrowIfNull(document);

        var summary = new NoteSummary
        {
            Id = document.Id,
            FilePath = document.FilePath,
            Title = document.Title,
            Tags = [.. document.Tags],
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            Preview = NotePreviewFormatter.Build(document.Body),
            SearchText = BuildSearchText(document)
        };

        if (includeRenameText)
        {
            summary.RenameText = Path.GetFileNameWithoutExtension(document.FilePath);
        }

        return summary;
    }

    public static string BuildSearchText(NoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return string.Join(' ', new[] { document.Title, document.Body, string.Join(' ', document.Tags) });
    }
}
