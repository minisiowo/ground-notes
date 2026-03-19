namespace QuickNotesTxt.ViewModels;

public sealed class ConfirmationDialogViewModel : ViewModelBase
{
    public ConfirmationDialogViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    public string Title { get; }

    public string Message { get; }
}
