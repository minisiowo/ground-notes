using CommunityToolkit.Mvvm.ComponentModel;

namespace GroundNotes.ViewModels;

public sealed partial class TagFilterItemViewModel : ViewModelBase
{
    private readonly Action _selectionChanged;

    public TagFilterItemViewModel(string tag, int noteCount, bool isSelected, Action selectionChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentOutOfRangeException.ThrowIfNegative(noteCount);

        Tag = tag;
        NoteCount = noteCount;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    public string Tag { get; }

    public int NoteCount { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _selectionChanged();
}
