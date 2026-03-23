using System.Collections.ObjectModel;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;

namespace GroundNotes.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    [RelayCommand]
    private async Task OpenChatAsync()
    {
        if (!HasSelectedFolder)
        {
            StatusMessage = "Choose a folder first.";
            return;
        }

        var initialNotes = SelectedNoteSummary is not null ? new[] { SelectedNoteSummary } : null;
        var chatVm = _chatViewModelFactory.Create(
            NotesFolder,
            SelectedAiModel,
            () => _allNotes,
            SelectedNoteSummary,
            initialNotes);

        await _workspaceDialogService.ShowChatAsync(chatVm);
    }

    [RelayCommand]
    private async Task ReloadAiPromptsAsync()
    {
        var promptLoad = await LoadAiPromptsAsync();
        StatusMessage = BuildPromptLoadStatus(promptLoad);
    }

    public async Task<string?> RunAiPromptAsync(AiPromptDefinition prompt, string selectedText, CancellationToken cancellationToken = default)
    {
        if (!IsAiEnabled)
        {
            StatusMessage = "AI is disabled in settings.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            StatusMessage = "Select text first.";
            return null;
        }

        if (IsAiBusy)
        {
            StatusMessage = "AI is already processing a prompt.";
            return null;
        }

        IsAiBusy = true;
        StatusMessage = $"Running {prompt.Name}...";

        try
        {
            var result = await _aiTextActionService.RunPromptAsync(prompt, selectedText, BuildAiSettings(), cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "AI request canceled.";
            return null;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return null;
        }
        catch (HttpRequestException)
        {
            StatusMessage = "AI request failed.";
            return null;
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    private void ApplyAiSettings(AiSettings settings)
    {
        var normalized = AiSettings.Normalize(settings);
        OpenAiApiKey = normalized.ApiKey;
        SelectedAiModel = normalized.DefaultModel;
        OpenAiProjectId = normalized.ProjectId;
        OpenAiOrganizationId = normalized.OrganizationId;
        IsAiEnabled = normalized.IsEnabled;
    }

    private AiSettings BuildAiSettings()
    {
        return AiSettings.Normalize(OpenAiApiKey, SelectedAiModel, IsAiEnabled, OpenAiProjectId, OpenAiOrganizationId);
    }

    private static string BuildPromptLoadStatus(AiPromptCatalogLoadResult promptLoad, string defaultMessage = "No AI prompts were found.")
    {
        if (promptLoad.Warnings.Count > 0)
        {
            var promptMessage = promptLoad.Prompts.Count > 0
                ? $"Loaded {promptLoad.Prompts.Count} AI prompts."
                : defaultMessage;

            return $"{promptMessage} {promptLoad.Warnings.Count} warning(s).";
        }

        return promptLoad.Prompts.Count > 0
            ? $"Loaded {promptLoad.Prompts.Count} AI prompts."
            : defaultMessage;
    }

    private async Task<AiPromptCatalogLoadResult> LoadAiPromptsAsync()
    {
        var result = await _aiPromptCatalogService.LoadPromptsAsync(HasSelectedFolder ? NotesFolder : null);
        AiPrompts = result.Prompts;
        return result;
    }
}
