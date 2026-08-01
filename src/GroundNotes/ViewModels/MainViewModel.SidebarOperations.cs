using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

internal sealed record SidebarSelectionState(
    IReadOnlyList<string> FilePaths,
    string? AnchorOccurrencePath,
    string? SelectedOccurrencePath);

public partial class MainViewModel
{
    private readonly HashSet<string> _selectedSidebarFilePaths = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _tagFolderPaths = [];
    private string? _sidebarSelectionAnchorOccurrencePath;
    private bool _isApplyingSidebarBulkMutation;
    private string? _focusedSidebarTagPath;

    public IReadOnlyList<NoteListItemViewModel> SelectedSidebarNotes => _selectedSidebarFilePaths
        .Select(path => VisibleNotes.FirstOrDefault(note => string.Equals(note.FilePath, path, StringComparison.OrdinalIgnoreCase))
            ?? (_allNotes.FirstOrDefault(note => string.Equals(note.FilePath, path, StringComparison.OrdinalIgnoreCase)) is { } summary
                ? new NoteListItemViewModel(summary)
                : null))
        .OfType<NoteListItemViewModel>()
        .OrderBy(note => note.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<string> TagFolderPaths => GetAllTagFolderPaths();

    public bool CanMoveSelectedNotesToRoot => SelectedSidebarNotes.Any(note => note.Summary.Tags.Count > 0);

    public string? FocusedSidebarTagPath => _focusedSidebarTagPath;

    public bool IsSidebarFolderFocused => !string.IsNullOrWhiteSpace(_focusedSidebarTagPath);

    public string FocusedSidebarFolderLabel => IsSidebarFolderFocused ? $"Focused: {_focusedSidebarTagPath}" : string.Empty;

    public void SelectOnlySidebarNote(SidebarTreeRowViewModel row)
    {
        if (row.Note is null)
        {
            return;
        }

        _selectedSidebarFilePaths.Clear();
        _selectedSidebarFilePaths.Add(row.Note.FilePath);
        _sidebarSelectionAnchorOccurrencePath = row.OccurrencePath;
        SelectedSidebarRow = row;
        RestoreSidebarSelectionFlags();
    }

    private void SelectOnlySidebarFilePath(string filePath)
    {
        _selectedSidebarFilePaths.Clear();
        _selectedSidebarFilePaths.Add(filePath);
        var row = FindPreferredNoteRow(filePath);
        _sidebarSelectionAnchorOccurrencePath = row?.OccurrencePath;
        SelectedSidebarRow = row;
        RestoreSidebarSelectionFlags();
    }

    public void ToggleSidebarNoteSelection(SidebarTreeRowViewModel row)
    {
        if (row.Note is null)
        {
            return;
        }

        if (!_selectedSidebarFilePaths.Remove(row.Note.FilePath))
        {
            _selectedSidebarFilePaths.Add(row.Note.FilePath);
        }

        _sidebarSelectionAnchorOccurrencePath = row.OccurrencePath;
        SelectedSidebarRow = row;
        RestoreSidebarSelectionFlags();
    }

    public void SelectSidebarNoteRange(SidebarTreeRowViewModel row)
    {
        if (row.Note is null)
        {
            return;
        }

        var targetIndex = VisibleSidebarRows.IndexOf(row);
        var anchorIndex = -1;
        if (!string.IsNullOrWhiteSpace(_sidebarSelectionAnchorOccurrencePath))
        {
            for (var index = 0; index < VisibleSidebarRows.Count; index++)
            {
                if (string.Equals(VisibleSidebarRows[index].OccurrencePath, _sidebarSelectionAnchorOccurrencePath, StringComparison.Ordinal))
                {
                    anchorIndex = index;
                    break;
                }
            }
        }
        if (anchorIndex < 0)
        {
            SelectOnlySidebarNote(row);
            return;
        }

        _selectedSidebarFilePaths.Clear();
        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);
        for (var index = start; index <= end; index++)
        {
            if (VisibleSidebarRows[index].Note is { } note)
            {
                _selectedSidebarFilePaths.Add(note.FilePath);
            }
        }

        SelectedSidebarRow = row;
        RestoreSidebarSelectionFlags();
    }

