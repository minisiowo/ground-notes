using CommunityToolkit.Mvvm.ComponentModel;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class NoteListItemViewModel : ObservableObject
{
    public NoteListItemViewModel(NoteSummary summary)
    {
        Summary = summary;
        RenameText = summary.DisplayName;
    }

    public NoteSummary Summary { get; }

    public string FilePath => Summary.FilePath;

    public string DisplayName => Summary.DisplayName;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText;
}
