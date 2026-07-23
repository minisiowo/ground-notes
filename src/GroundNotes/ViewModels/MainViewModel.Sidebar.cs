using System.Collections.ObjectModel;
using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

public partial class MainViewModel
{
    private IReadOnlyList<SidebarTreeNode> _sidebarTree = [];

    private void RefreshSidebarTree()
    {
        _sidebarTree = SidebarTreeBuilder.Build(VisibleNotes.Select(item => item.Summary), SelectedSortOption);
        RefreshVisibleSidebarRows();
    }

    private void RefreshVisibleSidebarRows()
    {
        var rows = new List<SidebarTreeRowViewModel>();
        var noteItems = VisibleNotes.ToDictionary(item => item.FilePath, StringComparer.OrdinalIgnoreCase);
        var activeFilePath = GetActiveSidebarFilePath();
        var forceExpand = !string.IsNullOrWhiteSpace(SearchText) || SelectedCalendarDate is not null;

        AddRows(_sidebarTree, depth: 0, parentOccurrencePath: string.Empty);
        VisibleSidebarRows = new ObservableCollection<SidebarTreeRowViewModel>(rows);

        var selectedOccurrence = SelectedSidebarRow?.OccurrencePath;
        SelectedSidebarRow = selectedOccurrence is null
            ? FindPreferredNoteRow(activeFilePath)
            : VisibleSidebarRows.FirstOrDefault(row => string.Equals(row.OccurrencePath, selectedOccurrence, StringComparison.Ordinal))
              ?? FindPreferredNoteRow(activeFilePath);

        return;

        void AddRows(IEnumerable<SidebarTreeNode> nodes, int depth, string parentOccurrencePath)
        {
            foreach (var node in nodes)
            {
                if (node is SidebarTreeFolderNode folder)
                {
                    var folderOccurrencePath = $"folder:{folder.TagPath}";
                    var containsActiveNote = !string.IsNullOrWhiteSpace(activeFilePath)
                                             && ContainsFilePath(folder, activeFilePath);
                    var isExpanded = forceExpand
                                     || containsActiveNote
                                     || (_sidebarTreeExpansionStates.TryGetValue(folder.TagPath, out var expanded) && expanded);
                    rows.Add(new SidebarTreeRowViewModel(
                        folder,
                        note: null,
                        depth,
                        folderOccurrencePath,
                        isExpanded,
                        OnSidebarFolderExpansionChanged));

                    if (isExpanded)
                    {
                        AddRows(folder.Children, depth + 1, folderOccurrencePath);
                    }

                    continue;
                }

                if (node is not SidebarTreeNoteNode noteNode
                    || !noteItems.TryGetValue(noteNode.Note.FilePath, out var noteItem))
                {
                    continue;
                }

                var noteOccurrencePath = $"note:{parentOccurrencePath}:{noteNode.Note.FilePath}";
                rows.Add(new SidebarTreeRowViewModel(
                    noteNode,
                    noteItem,
                    depth,
                    noteOccurrencePath,
                    isExpanded: false,
                    expansionChanged: null));
            }
        }
    }

    private void OnSidebarFolderExpansionChanged(string tagPath, bool isExpanded)
    {
        _sidebarTreeExpansionStates[tagPath] = isExpanded;
        RefreshVisibleSidebarRows();
    }

    private SidebarTreeRowViewModel? FindPreferredNoteRow(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        return VisibleSidebarRows.FirstOrDefault(row =>
            row.Note is not null
            && string.Equals(row.Note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsFilePath(SidebarTreeFolderNode folder, string filePath)
    {
        foreach (var child in folder.Children)
        {
            if (child is SidebarTreeNoteNode note
                && string.Equals(note.Note.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (child is SidebarTreeFolderNode childFolder && ContainsFilePath(childFolder, filePath))
            {
                return true;
            }
        }

        return false;
    }



    public bool HasVisibleSidebarRows => VisibleSidebarRows.Count > 0;

    public IReadOnlyList<string> ExpandedSidebarTagPaths => _sidebarTreeExpansionStates
        .Where(pair => pair.Value)
        .Select(pair => pair.Key)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public void RestoreSidebarTreeExpansion(IReadOnlyList<string>? tagPaths)
    {
        _sidebarTreeExpansionStates.Clear();
        foreach (var tagPath in tagPaths ?? [])
        {
            if (!string.IsNullOrWhiteSpace(tagPath))
            {
                _sidebarTreeExpansionStates[tagPath] = true;
            }
        }

        if (_sidebarTree.Count > 0)
        {
            RefreshVisibleSidebarRows();
        }
    }
}
