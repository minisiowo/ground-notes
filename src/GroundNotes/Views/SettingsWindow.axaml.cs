using Avalonia.Controls;
using Avalonia.Input;
using GroundNotes.Models;
using GroundNotes.Styles;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class SettingsWindow : Window
{
    private readonly DialogWindowController _dialogController;
    private SettingsViewModel? _viewModel;

    public Func<SettingsDialogModel, Task>? PreviewSettingsAsync { get; set; }

    public Func<Task>? ShowKeyboardShortcutsHelpAsync { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        _dialogController = new DialogWindowController(this, () => Close(null), () => ThemeComboBox);
        _dialogController.Attach();
        Closed += (_, _) => _dialogController.Detach();
        Opened += (_, _) => ThemeService.SyncScrollBarClassFromMainWindow(this);
    }

    public SettingsWindow(SettingsDialogModel model) : this()
    {
        BindViewModel(new SettingsViewModel(model));
    }

    private void BindViewModel(SettingsViewModel viewModel)
    {
        if (_viewModel is not null)
        {
            _viewModel.PreviewRequested -= OnPreviewRequested;
        }

        _viewModel = viewModel;
        _viewModel.PreviewRequested += OnPreviewRequested;
        DataContext = _viewModel;
    }

    private async void OnPreviewRequested(object? sender, SettingsDialogModel model)
    {
        if (PreviewSettingsAsync is null)
        {
            return;
        }

        await PreviewSettingsAsync(model);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(_viewModel?.BuildModel());
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        _dialogController.OnCloseRequested();
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dialogController.OnCloseRequested();
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

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnSaveClick(sender, new Avalonia.Interactivity.RoutedEventArgs());
        }
    }
}