    public void EnsureSidebarNoteSelected(SidebarTreeRowViewModel row)
    {
        if (row.Note is not null && !_selectedSidebarFilePaths.Contains(row.Note.FilePath))
        {
            SelectOnlySidebarNote(row);
        }
    }

    public void ClearSidebarSelection()
    {
        _selectedSidebarFilePaths.Clear();
        _sidebarSelectionAnchorOccurrencePath = null;
        RestoreSidebarSelectionFlags();
    }

    public bool ClearAdditionalSidebarSelection()
    {
        if (_selectedSidebarFilePaths.Count <= 1)
        {
            return false;
        }

        var activeFilePath = GetActiveSidebarFilePath();
        var retainedFilePath = !string.IsNullOrWhiteSpace(activeFilePath)
            && _selectedSidebarFilePaths.Contains(activeFilePath)
            ? activeFilePath
            : null;
        var retainedRow = retainedFilePath is null
            ? (!string.IsNullOrWhiteSpace(activeFilePath) ? FindPreferredNoteRow(activeFilePath) : null)
            : FindPreferredNoteRow(retainedFilePath);

        _selectedSidebarFilePaths.Clear();
        if (retainedFilePath is not null)
        {
            _selectedSidebarFilePaths.Add(retainedFilePath);
            _sidebarSelectionAnchorOccurrencePath = retainedRow?.OccurrencePath;
        }
        else
        {
            _sidebarSelectionAnchorOccurrencePath = null;
        }

        SelectedSidebarRow = retainedRow;
        RestoreSidebarSelectionFlags();
        return true;
    }

    internal SidebarSelectionState CaptureSidebarSelection()
    {
        return new SidebarSelectionState(
            _selectedSidebarFilePaths.ToList(),
            _sidebarSelectionAnchorOccurrencePath,
            SelectedSidebarRow?.OccurrencePath);
    }

