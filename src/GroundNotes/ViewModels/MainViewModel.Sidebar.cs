using System.Collections.ObjectModel;
using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

public partial class MainViewModel
{
    private IReadOnlyList<SidebarTreeNode> _sidebarTree = [];

    private void RefreshSidebarTree()
    {
        var explicitFolderPaths = string.IsNullOrWhiteSpace(FocusedSidebarTagPath)
            ? _tagFolderPaths
            : _tagFolderPaths.Append(FocusedSidebarTagPath);
        _sidebarTree = SidebarTreeBuilder.Build(VisibleNotes.Select(item => item.Summary), SelectedSortOption, explicitFolderPaths);
        RefreshVisibleSidebarRows();
    }

    private void RefreshVisibleSidebarRows()
    {
        var rows = new List<SidebarTreeRowViewModel>();
        var noteItems = VisibleNotes.ToDictionary(item => item.FilePath, StringComparer.OrdinalIgnoreCase);
        var activeFilePath = GetActiveSidebarFilePath();
        var forceExpand = !string.IsNullOrWhiteSpace(SearchText) || SelectedCalendarDate is not null;
        var selectedOccurrence = SelectedSidebarRow?.OccurrencePath;
        var focusedFolder = FindSidebarFolder(_sidebarTree, FocusedSidebarTagPath);
        var rootNodes = focusedFolder?.Children ?? _sidebarTree;
        var rootOccurrencePath = focusedFolder is null ? string.Empty : $"folder:{focusedFolder.TagPath}";

        AddRows(rootNodes, depth: 0, parentOccurrencePath: rootOccurrencePath);

        if (rows.Count == VisibleSidebarRows.Count
            && rows.Select(row => row.OccurrencePath).SequenceEqual(
                VisibleSidebarRows.Select(row => row.OccurrencePath),
                StringComparer.Ordinal))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                VisibleSidebarRows[i].IsExpanded = rows[i].IsExpanded;
            }

            SelectedSidebarRow = selectedOccurrence is null
                ? FindPreferredNoteRow(activeFilePath)
                : VisibleSidebarRows.FirstOrDefault(row => string.Equals(row.OccurrencePath, selectedOccurrence, StringComparison.Ordinal))
                  ?? FindPreferredNoteRow(activeFilePath);
            return;
        }

        VisibleSidebarRows = new ObservableCollection<SidebarTreeRowViewModel>(rows);
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
                    var isExpanded = _sidebarTreeExpansionStates.TryGetValue(folder.TagPath, out var expanded)
                        ? expanded
                        : forceExpand;
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

    private static SidebarTreeFolderNode? FindSidebarFolder(IEnumerable<SidebarTreeNode> nodes, string? tagPath)
    {
        if (string.IsNullOrWhiteSpace(tagPath))
        {
            return null;
        }

        foreach (var folder in nodes.OfType<SidebarTreeFolderNode>())
        {
            if (string.Equals(folder.TagPath, tagPath, StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }

            var nested = FindSidebarFolder(folder.Children, tagPath);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
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
