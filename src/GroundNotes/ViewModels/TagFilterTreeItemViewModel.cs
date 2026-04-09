using System.Collections.ObjectModel;

using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GroundNotes.ViewModels;

public sealed partial class TagFilterTreeItemViewModel : ViewModelBase
{
    private readonly Action<string, bool> _expansionChanged;

    public TagFilterTreeItemViewModel(
        TagFilterItemViewModel tagFilter,
        string label,
        IEnumerable<TagFilterTreeItemViewModel> children,
        int depth,
        bool isExpanded,
        Action<string, bool> expansionChanged)
    {
        ArgumentNullException.ThrowIfNull(tagFilter);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(children);
        ArgumentNullException.ThrowIfNull(expansionChanged);

        TagFilter = tagFilter;
        Label = label;
        Depth = depth;
        Children = new ObservableCollection<TagFilterTreeItemViewModel>(children);
        _isExpanded = isExpanded;
        _expansionChanged = expansionChanged;
    }

    public TagFilterItemViewModel TagFilter { get; }

    public string Tag => TagFilter.Tag;

    public string Label { get; }

    public string ExpansionGlyph => IsExpanded ? "v" : ">";

    public int Depth { get; }

    public Thickness IndentMargin => new(Depth * 6, 0, 0, 0);

    public int NoteCount => TagFilter.NoteCount;

    public ObservableCollection<TagFilterTreeItemViewModel> Children { get; }

    public bool HasChildren => Children.Count > 0;

    public bool ShowChildren => HasChildren && IsExpanded;

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ExpansionGlyph));
        OnPropertyChanged(nameof(ShowChildren));
        _expansionChanged(Tag, value);
    }

    [RelayCommand]
    public void ToggleExpanded()
    {
        if (!HasChildren)
        {
            return;
        }

        IsExpanded = !IsExpanded;
    }
}
