using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Views;

public partial class KeyboardShortcutsHelpWindow : Window
{
    private readonly DialogWindowController _dialogController;

    public KeyboardShortcutsHelpWindow()
    {
        InitializeComponent();
        DataContext = new KeyboardShortcutsHelpDisplayModel();
        _dialogController = new DialogWindowController(this, () => Close());
        _dialogController.Attach();
        Closed += (_, _) => _dialogController.Detach();
        Opened += (_, _) => ThemeService.SyncScrollBarClassFromMainWindow(this);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnCloseRequested(object? sender, EventArgs e) => _dialogController.OnCloseRequested();

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogController.HandleEscape(e))
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Close();
        }
    }
}
