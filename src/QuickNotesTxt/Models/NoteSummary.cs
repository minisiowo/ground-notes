using CommunityToolkit.Mvvm.ComponentModel;

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
}
