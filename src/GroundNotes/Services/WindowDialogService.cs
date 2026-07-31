using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GroundNotes.Models;
using GroundNotes.ViewModels;
using GroundNotes.Views;

namespace GroundNotes.Services;

public sealed class WindowDialogService : IWorkspaceDialogService
{
    private const double ChatWindowDefaultWidth = 500;
    private const double ChatWindowDefaultHeight = 600;

    private readonly Window _owner;
    private readonly IEditorLayoutState _editorLayoutState;
    private readonly IKeyboardShortcutService _keyboardShortcutService;

    public WindowDialogService(
        Window owner,
        IEditorLayoutState editorLayoutState,
        IKeyboardShortcutService keyboardShortcutService)
    {
        _owner = owner;
        _editorLayoutState = editorLayoutState;
        _keyboardShortcutService = keyboardShortcutService;
    }

    public async Task<string?> PickFolderAsync()
    {
        var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose notes folder"
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    public async Task<bool> ConfirmDeleteAsync(string noteName)
    {
        var dialog = new ConfirmDeleteWindow(noteName);
        return await dialog.ShowDialog<bool>(_owner);
    }

    public async Task<string?> PromptCreateTagFolderAsync()
    {
        var dialog = TagFolderDialogWindow.Create();
        return await dialog.ShowDialog<string?>(_owner);
    }

    public async Task<string?> PromptRenameTagFolderAsync(string currentPath)
    {
        var dialog = TagFolderDialogWindow.Rename(currentPath);
        return await dialog.ShowDialog<string?>(_owner);
    }

    public async Task<string?> ChooseTagFolderDestinationAsync(IReadOnlyList<string> folderPaths)
    {
        if (folderPaths.Count == 0)
        {
            return null;
        }

        var dialog = TagFolderDialogWindow.ChooseDestination(folderPaths);
        return await dialog.ShowDialog<string?>(_owner);
    }

    public async Task<bool> ConfirmDeleteTagFolderAsync(string folderPath)
    {
        var dialog = new ConfirmDeleteWindow(
            "Delete tag folder",
            "Delete tag folder?",
            $"Delete '{folderPath}'? Notes will remain, but tags in this folder will be removed from them.",
            "Delete");
        return await dialog.ShowDialog<bool>(_owner);
    }

    public async Task<bool> ConfirmDeleteNotesAsync(IReadOnlyList<string> noteNames)
    {
        if (noteNames.Count == 0)
        {
            return false;
        }

        var noteLabel = noteNames.Count == 1 ? "note" : "notes";
        var names = string.Join(Environment.NewLine, noteNames.Select(name => $"- {name}"));
        var dialog = new ConfirmDeleteWindow(
            "Delete notes",
            $"Delete {noteNames.Count} {noteLabel}?",
            $"The selected {noteLabel} will be permanently deleted:{Environment.NewLine}{Environment.NewLine}{names}",
            "Delete")
        {
            Height = 280
        };
        return await dialog.ShowDialog<bool>(_owner);
    }

    public async Task<bool> ConfirmDiscardInvalidDraftAsync()
    {
        var dialog = new ConfirmDeleteWindow(
            "Discard invalid draft",
            "Discard invalid draft?",
            "This YAML draft is invalid and has not been saved. Discard it and continue?",
            "Discard");
        return await dialog.ShowDialog<bool>(_owner);
    }

    public Task ShowChatAsync(ChatViewModel model)
    {
        var dialog = new ChatWindow
        {
            DataContext = model,
            Width = ChatWindowDefaultWidth,
            Height = ChatWindowDefaultHeight
        };
        dialog.ShowKeyboardShortcutsHelpAsync = () => ShowKeyboardShortcutsHelpAsync(dialog);
        dialog.KeyboardShortcuts = _keyboardShortcutService;
        dialog.SetEditorLayoutState(_editorLayoutState);

        dialog.Show(_owner);
        return Task.CompletedTask;
    }

    public async Task ShowKeyboardShortcutsHelpAsync(Window? owner = null)
    {
        var dialog = new KeyboardShortcutsHelpWindow
        {
            DataContext = new KeyboardShortcutsHelpDisplayModel(_keyboardShortcutService.BuildHelpSections())
        };
        await dialog.ShowDialog(owner ?? _owner);
    }

    public async Task ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> onChange, SettingsPromptActions promptActions, SettingsSlashCommandActions? slashCommandActions = null)
    {
        var dialog = new SettingsWindow(model)
        {
            OnSettingsChanged = onChange,
            PromptActions = promptActions,
            SlashCommandActions = slashCommandActions,
            KeyboardShortcuts = _keyboardShortcutService
        };

        dialog.ShowKeyboardShortcutsHelpAsync = () => ShowKeyboardShortcutsHelpAsync(dialog);

        await dialog.ShowDialog(_owner);
    }
}
