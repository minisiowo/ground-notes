using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Views;

public partial class ConfirmDeleteWindow : Window
{
    private readonly DialogWindowController _dialogController;

    public ConfirmDeleteWindow()
    {
        InitializeComponent();
        DataContext = new ConfirmationDialogViewModel("Delete note", string.Empty);
        _dialogController = new DialogWindowController(this, () => Close(false), () => this.FindControl<Button>("DeleteButton"));
        _dialogController.Attach();
        Closed += (_, _) => _dialogController.Detach();
    }

    public ConfirmDeleteWindow(string noteName) : this()
    {
        DataContext = new ConfirmationDialogViewModel("Delete note", $"Delete '{noteName}' permanently?");
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _dialogController.OnCloseRequested();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogController.HandleEscape(e))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Close(true);
        }
    }
}
