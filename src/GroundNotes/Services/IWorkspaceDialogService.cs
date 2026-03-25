using Avalonia.Controls;
using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Services;

public interface IWorkspaceDialogService
{
    Task<string?> PickFolderAsync();

    Task<bool> ConfirmDeleteAsync(string noteName);

    Task<bool> ConfirmDiscardInvalidDraftAsync();

    Task ShowChatAsync(ChatViewModel model);

    Task ShowKeyboardShortcutsHelpAsync(Window? owner = null);

    Task<SettingsDialogModel?> ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> previewAsync);
}
