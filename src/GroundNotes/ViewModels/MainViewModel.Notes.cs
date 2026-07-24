using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Editors;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;

namespace GroundNotes.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private const string NoteConflictStatus = "This note changed on disk while you had local edits. Reselect it to reload.";

    public async Task CommitRenameAsync(NoteListItemViewModel? noteItem)
    {
        if (noteItem is null || !noteItem.IsRenaming || !HasSelectedFolder)
        {
            return;
        }

        if (IsNoteConflicted(noteItem.FilePath))
        {
            StatusMessage = "Resolve the note conflict before renaming it.";
            CancelRename(noteItem);
            return;
        }

        var newName = noteItem.RenameText.Trim();
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName, noteItem.DisplayName, StringComparison.Ordinal))
        {
            CancelRename(noteItem);
            return;
        }

        EditorPaneViewModel? openPane = null;
        NoteDocument? document;
        if (CurrentNote is not null && string.Equals(CurrentNote.FilePath, noteItem.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            CancelScheduledSave();
            if (!UpdateCurrentNoteFromEditor())
            {
                CancelRename(noteItem);
                return;
            }

            document = CurrentNote;
        }
        else if ((openPane = FindPaneByFilePath(noteItem.FilePath))?.CurrentNote is not null)
        {
            CancelPaneScheduledSave(openPane);
            if (!UpdatePaneNoteFromEditor(openPane))
            {
                CancelRename(noteItem);
                return;
            }

            document = openPane.CurrentNote;
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
        var editVersion = openPane?.EditVersion ?? _primaryEditVersion;
        var snapshot = CloneForPersistence(document);
        snapshot.Title = newName;
        NoteDocument renamed;
        try
        {
            using (BeginMutationScope())
            {
                renamed = await _noteMutationService.SaveAsync(NotesFolder, snapshot, CancellationToken.None);
            }
        }
        catch (NoteSaveConflictException)
        {
            MarkNoteConflict(noteItem.FilePath);
            CancelRename(noteItem);
            return;
        }
        CancelRename(noteItem);
        if ((openPane is null && ReferenceEquals(document, CurrentNote) && _primaryEditVersion != editVersion)
            || (openPane is not null && openPane.EditVersion != editVersion))
        {
            MergeSavedPersistenceState(openPane?.CurrentNote ?? CurrentNote, snapshot, renamed);
            StatusMessage = $"Renamed to {Path.GetFileNameWithoutExtension(renamed.FilePath)}";
            return;
        }

        ApplySavedDocumentToOpenPanes(noteItem.FilePath, renamed);
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

        _tagFolderPaths = await _tagFolderCatalogService.LoadAsync(NotesFolder);

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

        if (CurrentNote is null && SecondaryPanes.All(pane => pane.CurrentNote is null))
        {
            return;
        }

        var matchingSummary = CurrentNote is null
            ? null
            : _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));
        if (CurrentNote is not null && matchingSummary is null)
        {
            if (HasUnsavedChanges)
            {
                MarkCurrentNoteConflict();
            }
            else
            {
                ClearEditor();
                StatusMessage = "The current note was removed.";
            }
        }
        else if (CurrentNote is not null && HasUnsavedChanges)
        {
            MarkCurrentNoteConflict();
        }
        else if (CurrentNote is not null && matchingSummary is not null)
        {
            var reloaded = await _notesRepository.LoadNoteAsync(matchingSummary.FilePath);
            if (reloaded is not null)
            {
                ApplyDocumentToEditor(reloaded);
                SelectSummaryByPath(reloaded.FilePath);
            }
        }

        foreach (var pane in SecondaryPanes.ToList())
        {
            if (pane.CurrentNote is null)
            {
                continue;
            }

            var secondarySummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, pane.CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));
            if (secondarySummary is null)
            {
                if (pane.HasUnsavedChanges)
                {
                    MarkPaneConflict(pane);
                }
                else
                {
                    ClearPane(pane);
                    UnsubscribePane(pane);
                    SecondaryPanes.Remove(pane);
                }
                continue;
            }

            if (pane.HasUnsavedChanges)
            {
                MarkPaneConflict(pane);
                continue;
            }

            var reloadedSecondary = await _notesRepository.LoadNoteAsync(secondarySummary.FilePath);
            if (reloadedSecondary is not null)
            {
                ApplyDocumentToPane(pane, reloadedSecondary);
            }
        }
    }

    public Task OpenNoteAsync(string filePath, bool focusEditorWhenReady = true)
    {
        return OpenNoteInActivePaneAsync(filePath, focusEditorWhenReady);
    }

    private async Task OpenNoteInActivePaneAsync(string filePath, bool focusEditorWhenReady)
    {
        if (!IsNotePathInNotesFolder(NotesFolder, filePath))
        {
            StatusMessage = "The note is outside the current notes folder.";
            return;
        }

        var existingPane = FindPaneByFilePath(filePath);
        if (existingPane is not null && !ReferenceEquals(existingPane, ActiveSecondaryPane))
        {
            ActivatePane(existingPane);
            if (focusEditorWhenReady)
            {
                FocusEditorRequested?.Invoke(this, new FocusEditorRequestEventArgs { PaneId = existingPane.Id });
            }

            StatusMessage = "Ready.";
            return;
        }

        if (IsPrimaryPaneActive)
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
            ActivatePrimaryPane();
            UpdateActiveVisibleNote(note.FilePath);
            StatusMessage = "Ready.";
            if (focusEditorWhenReady)
            {
                FocusEditorRequested?.Invoke(this, new FocusEditorRequestEventArgs { Target = EditorPaneTarget.Primary });
            }

            return;
        }

        if (ActiveSecondaryPane is null)
        {
            return;
        }

        await LoadPaneNoteAsync(ActiveSecondaryPane, filePath, activateAfterLoad: focusEditorWhenReady);
    }

    private void ApplySavedDocumentToOpenPanes(string previousPath, NoteDocument saved)
    {
        if (CurrentNote is not null
            && (string.Equals(CurrentNote.FilePath, previousPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(CurrentNote.FilePath, saved.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            ApplyDocumentToEditor(saved);
            SelectSummaryByPath(saved.FilePath);
        }

        foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null
                                                          && (string.Equals(pane.CurrentNote.FilePath, previousPath, StringComparison.OrdinalIgnoreCase)
                                                              || string.Equals(pane.CurrentNote.FilePath, saved.FilePath, StringComparison.OrdinalIgnoreCase))))
        {
            ApplyDocumentToPane(pane, saved);
        }
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
            DismissTagSuggestions();
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
            DismissTagSuggestions();
            HasUnsavedChanges = false;
            HasConflict = false;
            LastSavedText = "GroundNotes";
        }
        finally
        {
            _isApplyingSelection = false;
        }
    }

    private bool TryUpdateActiveNoteFromEditor()
    {
        if (ActiveSecondaryPane is not null)
        {
            if (ActiveSecondaryPane.CurrentNote is null)
            {
                return false;
            }

            return UpdatePaneNoteFromEditor(ActiveSecondaryPane);
        }

        return UpdateCurrentNoteFromEditor();
    }

    private void CancelActiveNoteSave()
    {
        if (ActiveSecondaryPane is not null)
        {
            CancelPaneScheduledSave(ActiveSecondaryPane);
            return;
        }

        CancelScheduledSave();
    }

    private void ApplyDocumentToActivePane(NoteDocument note)
    {
        if (ActiveSecondaryPane is not null)
        {
            ApplyDocumentToPane(ActiveSecondaryPane, note);
            return;
        }

        ApplyDocumentToEditor(note);
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

            EditorTags = string.IsNullOrWhiteSpace(EditorTags)
                ? string.Empty
                : EditorTags;
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

        _primaryEditVersion++;
        if (ShowYamlFrontMatterInEditor)
        {
            var hadInvalidYamlFrontMatter = _hasInvalidYamlFrontMatter;
            if (!NotesRepository.TryParseEditableDocumentText(CurrentNote, EditorBody, out var parsedDocument, out var errorMessage))
            {
                _hasInvalidYamlFrontMatter = true;
                HasUnsavedChanges = true;
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
        }

        HasUnsavedChanges = true;
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
        catch (Exception ex)
        {
            StatusMessage = $"Could not save note: {ex.Message}";
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

        if (HasConflict)
        {
            StatusMessage = NoteConflictStatus;
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
            using (BeginMutationScope())
            {
                await _noteMutationService.DeleteIfExistsAsync(CurrentNote.FilePath, cancellationToken);
            }
            StatusMessage = "Empty draft discarded.";
            return;
        }

        var editVersion = _primaryEditVersion;
        var snapshot = CloneForPersistence(CurrentNote);
        NoteDocument saved;
        try
        {
            saved = await PersistNoteAsync(snapshot, cancellationToken);
        }
        catch (NoteSaveConflictException)
        {
            MarkCurrentNoteConflict();
            return;
        }

        if (!IsSamePersistedNote(CurrentNote, snapshot, saved))
        {
            return;
        }

        if (_primaryEditVersion != editVersion)
        {
            MergeSavedPersistenceState(CurrentNote, snapshot, saved);
            HasUnsavedChanges = true;
            return;
        }

        CurrentNote = saved;
        HasUnsavedChanges = false;
        LastSavedText = FormatLastSavedText(saved.UpdatedAt);
    }

    private static NoteDocument CloneForPersistence(NoteDocument document)
    {
        return document with { Tags = [.. document.Tags] };
    }

    private static bool IsSamePersistedNote(
        NoteDocument? current,
        NoteDocument snapshot,
        NoteDocument saved)
    {
        return current is not null
               && (string.Equals(current.FilePath, snapshot.FilePath, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(current.FilePath, saved.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    private static void MergeSavedPersistenceState(
        NoteDocument? current,
        NoteDocument snapshot,
        NoteDocument saved)
    {
        if (current is null
            || (!string.Equals(current.FilePath, snapshot.FilePath, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(current.FilePath, saved.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        current.Id = saved.Id;
        current.FilePath = saved.FilePath;
        current.OriginalTitle = saved.OriginalTitle;
        current.UpdatedAt = saved.UpdatedAt;
        current.SourceContentHash = saved.SourceContentHash;
        if (string.Equals(current.Title, snapshot.Title, StringComparison.Ordinal))
        {
            current.Title = saved.Title;
        }
    }

    private async Task<NoteDocument> PersistNoteAsync(NoteDocument document, CancellationToken cancellationToken)
    {
        var notesFolder = NotesFolder;
        if (!IsNotePathInNotesFolder(notesFolder, document.FilePath))
        {
            throw new InvalidOperationException("The note is outside the current notes folder.");
        }

        await _notePersistenceLock.WaitAsync(cancellationToken);
        try
        {
            SuppressWatcher();
            using (BeginMutationScope())
            {
                return await _noteMutationService.SaveAsync(notesFolder, document, cancellationToken);
            }
        }
        finally
        {
            _notePersistenceLock.Release();
        }
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
            var currentItems = VisibleNotes.ToDictionary(note => note.FilePath, StringComparer.OrdinalIgnoreCase);
            var nextNotes = _notesRepository.QueryNotes(_allNotes, SearchText, SelectedCalendarDate, SelectedSortOption);
            var nextItems = nextNotes.Select(note =>
            {
                if (!currentItems.TryGetValue(note.FilePath, out var existing))
                {
                    return new NoteListItemViewModel(note);
                }

                existing.UpdateSummary(note);
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

    private void RefreshAvailableTags()
    {
        AvailableTags = new ObservableCollection<string>(TagHierarchyHelper
            .ExpandWithAncestors(_tagFolderPaths.Concat(_allNotes.SelectMany(note => note.Tags)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));
    }

    private void ReplaceSummary(string previousPath, NoteSummary summary)
    {
        if (_selectedSidebarFilePaths.Remove(previousPath))
        {
            _selectedSidebarFilePaths.Add(summary.FilePath);
        }

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
        return TagHierarchyHelper.ParseCommaSeparated(input);
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

    private static bool IsUnsavedInvalidYamlDraft(EditorPaneViewModel pane, IEnumerable<NoteSummary> allNotes)
    {
        if (!pane.HasInvalidYamlFrontMatter || pane.CurrentNote is null)
        {
            return false;
        }

        if (!pane.CurrentNote.IsAutoCreated)
        {
            return false;
        }

        return !allNotes.Any(note => string.Equals(note.FilePath, pane.CurrentNote.FilePath, StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadPaneNoteAsync(EditorPaneViewModel pane, string filePath, bool activateAfterLoad)
    {
        if (!await CanLeavePaneStateAsync(pane, filePath))
        {
            return;
        }

        var note = await _notesRepository.LoadNoteAsync(filePath);
        if (note is null)
        {
            return;
        }

        CancelInlineRename();
        ApplyDocumentToPane(pane, note);
        pane.HasConflict = false;
        pane.IsOpen = true;
        StatusMessage = "Ready.";
        UpdateActiveVisibleNote(GetActiveSidebarFilePath());

        if (ReferenceEquals(ActiveSecondaryPane, pane))
        {
            SyncSelectionToFilePath(filePath);
            UpdateActiveVisibleNote(filePath);
        }

        if (activateAfterLoad)
        {
            ActivatePane(pane);
            SelectSummaryByPath(filePath);
            FocusEditorRequested?.Invoke(this, new FocusEditorRequestEventArgs { PaneId = pane.Id });
        }
    }

    private void ApplyDocumentToPane(EditorPaneViewModel pane, NoteDocument note)
    {
        pane.IsApplyingSelection = true;
        try
        {
            pane.HasInvalidYamlFrontMatter = false;
            pane.CurrentNote = note;
            pane.EditorTitle = note.Title;
            pane.EditorTags = string.Join(", ", note.Tags);
            pane.EditorBody = ShowYamlFrontMatterInEditor
                ? NotesRepository.BuildEditableDocumentText(note)
                : note.Body;
            pane.HasUnsavedChanges = false;
            pane.LastSavedText = FormatLastSavedText(note.UpdatedAt);
            pane.IsOpen = true;
        }
        finally
        {
            pane.IsApplyingSelection = false;
        }

        UpdateActiveVisibleNote(GetActiveSidebarFilePath());
    }

    private void ClearPane(EditorPaneViewModel pane)
    {
        CancelPaneScheduledSave(pane);
        pane.IsApplyingSelection = true;
        try
        {
            pane.HasInvalidYamlFrontMatter = false;
            pane.CurrentNote = null;
            pane.EditorTitle = string.Empty;
            pane.EditorTags = string.Empty;
            pane.EditorBody = string.Empty;
            pane.HasUnsavedChanges = false;
            pane.HasConflict = false;
            pane.LastSavedText = "GroundNotes";
            pane.IsOpen = false;
        }
        finally
        {
            pane.IsApplyingSelection = false;
        }

        UpdateActiveVisibleNote(GetActiveSidebarFilePath());
    }

    private void HandlePaneEditorTitleChanged(EditorPaneViewModel pane)
    {
        ClearTransientStatusOnEdit();

        if (!HasSelectedFolder || pane.CurrentNote is null)
        {
            return;
        }

        if (!UpdatePaneNoteFromEditor(pane))
        {
            return;
        }

        SchedulePaneSave(pane);
    }

    private void HandlePaneEditorBodyChanged(EditorPaneViewModel pane)
    {
        ClearTransientStatusOnEdit();

        if (!HasSelectedFolder || pane.CurrentNote is null)
        {
            return;
        }

        if (!UpdatePaneNoteFromEditor(pane))
        {
            return;
        }

        SchedulePaneSave(pane);
    }

    internal async Task CommitPaneEditorTagsAsync(EditorPaneViewModel pane)
    {
        if (pane.CurrentNote is null)
        {
            return;
        }

        var committedTags = ParseTags(pane.EditorTags);
        var normalizedText = string.Join(", ", committedTags);
        if (!string.Equals(pane.EditorTags, normalizedText, StringComparison.Ordinal))
        {
            pane.IsApplyingSelection = true;
            try
            {
                pane.EditorTags = normalizedText;
            }
            finally
            {
                pane.IsApplyingSelection = false;
            }
        }

        if (pane.CurrentNote.Tags.SequenceEqual(committedTags, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        pane.CurrentNote.Tags = committedTags;
        pane.EditVersion++;
        pane.HasUnsavedChanges = true;
        await SavePaneNoteAsync(pane, CancellationToken.None);
    }

    private bool UpdatePaneNoteFromEditor(EditorPaneViewModel pane)
    {
        if (pane.CurrentNote is null)
        {
            return false;
        }

        pane.EditVersion++;
        if (ShowYamlFrontMatterInEditor)
        {
            var hadInvalidYamlFrontMatter = pane.HasInvalidYamlFrontMatter;
            if (!NotesRepository.TryParseEditableDocumentText(pane.CurrentNote, pane.EditorBody, out var parsedDocument, out var errorMessage))
            {
                pane.HasInvalidYamlFrontMatter = true;
                pane.HasUnsavedChanges = true;
                StatusMessage = errorMessage;
                return false;
            }

            pane.HasInvalidYamlFrontMatter = false;
            pane.CurrentNote = parsedDocument;

            pane.IsApplyingSelection = true;
            try
            {
                pane.EditorTitle = parsedDocument.Title;
                pane.EditorTags = string.Join(", ", parsedDocument.Tags);
            }
            finally
            {
                pane.IsApplyingSelection = false;
            }

            if (hadInvalidYamlFrontMatter && StatusMessage.StartsWith("Invalid YAML frontmatter", StringComparison.Ordinal))
            {
                StatusMessage = "Ready.";
            }
        }
        else
        {
            pane.HasInvalidYamlFrontMatter = false;
            pane.CurrentNote.Title = string.IsNullOrWhiteSpace(pane.EditorTitle)
                ? pane.CurrentNote.OriginalTitle
                : pane.EditorTitle.Trim();
            pane.CurrentNote.Body = pane.EditorBody;
        }

        pane.HasUnsavedChanges = true;
        return true;
    }

    private void SchedulePaneSave(EditorPaneViewModel pane)
    {
        CancelPaneScheduledSave(pane);
        pane.SaveCts = new CancellationTokenSource();
        var token = pane.SaveCts.Token;
        _ = SavePaneAfterDebounceAsync(pane, token);
    }

    private async Task SavePaneAfterDebounceAsync(EditorPaneViewModel pane, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(450, cancellationToken);
            await SavePaneNoteAsync(pane, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save note: {ex.Message}";
        }
    }

    private async Task SavePaneNoteAsync(EditorPaneViewModel pane, CancellationToken cancellationToken)
    {
        if (pane.CurrentNote is null || !HasSelectedFolder)
        {
            return;
        }

        if (pane.HasConflict)
        {
            StatusMessage = NoteConflictStatus;
            return;
        }

        if (pane.HasInvalidYamlFrontMatter)
        {
            StatusMessage = "Invalid YAML frontmatter. Fix it before saving.";
            return;
        }

        var editVersion = pane.EditVersion;
        var snapshot = CloneForPersistence(pane.CurrentNote);
        NoteDocument saved;
        try
        {
            saved = await PersistNoteAsync(snapshot, cancellationToken);
        }
        catch (NoteSaveConflictException)
        {
            MarkPaneConflict(pane);
            return;
        }

        if (!IsSamePersistedNote(pane.CurrentNote, snapshot, saved))
        {
            return;
        }

        if (pane.EditVersion != editVersion)
        {
            MergeSavedPersistenceState(pane.CurrentNote, snapshot, saved);
            pane.HasUnsavedChanges = true;
            return;
        }

        pane.CurrentNote = saved;
        pane.HasUnsavedChanges = false;
        pane.LastSavedText = FormatLastSavedText(saved.UpdatedAt);
    }

    private void CancelPaneScheduledSave(EditorPaneViewModel pane)
    {
        pane.SaveCts?.Cancel();
        pane.SaveCts?.Dispose();
        pane.SaveCts = null;
    }

    public async Task<bool> PrepareToCloseAsync()
    {
        if (HasConflict || SecondaryPanes.Any(pane => pane.HasConflict))
        {
            CancelScheduledSave();
            foreach (var pane in SecondaryPanes)
            {
                CancelPaneScheduledSave(pane);
            }

            StatusMessage = "Resolve note conflicts before closing this window.";
            return false;
        }

        if (!await CanLeaveCurrentEditorStateAsync())
        {
            return false;
        }

        await FlushPendingSaveAsync();
        if (HasUnsavedChanges)
        {
            return false;
        }

        foreach (var pane in SecondaryPanes.ToList())
        {
            if (!await CanLeavePaneStateAsync(pane))
            {
                return false;
            }

            if (pane.CurrentNote is not null && pane.HasUnsavedChanges)
            {
                CancelPaneScheduledSave(pane);
                await SavePaneNoteAsync(pane, CancellationToken.None);
            }

            if (pane.HasUnsavedChanges)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> CanLeavePaneStateAsync(EditorPaneViewModel pane, string? nextFilePath = null)
    {
        if (pane.HasConflict && pane.CurrentNote is not null)
        {
            if (!string.IsNullOrWhiteSpace(nextFilePath)
                && string.Equals(pane.CurrentNote.FilePath, nextFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            StatusMessage = "Resolve the note conflict before switching notes.";
            return false;
        }

        if (!pane.HasInvalidYamlFrontMatter || pane.CurrentNote is null)
        {
            if (pane.CurrentNote is not null && pane.HasUnsavedChanges)
            {
                CancelPaneScheduledSave(pane);
                await SavePaneNoteAsync(pane, CancellationToken.None);
                if (pane.HasUnsavedChanges)
                {
                    StatusMessage = "Could not save the current note before switching.";
                    return false;
                }
            }

            return true;
        }

        if (!string.IsNullOrWhiteSpace(nextFilePath)
            && string.Equals(pane.CurrentNote.FilePath, nextFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsUnsavedInvalidYamlDraft(pane, _allNotes))
        {
            var shouldDiscard = await _workspaceDialogService.ConfirmDiscardInvalidDraftAsync();
            if (!shouldDiscard)
            {
                StatusMessage = "Invalid YAML frontmatter. Fix it or discard the draft to continue.";
                return false;
            }

            ClearPane(pane);
            StatusMessage = "Invalid YAML draft discarded.";
            return true;
        }

        StatusMessage = "Invalid YAML frontmatter. Fix it before switching notes.";
        return false;
    }

    private async Task<bool> CanLeaveCurrentEditorStateAsync(string? nextFilePath = null)
    {
        if (HasConflict && CurrentNote is not null)
        {
            if (!string.IsNullOrWhiteSpace(nextFilePath)
                && string.Equals(CurrentNote.FilePath, nextFilePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            RestoreSelectionAfterBlockedLeave();
            StatusMessage = "Resolve the note conflict before switching notes.";
            return false;
        }

        if (!_hasInvalidYamlFrontMatter || CurrentNote is null)
        {
            if (CurrentNote is not null && HasUnsavedChanges)
            {
                await FlushPendingSaveAsync();
                if (HasUnsavedChanges)
                {
                    RestoreSelectionAfterBlockedLeave();
                    StatusMessage = "Could not save the current note before switching.";
                    return false;
                }
            }

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
        if (_isApplyingSidebarBulkMutation)
        {
            return;
        }

        if (!HasSelectedFolder || !AreSameNotesFolder(NotesFolder, e.FolderPath))
        {
            return;
        }

        if (e.Kind == NoteMutationKind.Deleted)
        {
            ApplyDeletedNote(
                e.PreviousPath,
                refreshCollections: true,
                preserveUnsavedOpenNotes: e.OriginId != _mutationOriginId);
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

        if (e.OriginId == _mutationOriginId)
        {
            return;
        }

        if (CurrentNote is null && SecondaryPanes.All(pane => pane.CurrentNote is null))
        {
            return;
        }

        var touchesCurrentNote = CurrentNote is not null
            && (string.Equals(CurrentNote.FilePath, e.PreviousPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(CurrentNote.FilePath, e.Document.FilePath, StringComparison.OrdinalIgnoreCase));
        var touchesSecondaryPanes = SecondaryPanes
            .Where(pane => pane.CurrentNote is not null
                && (string.Equals(pane.CurrentNote.FilePath, e.PreviousPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pane.CurrentNote.FilePath, e.Document.FilePath, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (!touchesCurrentNote && touchesSecondaryPanes.Count == 0)
        {
            return;
        }

        if (touchesCurrentNote && HasUnsavedChanges)
        {
            MarkCurrentNoteConflict();
        }

        foreach (var pane in touchesSecondaryPanes.Where(pane => pane.HasUnsavedChanges))
        {
            MarkPaneConflict(pane);
        }

        if (touchesCurrentNote && !HasUnsavedChanges)
        {
            ApplyDocumentToEditor(e.Document);
            SelectSummaryByPath(e.Document.FilePath);
        }

        foreach (var pane in touchesSecondaryPanes.Where(pane => !pane.HasUnsavedChanges))
        {
            ApplyDocumentToPane(pane, e.Document);
        }
    }

    private bool IsNoteConflicted(string filePath)
    {
        return (CurrentNote is not null
                && HasConflict
                && string.Equals(CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
               || SecondaryPanes.Any(pane => pane.CurrentNote is not null
                                             && pane.HasConflict
                                             && string.Equals(pane.CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private void MarkNoteConflict(string filePath)
    {
        StatusMessage = NoteConflictStatus;
        if (CurrentNote is not null
            && string.Equals(CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            MarkCurrentNoteConflict();
        }

        foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null
                                                          && string.Equals(pane.CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
        {
            MarkPaneConflict(pane);
        }
    }

    private void MarkCurrentNoteConflict()
    {
        CancelScheduledSave();
        HasConflict = true;
        StatusMessage = NoteConflictStatus;
    }

    private void MarkPaneConflict(EditorPaneViewModel pane)
    {
        CancelPaneScheduledSave(pane);
        pane.HasConflict = true;
        StatusMessage = NoteConflictStatus;
    }

    internal static bool IsNotePathInNotesFolder(string? notesFolder, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            return AreSameNotesFolder(notesFolder, Path.GetDirectoryName(Path.GetFullPath(filePath)));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    internal static bool AreSameNotesFolder(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            var firstFullPath = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var secondFullPath = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(firstFullPath, secondFullPath, comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
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
        var touchedSecondaryPaneIds = new HashSet<Guid>();
        var currentPath = CurrentNote?.FilePath;
        var reloadedCurrentPath = currentPath;
        var reloadedSecondaryPaths = SecondaryPanes
            .Where(pane => pane.CurrentNote is not null)
            .ToDictionary(pane => pane.Id, pane => pane.CurrentNote!.FilePath);

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
                    foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && string.Equals(pane.CurrentNote.FilePath, change.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        touchedSecondaryPaneIds.Add(pane.Id);
                    }
                    break;
                }
                case NoteFileChangeKind.Deleted:
                    ApplyDeletedNote(change.Path, refreshCollections: false, preserveUnsavedOpenNotes: true);
                    touchedCurrentNote |= string.Equals(currentPath, change.Path, StringComparison.OrdinalIgnoreCase);
                    foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && string.Equals(pane.CurrentNote.FilePath, change.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        touchedSecondaryPaneIds.Add(pane.Id);
                    }
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
                    foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null
                        && (string.Equals(pane.CurrentNote.FilePath, change.OldPath, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(pane.CurrentNote.FilePath, change.Path, StringComparison.OrdinalIgnoreCase))))
                    {
                        touchedSecondaryPaneIds.Add(pane.Id);
                    }
                    if (string.Equals(currentPath, change.OldPath, StringComparison.OrdinalIgnoreCase))
                    {
                        reloadedCurrentPath = change.Path;
                    }
                    foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && string.Equals(pane.CurrentNote.FilePath, change.OldPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        reloadedSecondaryPaths[pane.Id] = change.Path;
                    }
                    break;
                }
            }
        }

        RefreshAvailableTags();
        RefreshVisibleNotes();
        RefreshCalendarNoteDates();
        RefreshCalendarDays();

        if (refreshFallback)
        {
            await RefreshFromDiskAsync();
            return;
        }

        if (touchedCurrentNote && CurrentNote is not null)
        {
            var matchingSummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, reloadedCurrentPath, StringComparison.OrdinalIgnoreCase));
            if (matchingSummary is null)
            {
                if (HasUnsavedChanges)
                {
                    MarkCurrentNoteConflict();
                }
                else
                {
                    ClearEditor();
                    StatusMessage = "The current note was removed.";
                }
            }
            else if (HasUnsavedChanges)
            {
                MarkCurrentNoteConflict();
            }
            else
            {
                var reloaded = await _notesRepository.LoadNoteAsync(matchingSummary.FilePath);
                if (reloaded is not null)
                {
                    ApplyDocumentToEditor(reloaded);
                    SelectSummaryByPath(reloaded.FilePath);
                }
            }
        }

        foreach (var pane in SecondaryPanes.Where(pane => touchedSecondaryPaneIds.Contains(pane.Id)).ToList())
        {
            if (pane.CurrentNote is null)
            {
                continue;
            }

            var reloadedPath = reloadedSecondaryPaths.TryGetValue(pane.Id, out var path)
                ? path
                : pane.CurrentNote.FilePath;
            var matchingSummary = _allNotes.FirstOrDefault(note => string.Equals(note.FilePath, reloadedPath, StringComparison.OrdinalIgnoreCase));
            if (matchingSummary is null)
            {
                if (pane.HasUnsavedChanges)
                {
                    MarkPaneConflict(pane);
                }
                else
                {
                    ClearPane(pane);
                    UnsubscribePane(pane);
                    SecondaryPanes.Remove(pane);
                }
            }
            else if (pane.HasUnsavedChanges)
            {
                MarkPaneConflict(pane);
            }
            else
            {
                var reloaded = await _notesRepository.LoadNoteAsync(matchingSummary.FilePath);
                if (reloaded is not null)
                {
                    ApplyDocumentToPane(pane, reloaded);
                }
            }
        }
    }

    private async Task<NoteSummary?> LoadSummaryForPathAsync(string filePath)
    {
        var note = await _notesRepository.LoadNoteAsync(filePath);
        return note is null ? null : BuildSummary(note);
    }

    private void ApplyDeletedNote(
        string filePath,
        bool refreshCollections,
        bool preserveUnsavedOpenNotes)
    {
        _selectedSidebarFilePaths.Remove(filePath);
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
            if (preserveUnsavedOpenNotes && HasUnsavedChanges)
            {
                MarkCurrentNoteConflict();
            }
            else
            {
                ClearEditor();
            }
        }

        foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && string.Equals(pane.CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (preserveUnsavedOpenNotes && pane.HasUnsavedChanges)
            {
                MarkPaneConflict(pane);
                continue;
            }

            ClearPane(pane);
            UnsubscribePane(pane);
            SecondaryPanes.Remove(pane);
            if (ReferenceEquals(ActiveSecondaryPane, pane))
            {
                ActivatePrimaryPane();
            }
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

    internal async Task<int> RenameImageReferenceInAllNotesAsync(
        string oldResolvedPath,
        string newMarkdownPath,
        NoteAssetService noteAssetService,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(NotesFolder) || !Directory.Exists(NotesFolder))
        {
            return 0;
        }

        var summaries = await _notesRepository.LoadSummariesAsync(NotesFolder, cancellationToken);
        if (summaries.Count == 0)
        {
            return 0;
        }

        var updatedCount = 0;
        SuppressWatcher();

        foreach (var summary in summaries)
        {
            var filePath = summary.FilePath;
            cancellationToken.ThrowIfCancellationRequested();

            if (CurrentNote is not null
                && string.Equals(CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var pane = FindPaneByFilePath(filePath);
            if (pane is not null)
            {
                var updatedText = TryReplaceImageReferences(
                    pane.EditorBody,
                    NotesFolder,
                    oldResolvedPath,
                    newMarkdownPath,
                    noteAssetService);

                if (updatedText is not null)
                {
                    pane.EditorBody = updatedText;
                    updatedCount++;
                }

                continue;
            }

            var note = await _notesRepository.LoadNoteAsync(filePath, cancellationToken);
            if (note is null)
            {
                continue;
            }

            var updatedBody = TryReplaceImageReferences(
                note.Body,
                NotesFolder,
                oldResolvedPath,
                newMarkdownPath,
                noteAssetService);

            if (updatedBody is not null)
            {
                note = note with { Body = updatedBody };
                using (BeginMutationScope())
                {
                    await _noteMutationService.SaveAsync(NotesFolder, note, cancellationToken, preserveTimestamp: true);
                }

                updatedCount++;
            }
        }

        return updatedCount;
    }

    internal static string? TryReplaceImageReferences(
        string text,
        string notesFolderPath,
        string oldResolvedPath,
        string newMarkdownPath,
        NoteAssetService noteAssetService)
    {
        var changed = false;
        var result = new StringBuilder();

        foreach (var (lineText, lineEnding) in EnumerateLines(text))
        {
            var analysis = MarkdownLineParser.Analyze(lineText, MarkdownFenceState.None);
            if (analysis.Images.Count == 0)
            {
                result.Append(lineText).Append(lineEnding);
                continue;
            }

            var lineResult = new StringBuilder(lineText);
            var offsetAdjustment = 0;

            foreach (var image in analysis.Images.OrderByDescending(static img => img.Url.Start))
            {
                var urlText = lineText[image.Url.Start..image.Url.End];
                var resolved = noteAssetService.ResolveImagePath(notesFolderPath, urlText);
                if (string.Equals(resolved, oldResolvedPath, StringComparison.OrdinalIgnoreCase))
                {
                    lineResult.Remove(image.Url.Start + offsetAdjustment, image.Url.Length);
                    lineResult.Insert(image.Url.Start + offsetAdjustment, newMarkdownPath);
                    offsetAdjustment += newMarkdownPath.Length - image.Url.Length;
                    changed = true;
                }
            }

            result.Append(lineResult).Append(lineEnding);
        }

        return changed ? result.ToString() : null;
    }

    private static IEnumerable<(string LineText, string LineEnding)> EnumerateLines(string text)
    {
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                var lineEnd = i;
                var ending = "\n";
                if (i > 0 && text[i - 1] == '\r')
                {
                    lineEnd = i - 1;
                    ending = "\r\n";
                }

                yield return (text[start..lineEnd], ending);
                start = i + 1;
            }
        }

        if (start < text.Length)
        {
            yield return (text[start..], string.Empty);
        }
        else if (start == text.Length)
        {
            yield return (string.Empty, string.Empty);
        }
    }
}
