using Avalonia.Controls;
using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Services;

public interface IWorkspaceDialogService
{
    Task<string?> PickFolderAsync();

    Task<bool> ConfirmDeleteAsync(string noteName);

    Task<string?> PromptCreateTagFolderAsync();

    Task<string?> PromptRenameTagFolderAsync(string currentPath);

    Task<string?> ChooseTagFolderDestinationAsync(IReadOnlyList<string> folderPaths);

    Task<bool> ConfirmDeleteTagFolderAsync(string folderPath);

    Task<bool> ConfirmDeleteNotesAsync(IReadOnlyList<string> noteNames);

    Task<bool> ConfirmDiscardInvalidDraftAsync();

    Task ShowChatAsync(ChatViewModel model);

    Task ShowKeyboardShortcutsHelpAsync(Window? owner = null);

    Task ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> onChange, SettingsPromptActions promptActions, SettingsSlashCommandActions? slashCommandActions = null);
}
