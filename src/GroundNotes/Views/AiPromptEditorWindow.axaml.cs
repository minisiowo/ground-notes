using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GroundNotes.Models;
using GroundNotes.Styles;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class AiPromptEditorWindow : Window
{
    private readonly DialogWindowController _dialogController;

    public AiPromptEditorWindow()
    {
        InitializeComponent();
        _dialogController = new DialogWindowController(this, () => Close(false), () => NameTextBox);
        _dialogController.Attach();
        Closed += (_, _) => _dialogController.Detach();
        Opened += (_, _) => ThemeService.SyncScrollBarClassFromMainWindow(this);
    }

    public AiPromptEditorWindow(AiPromptEditorViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public AiPromptDefinition? Prompt { get; private set; }

    public Func<Task>? ShowKeyboardShortcutsHelpAsync { get; set; }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        _dialogController.OnCloseRequested();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AiPromptEditorViewModel vm || !vm.CanSave)
        {
            return;
        }

        Prompt = vm.BuildPrompt();
        Close(true);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogController.HandleEscape(e))
        {
            return;
        }

        if (ShowKeyboardShortcutsHelpAsync is not null && MainWindow.IsShowShortcutsHelpGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await ShowKeyboardShortcutsHelpAsync();
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            OnSaveClick(sender, new RoutedEventArgs());
        }
    }
}