    internal void RestoreSidebarSelection(SidebarSelectionState state)
    {
        _selectedSidebarFilePaths.Clear();
        foreach (var filePath in state.FilePaths)
        {
            if (_allNotes.Any(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedSidebarFilePaths.Add(filePath);
            }
        }

        _sidebarSelectionAnchorOccurrencePath = state.AnchorOccurrencePath;
        SelectedSidebarRow = state.SelectedOccurrencePath is null
            ? FindPreferredNoteRow(GetActiveSidebarFilePath())
            : VisibleSidebarRows.FirstOrDefault(row => string.Equals(
                row.OccurrencePath,
                state.SelectedOccurrencePath,
                StringComparison.Ordinal))
              ?? FindPreferredNoteRow(GetActiveSidebarFilePath());
        RestoreSidebarSelectionFlags();
    }

    [RelayCommand]
    private void FocusSidebarFolder(string? folderPath)
    {
        var normalized = TagHierarchyHelper.TryNormalize(folderPath);
        if (normalized is null)
        {
            return;
        }

        ClearSidebarSelection();
        SetFocusedSidebarTagPath(normalized);
        RefreshSidebarTree();
    }

    [RelayCommand]
    private void ClearSidebarFolderFocus()
    {
        if (!IsSidebarFolderFocused)
        {
            return;
        }

        ClearSidebarSelection();
        SetFocusedSidebarTagPath(null);
        RefreshSidebarTree();
    }

    private void SetFocusedSidebarTagPath(string? folderPath)
    {
        if (string.Equals(_focusedSidebarTagPath, folderPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _focusedSidebarTagPath = folderPath;
        OnPropertyChanged(nameof(FocusedSidebarTagPath));
        OnPropertyChanged(nameof(IsSidebarFolderFocused));
        OnPropertyChanged(nameof(FocusedSidebarFolderLabel));
    }

    private void RestoreSidebarSelectionFlags()
    {
        foreach (var note in VisibleNotes)
        {
            note.IsSelected = _selectedSidebarFilePaths.Contains(note.FilePath);
        }

        OnPropertyChanged(nameof(SelectedSidebarNotes));
        OnPropertyChanged(nameof(CanMoveSelectedNotesToRoot));
    }

    [RelayCommand]
    private async Task CreateTagFolderAsync(string? parentPath)
    {
        if (!HasSelectedFolder)
        {
            return;
        }

        var workspacePath = NotesFolder;
        var enteredPath = await _workspaceDialogService.PromptCreateTagFolderAsync();
        if (!string.Equals(NotesFolder, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        var normalized = TagHierarchyHelper.TryNormalize(enteredPath);
        if (normalized is null)
        {
            return;
        }

        var normalizedParent = TagHierarchyHelper.TryNormalize(parentPath);
        var folderPath = normalizedParent is null
            || normalized.StartsWith(normalizedParent + '/', StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalizedParent}/{normalized}";
        _tagFolderPaths = _tagFolderPaths
            .Append(folderPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();
        await _tagFolderCatalogService.SaveAsync(workspacePath, _tagFolderPaths);
        foreach (var path in TagHierarchyHelper.ExpandWithAncestors([folderPath]))
        {
            _sidebarTreeExpansionStates[path] = true;
        }
        RefreshAvailableTags();
        RefreshSidebarTree();
        StatusMessage = $"Created folder {folderPath}.";
    }

    [RelayCommand]
    private async Task RenameTagFolderAsync(string? folderPath)
    {
        var currentPath = TagHierarchyHelper.TryNormalize(folderPath);
        if (currentPath is null)
        {
            return;
        }

        var workspacePath = NotesFolder;
        var replacement = TagHierarchyHelper.TryNormalize(await _workspaceDialogService.PromptRenameTagFolderAsync(currentPath));
        if (!string.Equals(NotesFolder, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (replacement is null || string.Equals(currentPath, replacement, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (IsTagPathWithin(replacement, currentPath))
        {
            StatusMessage = "A folder cannot be renamed into one of its own subfolders.";
            return;
        }

        var nextFolderPaths = _tagFolderPaths
            .Select(path => ReplaceTagPathPrefix(path, currentPath, replacement))
            .Append(replacement)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();
        if (!await MutateNoteTagsAsync(
            _allNotes.Select(note => note.FilePath),
            tags => tags.Select(tag => ReplaceTagPathPrefix(tag, currentPath, replacement)).ToList()))
        {
            return;
        }

        _tagFolderPaths = nextFolderPaths;
        RemapSidebarExpansionStates(currentPath, replacement);
        if (IsSidebarFolderFocused && IsTagPathWithin(FocusedSidebarTagPath!, currentPath))
        {
            SetFocusedSidebarTagPath(ReplaceTagPathPrefix(FocusedSidebarTagPath!, currentPath, replacement));
        }
        await _tagFolderCatalogService.SaveAsync(workspacePath, _tagFolderPaths);
        RefreshAvailableTags();
        RefreshSidebarTree();
        StatusMessage = $"Renamed folder to {replacement}.";
    }

    [RelayCommand]
    private async Task DeleteTagFolderAsync(string? folderPath)
    {
        var normalized = TagHierarchyHelper.TryNormalize(folderPath);
        var workspacePath = NotesFolder;
        if (normalized is null || !await _workspaceDialogService.ConfirmDeleteTagFolderAsync(normalized))
        {
            return;
        }
        if (!string.Equals(NotesFolder, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var nextFolderPaths = _tagFolderPaths
            .Where(path => !IsTagPathWithin(path, normalized))
            .ToList();
        if (!await MutateNoteTagsAsync(
            _allNotes.Select(note => note.FilePath),
            tags => tags.Where(tag => !IsTagPathWithin(tag, normalized)).ToList()))
        {
            return;
        }

        _tagFolderPaths = nextFolderPaths;
        RemoveSidebarExpansionStates(normalized);
        if (IsSidebarFolderFocused && IsTagPathWithin(FocusedSidebarTagPath!, normalized))
        {
            SetFocusedSidebarTagPath(null);
        }
        await _tagFolderCatalogService.SaveAsync(workspacePath, _tagFolderPaths);
        RefreshAvailableTags();
        RefreshSidebarTree();
        StatusMessage = $"Deleted folder {normalized}. Notes were kept.";
    }

    [RelayCommand]
    private async Task AddSelectedNotesToTagFolderAsync()
    {
        var selectedPaths = _selectedSidebarFilePaths.ToList();
        var destination = await _workspaceDialogService.ChooseTagFolderDestinationAsync(GetAllTagFolderPaths());
        if (destination is not null)
        {
            await AddSidebarNotesToTagFolderAsync(selectedPaths, destination);
        }
    }

    public async Task AddSidebarSelectionToTagFolderAsync(string folderPath)
    {
        await AddSidebarNotesToTagFolderAsync(_selectedSidebarFilePaths, folderPath);
    }

    public async Task AddSidebarNotesToTagFolderAsync(IEnumerable<string> filePaths, string folderPath)
    {
        var normalized = TagHierarchyHelper.TryNormalize(folderPath);
        var selectedPaths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (normalized is null || selectedPaths.Count == 0)
        {
            return;
        }

        if (!await MutateNoteTagsAsync(selectedPaths, tags =>
        {
            if (!tags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(normalized);
            }

            return tags;
        }))
        {
            return;
        }
        StatusMessage = $"Added {selectedPaths.Count} note{(selectedPaths.Count == 1 ? string.Empty : "s")} to {normalized}.";
    }

    [RelayCommand]
    private async Task MoveSelectedNotesToRootAsync()
    {
        await MoveSidebarSelectionToRootAsync();
    }

    public async Task MoveSidebarSelectionToRootAsync()
    {
        await MoveSidebarNotesToRootAsync(_selectedSidebarFilePaths);
    }

    public async Task MoveSidebarNotesToRootAsync(IEnumerable<string> filePaths)
    {
        var selectedPaths = filePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selectedPaths.Count == 0)
        {
            return;
        }

        if (!await MutateNoteTagsAsync(selectedPaths, _ => []))
        {
            return;
        }

        StatusMessage = $"Moved {selectedPaths.Count} note{(selectedPaths.Count == 1 ? string.Empty : "s")} to the root list.";
    }

    [RelayCommand]
    private async Task DeleteSelectedSidebarNotesAsync()
    {
        var selected = SelectedSidebarNotes;
        var workspacePath = NotesFolder;
        if (selected.Count == 0
            || !await _workspaceDialogService.ConfirmDeleteNotesAsync(selected.Select(note => note.DisplayName).ToList()))
        {
            return;
        }
        if (!string.Equals(NotesFolder, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var paths = selected.Select(note => note.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (CurrentNote is not null && paths.Contains(CurrentNote.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            CancelScheduledSave();
        }

        foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && paths.Contains(pane.CurrentNote.FilePath, StringComparer.OrdinalIgnoreCase)))
        {
            CancelPaneScheduledSave(pane);
        }

        SuppressWatcher();
        var deletedPaths = new List<string>();
        Exception? failure = null;
        await _notePersistenceLock.WaitAsync();
        _isApplyingSidebarBulkMutation = true;
        try
        {
            using (BeginMutationScope())
            {
                foreach (var path in paths)
                {
                    SuppressWatcher();
                    await _noteMutationService.DeleteIfExistsAsync(path);
                    deletedPaths.Add(path);
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            _isApplyingSidebarBulkMutation = false;
            _notePersistenceLock.Release();
        }

        if (!string.Equals(NotesFolder, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var path in deletedPaths)
        {
            _selectedSidebarFilePaths.Remove(path);
            ApplyDeletedNote(path, refreshCollections: false, preserveUnsavedOpenNotes: false);
        }

        RefreshAvailableTags();
        RefreshVisibleNotes();
        RefreshCalendarNoteDates();
        RefreshCalendarDays();
        StatusMessage = failure is null
            ? $"Deleted {deletedPaths.Count} note{(deletedPaths.Count == 1 ? string.Empty : "s")}."
            : $"Deleted {deletedPaths.Count} of {paths.Count} notes. Refresh and retry the remaining files.";
    }

    private async Task<bool> MutateNoteTagsAsync(
        IEnumerable<string> filePaths,
        Func<List<string>, List<string>> updateTags)
    {
        var workspacePath = NotesFolder;
        var paths = filePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => _allNotes.Any(note => string.Equals(note.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (CurrentNote is not null && paths.Contains(CurrentNote.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            CancelScheduledSave();
        }

        foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && paths.Contains(pane.CurrentNote.FilePath, StringComparer.OrdinalIgnoreCase)))
        {
            CancelPaneScheduledSave(pane);
        }

        var documents = new List<(string Path, NoteDocument Document)>();
        foreach (var path in paths)
        {
            var document = await GetDocumentForTagMutationAsync(path);
            if (document is null)
            {
                StatusMessage = "Fix invalid YAML in selected open notes before changing folders.";
                return false;
            }

            documents.Add((path, document));
        }

        var savedDocuments = new List<(string PreviousPath, NoteDocument Document)>();
        Exception? failure = null;
        SuppressWatcher();
        _isApplyingSidebarBulkMutation = true;
        try
        {
            using (BeginMutationScope())
            {
                foreach (var (filePath, document) in documents)
                {
                    var updatedTags = updateTags([.. document.Tags])
                        .Select(TagHierarchyHelper.TryNormalize)
                        .OfType<string>()
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (document.Tags.SequenceEqual(updatedTags, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var previousPath = document.FilePath;
                    document.Tags = updatedTags;
                    SuppressWatcher();
                    await _notePersistenceLock.WaitAsync();
                    NoteDocument saved;
                    try
                    {
                        saved = await _noteMutationService.SaveAsync(workspacePath, document, preserveTimestamp: true);
                    }
                    finally
                    {
                        _notePersistenceLock.Release();
                    }
                    savedDocuments.Add((previousPath, saved));
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            _isApplyingSidebarBulkMutation = false;
        }

        if (!string.Equals(NotesFolder, workspacePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var (previousPath, document) in savedDocuments)
        {
            if (_selectedSidebarFilePaths.Remove(previousPath))
            {
                _selectedSidebarFilePaths.Add(document.FilePath);
            }

            ReplaceSummary(previousPath, BuildSummary(document));
            if (CurrentNote is not null && string.Equals(CurrentNote.FilePath, previousPath, StringComparison.OrdinalIgnoreCase))
            {
                ApplyDocumentToEditor(document);
            }

            foreach (var pane in SecondaryPanes.Where(pane => pane.CurrentNote is not null && string.Equals(pane.CurrentNote.FilePath, previousPath, StringComparison.OrdinalIgnoreCase)))
            {
                ApplyDocumentToPane(pane, document);
            }
        }

        RefreshAvailableTags();
        RefreshVisibleNotes();
        RefreshCalendarNoteDates();
        RefreshCalendarDays();
        if (failure is not null)
        {
            StatusMessage = $"Updated {savedDocuments.Count} of {paths.Count} notes. Refresh and retry the remaining files.";
            return false;
        }

        return true;
    }

    private async Task<NoteDocument?> GetDocumentForTagMutationAsync(string filePath)
    {
        if (IsNoteConflicted(filePath))
        {
            StatusMessage = "Resolve the note conflict before changing its tags.";
            return null;
        }

        if (CurrentNote is not null && string.Equals(CurrentNote.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return UpdateCurrentNoteFromEditor() ? CurrentNote : null;
        }

        var pane = FindPaneByFilePath(filePath);
        if (pane?.CurrentNote is not null)
        {
            return UpdatePaneNoteFromEditor(pane) ? pane.CurrentNote : null;
        }

        return await _notesRepository.LoadNoteAsync(filePath);
    }

    private IReadOnlyList<string> GetAllTagFolderPaths()
    {
        return TagHierarchyHelper
            .ExpandWithAncestors(_tagFolderPaths.Concat(_allNotes.SelectMany(note => note.Tags)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsTagPathWithin(string candidate, string folderPath)
    {
        return string.Equals(candidate, folderPath, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(folderPath + '/', StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceTagPathPrefix(string candidate, string oldPath, string newPath)
    {
        if (string.Equals(candidate, oldPath, StringComparison.OrdinalIgnoreCase))
        {
            return newPath;
        }

        return candidate.StartsWith(oldPath + '/', StringComparison.OrdinalIgnoreCase)
            ? newPath + candidate[oldPath.Length..]
            : candidate;
    }

    private void RemapSidebarExpansionStates(string oldPath, string newPath)
    {
        var states = _sidebarTreeExpansionStates
            .Select(pair => new KeyValuePair<string, bool>(ReplaceTagPathPrefix(pair.Key, oldPath, newPath), pair.Value))
            .ToList();
        _sidebarTreeExpansionStates.Clear();
        foreach (var state in states)
        {
            _sidebarTreeExpansionStates[state.Key] = state.Value;
        }
    }

    private void RemoveSidebarExpansionStates(string folderPath)
    {
        foreach (var path in _sidebarTreeExpansionStates.Keys.Where(path => IsTagPathWithin(path, folderPath)).ToList())
        {
            _sidebarTreeExpansionStates.Remove(path);
        }
    }
}
