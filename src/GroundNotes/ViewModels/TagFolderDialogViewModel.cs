using CommunityToolkit.Mvvm.ComponentModel;

namespace GroundNotes.ViewModels;

public sealed partial class TagFolderDialogViewModel : ViewModelBase
{
    public TagFolderDialogViewModel(
        string title,
        string heading,
        string message,
        string confirmButtonText,
        string? value = null,
        IReadOnlyList<string>? folderPaths = null)
    {
        Title = title;
        Heading = heading;
        Message = message;
        ConfirmButtonText = confirmButtonText;
        FolderPaths = folderPaths ?? [];
        IsFolderChoice = folderPaths is not null;
        _value = value ?? FolderPaths.FirstOrDefault();
    }

    public string Title { get; }

    public string Heading { get; }

    public string Message { get; }

    public string ConfirmButtonText { get; }

    public IReadOnlyList<string> FolderPaths { get; }

    public bool IsFolderChoice { get; }

    public bool IsTextInput => !IsFolderChoice;

    [ObservableProperty]
    private string? _value;
}
