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

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;
}
