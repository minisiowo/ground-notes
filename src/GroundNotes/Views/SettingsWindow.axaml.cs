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

    public Action<SettingsDialogModel>? OnSettingsChanged { get; set; }

    public Func<Task>? ShowKeyboardShortcutsHelpAsync { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        _dialogController = new DialogWindowController(this, () => Close(), () => ThemeComboBox);
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
            _viewModel.PreviewRequested -= OnSettingsModelChanged;
        }

        _viewModel = viewModel;
        _viewModel.PreviewRequested += OnSettingsModelChanged;
        DataContext = _viewModel;
    }

    private void OnSettingsModelChanged(object? sender, SettingsDialogModel model)
    {
        OnSettingsChanged?.Invoke(model);
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnTitleBarPointerPressed(e);

    private void OnCloseRequested(object? sender, EventArgs e)
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
        }
    }
}
