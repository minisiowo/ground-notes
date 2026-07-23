using GroundNotes.Models;

namespace GroundNotes.Services;

public static class SidebarTreeBuilder
{
    public static IReadOnlyList<SidebarTreeNode> Build(IEnumerable<NoteSummary> notes, SortOption sortOption)
    {
        return Build(notes, sortOption, []);
    }

    public static IReadOnlyList<SidebarTreeNode> Build(
        IEnumerable<NoteSummary> notes,
        SortOption sortOption,
        IEnumerable<string> explicitFolderPaths)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(explicitFolderPaths);

        var root = new MutableFolder(string.Empty);

        foreach (var folderPath in explicitFolderPaths.Select(TryParsePath).OfType<TagPath>())
        {
            AddFolder(root, folderPath);
        }

        foreach (var note in notes)
        {
            ArgumentNullException.ThrowIfNull(note);

            var paths = GetMostSpecificPaths(note.Tags);
            if (paths.Count == 0)
            {
                root.Notes.Add(note);
                continue;
            }

            foreach (var path in paths)
            {
                AddNote(root, path, note);
            }
        }

        return BuildChildren(root, string.Empty, sortOption);
    }

    private static void AddNote(MutableFolder root, TagPath path, NoteSummary note)
    {
        var folder = AddFolder(root, path);
        folder.Notes.Add(note);
    }

    private static MutableFolder AddFolder(MutableFolder root, TagPath path)
    {
        var current = root;

        foreach (var segment in path.Segments)
        {
            if (!current.Folders.TryGetValue(segment, out var child))
            {
                child = new MutableFolder(segment);
                current.Folders.Add(segment, child);
            }
            else
            {
                child.ConsiderDisplayName(segment);
            }

            current = child;
        }

        return current;
    }

    private static IReadOnlyList<SidebarTreeNode> BuildChildren(
        MutableFolder folder,
        string parentPath,
        SortOption sortOption)
    {
        var children = new List<SidebarTreeNode>(folder.Folders.Count + folder.Notes.Count);

        foreach (var child in folder.Folders.Values
                     .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            var tagPath = parentPath.Length == 0
                ? child.DisplayName
                : $"{parentPath}/{child.DisplayName}";
            children.Add(new SidebarTreeFolderNode(
                child.DisplayName,
                tagPath,
                BuildChildren(child, tagPath, sortOption)));
        }

        children.AddRange(OrderNotes(folder.Notes, sortOption)
            .Select(note => new SidebarTreeNoteNode(GetDisplayName(note), note)));

        return children;
    }

    private static IEnumerable<NoteSummary> OrderNotes(IEnumerable<NoteSummary> notes, SortOption sortOption)
    {
        IOrderedEnumerable<NoteSummary> ordered = sortOption switch
        {
            SortOption.Title => notes
                .OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(note => note.UpdatedAt),
            SortOption.CreatedAt => notes
                .OrderByDescending(note => note.CreatedAt)
                .ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase),
            _ => notes
                .OrderByDescending(note => note.UpdatedAt)
                .ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
        };

        return ordered
            .ThenBy(GetDisplayName, StringComparer.Ordinal)
            .ThenBy(note => note.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(note => note.FilePath, StringComparer.Ordinal)
            .ThenBy(note => note.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(note => note.Id, StringComparer.Ordinal);
    }

    private static string GetDisplayName(NoteSummary note)
    {
        var displayName = Path.GetFileNameWithoutExtension(note.FilePath);
        return string.IsNullOrWhiteSpace(displayName) ? note.Title : displayName;
    }

    private static IReadOnlyList<TagPath> GetMostSpecificPaths(IEnumerable<string> tags)
    {
        var paths = tags
            .Select(TryParsePath)
            .OfType<TagPath>()
            .GroupBy(path => path.Normalized, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(path => path.Normalized, StringComparer.Ordinal).First())
            .ToList();

        return paths
            .Where(candidate => !paths.Any(other => IsAncestor(candidate, other)))
            .OrderBy(path => path.Normalized, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path.Normalized, StringComparer.Ordinal)
            .ToList();
    }

    private static TagPath? TryParsePath(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var segments = tag
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return segments.Length == 0 ? null : new TagPath(segments);
    }

    private static bool IsAncestor(TagPath candidate, TagPath other)
    {
        if (candidate.Segments.Count >= other.Segments.Count)
        {
            return false;
        }

        for (var index = 0; index < candidate.Segments.Count; index++)
        {
            if (!string.Equals(
                    candidate.Segments[index],
                    other.Segments[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class MutableFolder
    {
        public MutableFolder(string displayName)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; private set; }

        public Dictionary<string, MutableFolder> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<NoteSummary> Notes { get; } = [];

        public void ConsiderDisplayName(string displayName)
        {
            if (StringComparer.Ordinal.Compare(displayName, DisplayName) < 0)
            {
                DisplayName = displayName;
            }
        }
    }

    private sealed class TagPath
    {
        public TagPath(IReadOnlyList<string> segments)
        {
            Segments = segments;
            Normalized = string.Join('/', segments);
        }

        public IReadOnlyList<string> Segments { get; }

        public string Normalized { get; }
    }

}
