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
    private async Task GenerateTitleSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAiEnabled)
        {
            StatusMessage = "AI is disabled in settings.";
            return;
        }

        if (!HasSelectedFolder || CurrentNote is null)
        {
            StatusMessage = "Open a note first.";
            return;
        }

        if (IsAiBusy || IsGeneratingTitleSuggestions)
        {
            StatusMessage = "AI is already processing a prompt.";
            return;
        }

        if (!UpdateCurrentNoteFromEditor())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentNote.Body)
            && string.IsNullOrWhiteSpace(CurrentNote.Title)
            && CurrentNote.Tags.Count == 0)
        {
            StatusMessage = "Add some note content first.";
            return;
        }

        IsGeneratingTitleSuggestions = true;
        StatusMessage = "Generating title suggestions...";

        try
        {
            var suggestions = await _aiTitleSuggestionService.GetSuggestionsAsync(
                CurrentNote,
                BuildAiSettings(),
                TitleSuggestionsContext,
                cancellationToken);
            TitleSuggestions = suggestions;
            IsTitleSuggestionsOpen = TitleSuggestions.Count > 0;
            StatusMessage = TitleSuggestions.Count > 0
                ? $"Generated {TitleSuggestions.Count} title suggestions."
                : "AI returned no usable title suggestions.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "AI request canceled.";
        }
        catch (AiServiceException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusMessage = "AI request failed.";
        }
        finally
        {
            IsGeneratingTitleSuggestions = false;
        }
    }

    [RelayCommand]
    private async Task ApplyTitleSuggestionAsync(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion) || CurrentNote is null || !HasSelectedFolder)
        {
            return;
        }

        if (!UpdateCurrentNoteFromEditor())
        {
            return;
        }

        var normalizedSuggestion = suggestion.Trim();
        if (string.Equals(CurrentNote.Title, normalizedSuggestion, StringComparison.Ordinal))
        {
            DismissTitleSuggestions(clearContext: true);
            return;
        }

        CancelScheduledSave();
        CurrentNote.Title = normalizedSuggestion;

        NoteDocument renamed;
        SuppressWatcher();
        using (BeginMutationScope())
        {
            renamed = await _noteMutationService.SaveAsync(NotesFolder, CurrentNote, CancellationToken.None);
        }

        DismissTitleSuggestions(clearContext: true);
        ApplyDocumentToEditor(renamed);
        SelectSummaryByPath(renamed.FilePath);
        StatusMessage = $"Renamed to {Path.GetFileNameWithoutExtension(renamed.FilePath)}";
    }

    [RelayCommand]
    private void CloseTitleSuggestions()
    {
        DismissTitleSuggestions(clearContext: false);
    }

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

    private void ClearTitleSuggestions() => DismissTitleSuggestions(clearContext: false);

    private void DismissTitleSuggestions(bool clearContext)
    {
        if (TitleSuggestions.Count == 0 && !IsTitleSuggestionsOpen)
        {
            if (clearContext)
            {
                TitleSuggestionsContext = string.Empty;
            }

            return;
        }

        TitleSuggestions = [];
        IsTitleSuggestionsOpen = false;

        if (clearContext)
        {
            TitleSuggestionsContext = string.Empty;
        }
    }
}
