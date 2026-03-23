using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Services;

public interface IWorkspaceDialogService
{
    Task<string?> PickFolderAsync();

    Task<bool> ConfirmDeleteAsync(string noteName);

    Task ShowChatAsync(ChatViewModel model);

    Task<SettingsDialogModel?> ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> previewAsync);
}
