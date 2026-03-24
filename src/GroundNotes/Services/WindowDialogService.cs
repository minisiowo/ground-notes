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

    public WindowDialogService(Window owner, IEditorLayoutState editorLayoutState)
    {
        _owner = owner;
        _editorLayoutState = editorLayoutState;
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
        dialog.SetEditorLayoutState(_editorLayoutState);

        dialog.Show(_owner);
        return Task.CompletedTask;
    }

    public async Task<SettingsDialogModel?> ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> previewAsync)
    {
        var dialog = new SettingsWindow(model)
        {
            PreviewSettingsAsync = previewAsync is null
                ? null
                : value =>
                {
                    previewAsync(value);
                    return Task.CompletedTask;
                }
        };

        return await dialog.ShowDialog<SettingsDialogModel?>(_owner);
    }
}
