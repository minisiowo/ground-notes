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
    public async Task CommitRenameAsync(NoteListItemViewModel? noteItem)
    {
        if (noteItem is null || !noteItem.IsRenaming || !HasSelectedFolder)
        {
            return;
        }

        var newName = noteItem.RenameText.Trim();
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, noteItem.DisplayName, StringComparison.Ordinal))
        {
            CancelRename(noteItem);
            return;
        }

        CancelScheduledSave();

        NoteDocument? document;
        if (CurrentNote is not null && string.Equals(CurrentNote.FilePath, noteItem.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            if (!UpdateCurrentNoteFromEditor())
            {
                CancelRename(noteItem);
                return;
            }

            document = CurrentNote;
        }
        else
        {
            document = await _notesRepository.LoadNoteAsync(noteItem.FilePath);
        }

        if (document is null)
        {
            CancelRename(noteItem);
            await RefreshFromDiskAsync();
            return;
        }

        SuppressWatcher();
        document.Title = newName;
        NoteDocument renamed;
        using (BeginMutationScope())
        {
            renamed = await _noteMutationService.SaveAsync(NotesFolder, document, CancellationToken.None);
        }
        CancelRename(noteItem);
        StatusMessage = $"Renamed to {Path.GetFileNameWithoutExtension(renamed.FilePath)}";
    }

    public void CancelRename(NoteListItemViewModel? noteItem)
    {
        if (noteItem is null)
        {
            return;
        }

        noteItem.IsRenaming = false;
        noteItem.RenameText = noteItem.DisplayName;
    }

    private async Task RefreshFromDiskAsync()
    {
        DismissTitleSuggestions(clearContext: true);

        if (!HasSelectedFolder)
        {
            return;
        }

        var summaries = await _notesRepository.LoadSummariesAsync(NotesFolder);
        _allNotes.Clear();
        foreach (var summary in summaries)
        {
            _allNotes.Add(summary);
        }

        RefreshCalendarNoteDates();
        RefreshCalendarDays();
        RefreshAvailableTags();
        RefreshVisibleNotes();

        if (CurrentNote is null)
        {
            return;
        }

        var matchingSummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));
        if (matchingSummary is null)
        {
            ClearEditor();
            StatusMessage = "The current note was removed.";
            return;
        }

        if (HasUnsavedChanges)
        {
            HasConflict = true;
            StatusMessage = "This note changed on disk while you had local edits. Reselect it to reload.";
            return;
        }

        var reloaded = await _notesRepository.LoadNoteAsync(matchingSummary.FilePath);
        if (reloaded is not null)
        {
            ApplyDocumentToEditor(reloaded);
            SelectSummaryByPath(reloaded.FilePath);
        }
    }

    private async Task LoadSelectedNoteAsync(string filePath)
    {
        if (!await CanLeaveCurrentEditorStateAsync(filePath))
        {
            return;
        }

        var note = await _notesRepository.LoadNoteAsync(filePath);
        if (note is null)
        {
            return;
        }

        CancelInlineRename();
        ApplyDocumentToEditor(note);
        HasConflict = false;
        StatusMessage = "Ready.";
    }

    private void ApplyDocumentToEditor(NoteDocument note)
    {
        DismissTitleSuggestions(clearContext: true);
        _isApplyingSelection = true;
        try
        {
            _hasInvalidYamlFrontMatter = false;
            CurrentNote = note;
            EditorTitle = note.Title;
            EditorTags = string.Join(", ", note.Tags);
            EditorBody = BuildEditorText(note);
            HasUnsavedChanges = false;
            LastSavedText = FormatLastSavedText(note.UpdatedAt);
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private void ClearEditor()
    {
        CancelScheduledSave();
        CancelInlineRename();
        DismissTitleSuggestions(clearContext: true);

        _isApplyingSelection = true;
        try
        {
            _hasInvalidYamlFrontMatter = false;
            CurrentNote = null;
            SelectedNoteSummary = null;
            SelectedVisibleNote = null;
            EditorTitle = string.Empty;
            EditorTags = string.Empty;
            EditorBody = string.Empty;
            HasUnsavedChanges = false;
            HasConflict = false;
            LastSavedText = "GroundNotes";
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private bool EnsureDraftExists(string incomingBody)
    {
        if (CurrentNote is not null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(incomingBody))
        {
            return false;
        }

        var draft = _notesRepository.CreateDraftNote(NotesFolder, DateTimeOffset.Now);
        CurrentNote = draft;

        _isApplyingSelection = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(EditorTitle))
            {
                draft.Title = EditorTitle.Trim();
            }
            else
            {
                EditorTitle = draft.Title;
            }

            if (!string.IsNullOrWhiteSpace(EditorTags))
            {
                draft.Tags = ParseTags(EditorTags);
            }
            else
            {
                EditorTags = string.Empty;
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }

        HasUnsavedChanges = true;
        return true;
    }

    private bool UpdateCurrentNoteFromEditor()
    {
        if (CurrentNote is null)
        {
            return false;
        }

        if (ShowYamlFrontMatterInEditor)
        {
            var hadInvalidYamlFrontMatter = _hasInvalidYamlFrontMatter;
            if (!NotesRepository.TryParseEditableDocumentText(CurrentNote, EditorBody, out var parsedDocument, out var errorMessage))
            {
                _hasInvalidYamlFrontMatter = true;
                HasUnsavedChanges = true;
                HasConflict = false;
                StatusMessage = errorMessage;
                return false;
            }

            _hasInvalidYamlFrontMatter = false;
            CurrentNote = parsedDocument;

            _isApplyingSelection = true;
            try
            {
                EditorTitle = parsedDocument.Title;
                EditorTags = string.Join(", ", parsedDocument.Tags);
            }
            finally
            {
                _isApplyingSelection = false;
            }

            if (hadInvalidYamlFrontMatter && StatusMessage.StartsWith("Invalid YAML frontmatter", StringComparison.Ordinal))
            {
                StatusMessage = "Ready.";
            }
        }
        else
        {
            _hasInvalidYamlFrontMatter = false;
            CurrentNote.Title = string.IsNullOrWhiteSpace(EditorTitle) ? CurrentNote.OriginalTitle : EditorTitle.Trim();
            CurrentNote.Body = EditorBody;
            CurrentNote.Tags = ParseTags(EditorTags);
        }

        HasUnsavedChanges = true;
        HasConflict = false;
        return true;
    }

    private void ScheduleSave()
    {
        CancelScheduledSave();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        _ = SaveAfterDebounceAsync(token);
    }

    private async Task SaveAfterDebounceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            await SaveCurrentNoteAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ClearTransientStatusOnEdit()
    {
        if (IsTransientFooterStatus(StatusMessage))
        {
            StatusMessage = "Ready.";
        }
    }

    private static bool IsTransientFooterStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status == "New note ready."
            || status == "Delete canceled."
            || status == "Add some note content first."
            || status == "AI settings saved."
            || status == "AI request canceled."
            || status == "AI request failed."
            || status == "AI is disabled in settings."
            || status == "AI is already processing a prompt."
            || status == "Open a note first."
            || status == "Select text first."
            || status.StartsWith("Generated ", StringComparison.Ordinal)
            || status.StartsWith("Deleted ", StringComparison.Ordinal)
            || status.StartsWith("Renamed to ", StringComparison.Ordinal)
            || status.StartsWith("Editor font size: ", StringComparison.Ordinal)
            || status.StartsWith("UI font size: ", StringComparison.Ordinal)
            || status.StartsWith("Loaded ", StringComparison.Ordinal)
            || status.StartsWith("Running ", StringComparison.Ordinal)
            || status.EndsWith(" applied.", StringComparison.Ordinal);
    }
    private async Task SaveCurrentNoteAsync(CancellationToken cancellationToken)
    {
        if (CurrentNote is null || !HasSelectedFolder)
        {
            return;
        }

        if (_hasInvalidYamlFrontMatter)
        {
            StatusMessage = "Invalid YAML frontmatter. Fix it before saving.";
            return;
        }

        if (ShouldDeleteEmptyAutoCreatedNote(CurrentNote))
        {
            SuppressWatcher();
            await _notesRepository.DeleteNoteIfExistsAsync(CurrentNote.FilePath, cancellationToken);
            RemoveSummary(CurrentNote.FilePath);
            RefreshCalendarNoteDates();
            RefreshCalendarDays();
            RefreshAvailableTags();
            RefreshVisibleNotes();
            ClearEditor();
            StatusMessage = "Empty draft discarded.";
            return;
        }

        var saved = await PersistNoteAsync(CurrentNote, cancellationToken);
        CurrentNote = saved;
        HasUnsavedChanges = false;
        LastSavedText = FormatLastSavedText(saved.UpdatedAt);
    }

    private async Task<NoteDocument> PersistNoteAsync(NoteDocument document, CancellationToken cancellationToken)
    {
        SuppressWatcher();
        NoteDocument saved;
        using (BeginMutationScope())
        {
            saved = await _noteMutationService.SaveAsync(NotesFolder, document, cancellationToken);
        }
        return saved;
    }

    private async Task FlushPendingSaveAsync()
    {
        if (!HasUnsavedChanges || CurrentNote is null)
        {
            return;
        }

        CancelScheduledSave();
        await SaveCurrentNoteAsync(CancellationToken.None);
    }

    private void CancelScheduledSave()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        _saveCts = null;
    }

    private void CancelInlineRename()
    {
        foreach (var note in VisibleNotes.Where(note => note.IsRenaming))
        {
            note.IsRenaming = false;
            note.RenameText = note.DisplayName;
        }
    }

    private static bool ShouldDeleteEmptyAutoCreatedNote(NoteDocument note)
    {
        return note.IsAutoCreated && string.IsNullOrWhiteSpace(note.Body) && note.Tags.Count == 0 && string.Equals(note.Title, note.OriginalTitle, StringComparison.Ordinal);
    }

    private void RefreshVisibleNotes()
    {
        // Replacing VisibleNotes clears the list's SelectedItem before we can re-sync. Without guarding,
        // OnSelectedVisibleNoteChanged(null) clears SelectedNoteSummary; the restore below then skips
        // (SelectedNoteSummary is null) and the sidebar can auto-select another note, loading it over the editor.
        _isApplyingSelection = true;
        try
        {
            var effectiveTag = string.Equals(SelectedTag, AllTagsFilter, StringComparison.Ordinal) ? null : SelectedTag;
            var currentItems = VisibleNotes.ToDictionary(note => note.FilePath, StringComparer.OrdinalIgnoreCase);
            var nextNotes = _notesRepository.QueryNotes(_allNotes, SearchText, effectiveTag, SelectedCalendarDate, SelectedSortOption);
            var nextItems = nextNotes.Select(note =>
            {
                if (!currentItems.TryGetValue(note.FilePath, out var existing))
                {
                    return new NoteListItemViewModel(note);
                }

                if (!string.Equals(existing.DisplayName, note.DisplayName, StringComparison.Ordinal))
                {
                    existing.RenameText = note.DisplayName;
                }

                return existing;
            });

            VisibleNotes = new ObservableCollection<NoteListItemViewModel>(nextItems);

            if (SelectedNoteSummary is not null)
            {
                SelectedVisibleNote = VisibleNotes.FirstOrDefault(note => string.Equals(note.FilePath, SelectedNoteSummary.FilePath, StringComparison.OrdinalIgnoreCase));
            }

            if (IsNotePickerOpen)
            {
                RefreshNotePickerResults();
            }
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private void RefreshNotePickerResults()
    {
        if (!IsNotePickerOpen)
        {
            return;
        }

        var allResults = _noteSearchService.Search(NotePickerQuery, maxResults: 0);
        NotePickerTotalMatchCount = allResults.Count;

        var results = NotePickerTotalMatchCount <= NotePickerResultLimit
            ? allResults
            : allResults.Take(NotePickerResultLimit).ToList();

        NotePickerResults = new ObservableCollection<NoteSummary>(results);

        if (NotePickerResults.Count == 0)
        {
            SelectedNotePickerSummary = null;
            return;
        }

        if (SelectedNotePickerSummary is not null)
        {
            var matching = NotePickerResults.FirstOrDefault(note => string.Equals(note.FilePath, SelectedNotePickerSummary.FilePath, StringComparison.OrdinalIgnoreCase));
            if (matching is not null)
            {
                SelectedNotePickerSummary = matching;
                return;
            }
        }

        var currentSelection = CurrentNote is null
            ? null
            : NotePickerResults.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));

        SelectedNotePickerSummary = currentSelection ?? NotePickerResults[0];
    }

    private const string AllTagsFilter = "All";

    private void RefreshAvailableTags()
    {
        var tags = _allNotes
            .SelectMany(note => note.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        tags.Insert(0, AllTagsFilter);
        AvailableTags = new ObservableCollection<string>(tags);
    }

    private void ReplaceSummary(string previousPath, NoteSummary summary)
    {
        RemoveSummary(previousPath, refreshCalendarNoteDates: false);
        _allNotes.Add(summary);
        RefreshCalendarNoteDates();
    }

    private void RemoveSummary(string filePath, bool refreshCalendarNoteDates = true)
    {
        var existing = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _allNotes.Remove(existing);
            if (refreshCalendarNoteDates)
            {
                RefreshCalendarNoteDates();
            }
        }
    }

    private void SelectSummaryByPath(string filePath)
    {
        var matching = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (matching is null)
        {
            return;
        }

        _isApplyingSelection = true;
        try
        {
            SelectedNoteSummary = matching;
            SelectedVisibleNote = VisibleNotes.FirstOrDefault(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    [RelayCommand]
    private async Task ToggleYamlFrontMatterVisibilityAsync()
    {
        if (!HasSelectedFolder)
        {
            StatusMessage = "Choose a folder first.";
            return;
        }

        var nextValue = !ShowYamlFrontMatterInEditor;
        if (CurrentNote is not null)
        {
            if (!UpdateCurrentNoteFromEditor())
            {
                return;
            }

            _isApplyingSelection = true;
            try
            {
                ShowYamlFrontMatterInEditor = nextValue;
                EditorBody = BuildEditorText(CurrentNote);
            }
            finally
            {
                _isApplyingSelection = false;
            }
        }
        else
        {
            ShowYamlFrontMatterInEditor = nextValue;
        }

        await PersistSettingsAsync(settings => settings with { ShowYamlFrontMatterInEditor = ShowYamlFrontMatterInEditor });
        StatusMessage = ShowYamlFrontMatterInEditor
            ? "YAML frontmatter visible."
            : "YAML frontmatter hidden.";
    }

    private static NoteSummary BuildSummary(NoteDocument document)
    {
        return NoteSummary.FromDocument(document);
    }

    private void SuppressWatcher()
    {
        _suppressWatcherUntil = DateTimeOffset.UtcNow.AddMilliseconds(900);
    }

    private static List<string> ParseTags(string input)
    {
        return input
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FormatLastSavedText(DateTimeOffset updatedAt)
    {
        return $"Last saved: {updatedAt.LocalDateTime:yyyy-MM-dd HH:mm}";
    }

    private string BuildEditorText(NoteDocument note)
    {
        return ShowYamlFrontMatterInEditor
            ? NotesRepository.BuildEditableDocumentText(note)
            : note.Body;
    }

    private bool IsUnsavedInvalidYamlDraft()
    {
        if (!_hasInvalidYamlFrontMatter || CurrentNote is null)
        {
            return false;
        }

        if (!CurrentNote.IsAutoCreated)
        {
            return false;
        }

        return !_allNotes.Any(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> CanLeaveCurrentEditorStateAsync(string? nextFilePath = null)
    {
        if (!_hasInvalidYamlFrontMatter || CurrentNote is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(nextFilePath)
            && string.Equals(CurrentNote.FilePath, nextFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsUnsavedInvalidYamlDraft())
        {
            var shouldDiscard = await _workspaceDialogService.ConfirmDiscardInvalidDraftAsync();
            if (!shouldDiscard)
            {
                RestoreSelectionAfterBlockedLeave();
                StatusMessage = "Invalid YAML frontmatter. Fix it or discard the draft to continue.";
                return false;
            }

            ClearEditor();
            StatusMessage = "Invalid YAML draft discarded.";
            return true;
        }

        RestoreSelectionAfterBlockedLeave();
        StatusMessage = "Invalid YAML frontmatter. Fix it before switching notes.";
        return false;
    }

    private void RestoreSelectionAfterBlockedLeave()
    {
        if (CurrentNote is null)
        {
            return;
        }

        if (_allNotes.Any(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            SelectSummaryByPath(CurrentNote.FilePath);
            return;
        }

        _isApplyingSelection = true;
        try
        {
            SelectedNoteSummary = null;
            SelectedVisibleNote = null;
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private void OnNoteChanged(object? sender, NoteFileChangedEventArgs e)
    {
        if (DateTimeOffset.UtcNow < _suppressWatcherUntil)
        {
            return;
        }

        var changes = e.Changes.ToArray();
        _ = Dispatcher.UIThread.InvokeAsync(async () => await ApplyExternalChangesAsync(changes));
    }

    private void OnNoteMutated(object? sender, NoteMutationEventArgs e)
    {
        if (!HasSelectedFolder)
        {
            return;
        }

        if (e.Kind == NoteMutationKind.Deleted)
        {
            ApplyDeletedNote(e.PreviousPath, refreshCollections: true);
            return;
        }

        if (e.Document is null)
        {
            return;
        }

        ReplaceSummary(e.PreviousPath, BuildSummary(e.Document));
        RefreshCalendarDays();
        RefreshAvailableTags();
        RefreshVisibleNotes();

        if (CurrentNote is null)
        {
            return;
        }

        var touchesCurrentNote = string.Equals(CurrentNote.FilePath, e.PreviousPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(CurrentNote.FilePath, e.Document.FilePath, StringComparison.OrdinalIgnoreCase);
        if (!touchesCurrentNote)
        {
            return;
        }

        if (HasUnsavedChanges && e.OriginId != _mutationOriginId)
        {
            HasConflict = true;
            StatusMessage = "This note changed on disk while you had local edits. Reselect it to reload.";
            return;
        }

        ApplyDocumentToEditor(e.Document);
        SelectSummaryByPath(e.Document.FilePath);
    }

    private IDisposable BeginMutationScope()
    {
        return NoteMutationService.BeginMutationScope(_mutationOriginId);
    }

    private async Task ApplyExternalChangesAsync(IReadOnlyList<NoteFileChangedEventArgs.NoteFileChange> changes)
    {
        if (!HasSelectedFolder || changes.Count == 0)
        {
            return;
        }

        var refreshFallback = false;
        var touchedCurrentNote = false;
        var currentPath = CurrentNote?.FilePath;
        var reloadedCurrentPath = currentPath;

        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case NoteFileChangeKind.Created:
                case NoteFileChangeKind.Changed:
                {
                    var summary = await LoadSummaryForPathAsync(change.Path);
                    if (summary is null)
                    {
                        refreshFallback = true;
                        continue;
                    }

                    ReplaceSummary(change.Path, summary);
                    touchedCurrentNote |= string.Equals(currentPath, change.Path, StringComparison.OrdinalIgnoreCase);
                    break;
                }
                case NoteFileChangeKind.Deleted:
                    ApplyDeletedNote(change.Path, refreshCollections: false);
                    touchedCurrentNote |= string.Equals(currentPath, change.Path, StringComparison.OrdinalIgnoreCase);
                    break;
                case NoteFileChangeKind.Renamed:
                {
                    if (string.IsNullOrWhiteSpace(change.OldPath))
                    {
                        refreshFallback = true;
                        continue;
                    }

                    RemoveSummary(change.OldPath);
                    var summary = await LoadSummaryForPathAsync(change.Path);
                    if (summary is null)
                    {
                        refreshFallback = true;
                        continue;
                    }

                    _allNotes.Add(summary);
                    touchedCurrentNote |= string.Equals(currentPath, change.OldPath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(currentPath, change.Path, StringComparison.OrdinalIgnoreCase);
                    if (string.Equals(currentPath, change.OldPath, StringComparison.OrdinalIgnoreCase))
                    {
                        reloadedCurrentPath = change.Path;
                    }
                    break;
                }
            }
        }

        RefreshAvailableTags();
        RefreshVisibleNotes();
        RefreshCalendarDays();

        if (refreshFallback)
        {
            await RefreshFromDiskAsync();
            return;
        }

        if (!touchedCurrentNote || CurrentNote is null)
        {
            return;
        }

        var matchingSummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, reloadedCurrentPath, StringComparison.OrdinalIgnoreCase));
        if (matchingSummary is null)
        {
            ClearEditor();
            StatusMessage = "The current note was removed.";
            return;
        }

        if (HasUnsavedChanges)
        {
            HasConflict = true;
            StatusMessage = "This note changed on disk while you had local edits. Reselect it to reload.";
            return;
        }

        var reloaded = await _notesRepository.LoadNoteAsync(matchingSummary.FilePath);
        if (reloaded is not null)
        {
            ApplyDocumentToEditor(reloaded);
            SelectSummaryByPath(reloaded.FilePath);
        }
    }

    private async Task<NoteSummary?> LoadSummaryForPathAsync(string filePath)
    {
        var note = await _notesRepository.LoadNoteAsync(filePath);
        return note is null ? null : BuildSummary(note);
    }

    private void ApplyDeletedNote(string filePath, bool refreshCollections)
    {
        RemoveSummary(filePath);
        if (refreshCollections)
        {
            RefreshAvailableTags();
            RefreshVisibleNotes();
            RefreshCalendarDays();
        }

        var deletedOpenNote = CurrentNote is not null && string.Equals(CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
        if (deletedOpenNote)
        {
            ClearEditor();
            return;
        }

        if (SelectedNoteSummary is not null && string.Equals(SelectedNoteSummary.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            _isApplyingSelection = true;
            try
            {
                SelectedNoteSummary = null;
                SelectedVisibleNote = null;
            }
            finally
            {
                _isApplyingSelection = false;
            }
        }
    }
}
