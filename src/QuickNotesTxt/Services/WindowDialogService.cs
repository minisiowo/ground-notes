using Avalonia.Controls;
using Avalonia.Platform.Storage;
using QuickNotesTxt.Models;
using QuickNotesTxt.ViewModels;
using QuickNotesTxt.Views;

namespace QuickNotesTxt.Services;

public sealed class WindowDialogService : IWorkspaceDialogService
{
    private const double ChatWindowDefaultWidth = 700;
    private const double ChatWindowMinWidth = 460;
    private const double ChatWindowMaxWidth = 820;

    private readonly Window _owner;

    public WindowDialogService(Window owner)
    {
        _owner = owner;
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

    public Task ShowChatAsync(ChatViewModel model)
    {
        var targetWidth = Math.Clamp(_owner.Bounds.Width * 0.6, ChatWindowMinWidth, ChatWindowMaxWidth);
        if (_owner.Bounds.Width <= 0)
        {
            targetWidth = ChatWindowDefaultWidth;
        }

        var dialog = new ChatWindow
        {
            DataContext = model,
            Width = targetWidth
        };

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
