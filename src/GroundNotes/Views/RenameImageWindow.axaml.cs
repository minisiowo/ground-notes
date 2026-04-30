using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GroundNotes.Views;

public partial class RenameImageWindow : Window
{
    private readonly DialogWindowController _dialogController;

    public RenameImageWindow()
    {
        InitializeComponent();
        _dialogController = new DialogWindowController(this, () => Close(null), () => FileNameTextBox);
        _dialogController.Attach();
        Closed += (_, _) => _dialogController.Detach();
    }

    public RenameImageWindow(string currentFileName) : this()
    {
        FileNameTextBox.Text = currentFileName;
        FileNameTextBox.SelectAll();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnTitleBarCloseRequested(object? sender, EventArgs e) => _dialogController.OnCloseRequested();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => _dialogController.OnCloseRequested();

    private void OnRenameClick(object? sender, RoutedEventArgs e) => Close(FileNameTextBox.Text?.Trim());

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogController.HandleEscape(e))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Close(FileNameTextBox.Text?.Trim());
        }
    }
}
