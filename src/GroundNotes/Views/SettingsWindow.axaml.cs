using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class SettingsWindow : Window
{
    private readonly DialogWindowController _dialogController;
    private SettingsViewModel? _viewModel;
    private KeyboardShortcutBindingViewModel? _recordingShortcut;

    public Action<SettingsDialogModel>? OnSettingsChanged { get; set; }

    public SettingsPromptActions? PromptActions { get; set; }

    public IKeyboardShortcutService? KeyboardShortcuts { get; set; }

    public Func<Task>? ShowKeyboardShortcutsHelpAsync { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        _dialogController = new DialogWindowController(this, () => Close(), () => ThemeComboBox);
        _dialogController.Attach();
        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnShortcutCaptureKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        Closed += (_, _) =>
        {
            PointerMoved -= OnWindowPointerMoved;
            PointerExited -= OnWindowPointerExited;
            RemoveHandler(PointerPressedEvent, OnWindowPointerPressed);
            RemoveHandler(KeyDownEvent, OnShortcutCaptureKeyDown);
            _dialogController.Detach();
        };
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

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e) => _dialogController.OnWindowPointerMoved(e);

    private void OnWindowPointerExited(object? sender, PointerEventArgs e) => _dialogController.OnWindowPointerExited();

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) => _dialogController.OnWindowPointerPressed(e);

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        _dialogController.OnCloseRequested();
    }

    private void OnPromptSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is not null && sender is ListBox listBox)
        {
            _viewModel.SelectedPrompt = listBox.SelectedItem as AiPromptListItemViewModel;
        }
    }

    private async void OnPromptDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel?.SelectedPrompt is not { CanEdit: true } selectedPrompt)
        {
            return;
        }

        e.Handled = true;
        await EditPromptAsync(selectedPrompt.Definition, duplicate: false);
    }

    private void OnRecordShortcutClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: KeyboardShortcutBindingViewModel binding })
        {
            return;
        }

        _recordingShortcut?.CancelRecording();
        _recordingShortcut = binding;
        binding.BeginRecording();
    }

    private void OnShortcutCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (_recordingShortcut is null)
        {
            return;
        }

        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            _recordingShortcut.CancelRecording();
            _recordingShortcut = null;
            return;
        }

        if (e.Key == Key.None || IsModifierKey(e.Key))
        {
            return;
        }

        var binding = _recordingShortcut;
        _recordingShortcut = null;
        binding.Capture(
            e.Key.ToString(),
            e.KeyModifiers.HasFlag(KeyModifiers.Control),
            e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            e.KeyModifiers.HasFlag(KeyModifiers.Alt),
            e.KeyModifiers.HasFlag(KeyModifiers.Meta));
    }

    private static bool IsModifierKey(Key key)
    {
        var name = key.ToString();
        return name.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Shift", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Alt", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Win", StringComparison.OrdinalIgnoreCase)
               || name.Contains("Meta", StringComparison.OrdinalIgnoreCase);
    }

    private async void OnAddPromptClick(object? sender, RoutedEventArgs e)
    {
        await EditPromptAsync(null, duplicate: false);
    }

    private async void OnEditPromptClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPrompt is not { } selectedPrompt || selectedPrompt.IsBuiltIn)
        {
            return;
        }

        await EditPromptAsync(selectedPrompt.Definition, duplicate: false);
    }

    private async void OnDuplicatePromptClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPrompt is not { } selectedPrompt)
        {
            return;
        }

        await EditPromptAsync(selectedPrompt.Definition, duplicate: true);
    }

    private async void OnDeletePromptClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedPrompt is not { } selectedPrompt || PromptActions is null || selectedPrompt.IsBuiltIn)
        {
            return;
        }

        var confirmation = new ConfirmDeleteWindow(
            "Delete AI prompt",
            "Delete AI prompt?",
            $"Delete custom prompt '{selectedPrompt.Name}' permanently?",
            "Delete");
        if (!await confirmation.ShowDialog<bool>(this))
        {
            return;
        }

        var result = await PromptActions.DeletePromptAsync(selectedPrompt.Definition);
        _viewModel.SetAiPrompts(result.Prompts);
        OnSettingsChanged?.Invoke(_viewModel.BuildModel());
    }

    private async void OnReloadPromptsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || PromptActions is null)
        {
            return;
        }

        var prompts = await PromptActions.ReloadPromptsAsync();
        _viewModel.SetAiPrompts(prompts);
        OnSettingsChanged?.Invoke(_viewModel.BuildModel());
    }

    private async Task EditPromptAsync(AiPromptDefinition? prompt, bool duplicate)
    {
        if (_viewModel is null || PromptActions is null)
        {
            return;
        }

        var editorViewModel = new AiPromptEditorViewModel(
            prompt,
            _viewModel.DefaultModel,
            _viewModel.DefaultReasoningEffort,
            duplicate);
        var dialog = new AiPromptEditorWindow(editorViewModel)
        {
            ShowKeyboardShortcutsHelpAsync = ShowKeyboardShortcutsHelpAsync,
            KeyboardShortcuts = KeyboardShortcuts
        };

        var saved = await dialog.ShowDialog<bool>(this);
        if (!saved || dialog.Prompt is null)
        {
            return;
        }

        var idConflict = _viewModel.PromptItems.FirstOrDefault(item =>
            string.Equals(item.Id, dialog.Prompt.Id, StringComparison.OrdinalIgnoreCase)
            && (prompt is null
                || duplicate
                || !string.Equals(item.Id, prompt.Id, StringComparison.OrdinalIgnoreCase)));
        if (idConflict is not null)
        {
            var confirmation = new ConfirmDeleteWindow(
                "Replace AI prompt",
                "Replace AI prompt?",
                $"A prompt with ID '{dialog.Prompt.Id}' already exists. Replace it with '{dialog.Prompt.Name}'?",
                "Replace");
            if (!await confirmation.ShowDialog<bool>(this))
            {
                return;
            }
        }

        var saveResult = await PromptActions.SavePromptAsync(dialog.Prompt);
        if (!saveResult.Succeeded)
        {
            _viewModel.SetAiPrompts(saveResult.Prompts);
            return;
        }

        var prompts = saveResult.Prompts;
        if (prompt is not null
            && !duplicate
            && !prompt.IsBuiltIn
            && !string.Equals(prompt.Id, dialog.Prompt.Id, StringComparison.OrdinalIgnoreCase))
        {
            var deleteResult = await PromptActions.DeletePromptAsync(prompt);
            prompts = deleteResult.Prompts;
        }

        _viewModel.SetAiPrompts(prompts);
        OnSettingsChanged?.Invoke(_viewModel.BuildModel());
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dialogController.HandleEscape(e))
        {
            return;
        }

        if (ShowKeyboardShortcutsHelpAsync is not null
            && (KeyboardShortcuts?.Matches(KeyboardShortcutActionIds.ShowShortcuts, e.Key, e.KeyModifiers)
                ?? MainWindow.IsShowShortcutsHelpGesture(e.Key, e.KeyModifiers)))
        {
            e.Handled = true;
            await ShowKeyboardShortcutsHelpAsync();
        }
    }
}
