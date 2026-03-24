namespace GroundNotes.ViewModels;

public sealed class ConfirmationDialogViewModel : ViewModelBase
{
    public ConfirmationDialogViewModel(string title, string heading, string message, string confirmButtonText)
    {
        Title = title;
        Heading = heading;
        Message = message;
        ConfirmButtonText = confirmButtonText;
    }

    public string Title { get; }

    public string Heading { get; }

    public string Message { get; }

    public string ConfirmButtonText { get; }
}
