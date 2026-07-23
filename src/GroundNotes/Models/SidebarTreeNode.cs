namespace GroundNotes.Models;

public enum SidebarTreeNodeKind
{
    Folder,
    Note
}

public abstract class SidebarTreeNode
{
    protected SidebarTreeNode(string name, SidebarTreeNodeKind kind)
    {
        Name = name;
        Kind = kind;
    }

    public string Name { get; }

    public string DisplayName => Name;

    public SidebarTreeNodeKind Kind { get; }
}

public sealed class SidebarTreeFolderNode : SidebarTreeNode
{
    public SidebarTreeFolderNode(string name, string tagPath, IReadOnlyList<SidebarTreeNode> children)
        : base(name, SidebarTreeNodeKind.Folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagPath);
        ArgumentNullException.ThrowIfNull(children);

        TagPath = tagPath;
        Children = children;
    }

    public string TagPath { get; }

    public IReadOnlyList<SidebarTreeNode> Children { get; }
}

public sealed class SidebarTreeNoteNode : SidebarTreeNode
{
    public SidebarTreeNoteNode(string name, NoteSummary note)
        : base(name, SidebarTreeNodeKind.Note)
    {
        ArgumentNullException.ThrowIfNull(note);

        Note = note;
    }

    public NoteSummary Note { get; }
}
