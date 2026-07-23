using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class SidebarTreeRowViewModel : ViewModelBase
{
    private readonly Action<string, bool>? _expansionChanged;

    public SidebarTreeRowViewModel(
        SidebarTreeNode node,
        NoteListItemViewModel? note,
        int depth,
        string occurrencePath,
        bool isExpanded,
        Action<string, bool>? expansionChanged)
    {
        Node = node;
        Note = note;
        Depth = depth;
        OccurrencePath = occurrencePath;
        _isExpanded = isExpanded;
        _expansionChanged = expansionChanged;
    }

    public SidebarTreeNode Node { get; }

    public NoteListItemViewModel? Note { get; }

    public string Label => Node.Name;

    public string OccurrencePath { get; }

    public string? TagPath => (Node as SidebarTreeFolderNode)?.TagPath;

    public int Depth { get; }

    public Thickness IndentMargin => new(Depth * 12, 0, 0, 3);

    public bool IsFolder => Node.Kind == SidebarTreeNodeKind.Folder;

    public bool IsNote => Node.Kind == SidebarTreeNodeKind.Note;

    public bool HasChildren => Node is SidebarTreeFolderNode { Children.Count: > 0 };

    public string ExpansionGlyph => IsExpanded ? "⌄" : "›";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpansionGlyph))]
    private bool _isExpanded;

    [RelayCommand]
    private void ToggleExpanded()
    {
        if (!IsFolder || TagPath is null)
        {
            return;
        }

        IsExpanded = !IsExpanded;
        _expansionChanged?.Invoke(TagPath, IsExpanded);
    }
}
