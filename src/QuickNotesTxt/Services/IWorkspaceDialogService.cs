using QuickNotesTxt.Models;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Services;

public interface IWorkspaceDialogService
{
    Task<string?> PickFolderAsync();

    Task<bool> ConfirmDeleteAsync(string noteName);

    Task ShowChatAsync(ChatViewModel model);

    Task<SettingsDialogModel?> ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> previewAsync);
}
