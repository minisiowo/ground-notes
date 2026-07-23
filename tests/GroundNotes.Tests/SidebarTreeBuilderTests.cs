using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class SidebarTreeBuilderTests
{
    [Fact]
    public void Build_CreatesVirtualHierarchyAndKeepsUntaggedNotesAtRoot()
    {
        var untagged = CreateNote("untagged", tags: []);
        var direct = CreateNote("direct", tags: ["Projects"]);
        var nested = CreateNote("nested", tags: [" Projects / Alpha "]);

        var tree = SidebarTreeBuilder.Build([nested, untagged, direct], SortOption.Title);

        var projects = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(tree.OfType<SidebarTreeFolderNode>()));
        Assert.Equal("Projects", projects.Name);
        Assert.Equal("Projects", projects.TagPath);
        Assert.Equal("direct", Assert.IsType<SidebarTreeNoteNode>(projects.Children[1]).Name);

        var alpha = Assert.IsType<SidebarTreeFolderNode>(projects.Children[0]);
        Assert.Equal("Projects/Alpha", alpha.TagPath);
        Assert.Equal("nested", Assert.IsType<SidebarTreeNoteNode>(Assert.Single(alpha.Children)).Name);
        Assert.Equal("untagged", Assert.IsType<SidebarTreeNoteNode>(tree[1]).Name);
    }

    [Fact]
    public void Build_KeepsOnlyMostSpecificTagInEachBranch()
    {
        var note = CreateNote("plan", tags: ["Work", "work/Project", "Personal"]);

        var tree = SidebarTreeBuilder.Build([note], SortOption.Title);

        var work = Assert.IsType<SidebarTreeFolderNode>(tree[1]);
        Assert.DoesNotContain(work.Children, node => node is SidebarTreeNoteNode);
        var project = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(work.Children));
        Assert.Same(note, Assert.IsType<SidebarTreeNoteNode>(Assert.Single(project.Children)).Note);

        var personal = Assert.IsType<SidebarTreeFolderNode>(tree[0]);
        Assert.Same(note, Assert.IsType<SidebarTreeNoteNode>(Assert.Single(personal.Children)).Note);
    }

    [Fact]
    public void Build_MergesFolderIdentityIgnoringCaseWithDeterministicDisplayCasing()
    {
        var lowerCase = CreateNote("lower", tags: ["work/alpha"]);
        var upperCase = CreateNote("upper", tags: ["Work/ALPHA"]);

        var forward = SidebarTreeBuilder.Build([lowerCase, upperCase], SortOption.Title);
        var reverse = SidebarTreeBuilder.Build([upperCase, lowerCase], SortOption.Title);

        var forwardWork = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(forward));
        var reverseWork = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(reverse));
        Assert.Equal("Work", forwardWork.Name);
        Assert.Equal("Work", reverseWork.Name);

        var forwardAlpha = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(forwardWork.Children));
        var reverseAlpha = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(reverseWork.Children));
        Assert.Equal("ALPHA", forwardAlpha.Name);
        Assert.Equal("ALPHA", reverseAlpha.Name);
        Assert.Equal("Work/ALPHA", forwardAlpha.TagPath);
        Assert.Equal(2, forwardAlpha.Children.Count);
    }

    [Fact]
    public void Build_OrdersFoldersBeforeNotesAndFoldersAlphabetically()
    {
        var rootNote = CreateNote("aardvark", tags: []);
        var zebra = CreateNote("zebra-note", tags: ["zebra"]);
        var alpha = CreateNote("alpha-note", tags: ["Alpha"]);

        var tree = SidebarTreeBuilder.Build([rootNote, zebra, alpha], SortOption.Title);

        Assert.Equal(
            new[] { "Alpha", "zebra", "aardvark" },
            tree.Select(node => node.Name).ToArray());
        Assert.Equal(
            [SidebarTreeNodeKind.Folder, SidebarTreeNodeKind.Folder, SidebarTreeNodeKind.Note],
            tree.Select(node => node.Kind).ToArray());
    }

    [Theory]
    [InlineData(SortOption.Title, "alpha", "bravo", "charlie")]
    [InlineData(SortOption.CreatedAt, "bravo", "charlie", "alpha")]
    [InlineData(SortOption.LastModified, "charlie", "alpha", "bravo")]
    public void Build_OrdersNotesUsingSidebarSortSemantics(
        SortOption sortOption,
        string first,
        string second,
        string third)
    {
        var baseline = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var alpha = CreateNote("alpha", [], baseline.AddDays(1), baseline.AddDays(2));
        var bravo = CreateNote("bravo", [], baseline.AddDays(3), baseline.AddDays(1));
        var charlie = CreateNote("charlie", [], baseline.AddDays(2), baseline.AddDays(3));

        var tree = SidebarTreeBuilder.Build([alpha, bravo, charlie], sortOption);

        Assert.Equal(new[] { first, second, third }, tree.Select(node => node.Name).ToArray());
    }

    [Fact]
    public void Build_TreatsBlankAndSeparatorOnlyTagsAsUntagged()
    {
        var note = CreateNote("root", tags: [" ", " /// "]);

        var tree = SidebarTreeBuilder.Build([note], SortOption.Title);

        Assert.Same(note, Assert.IsType<SidebarTreeNoteNode>(Assert.Single(tree)).Note);
    }

    [Fact]
    public void Build_IncludesEmptyExplicitFolder()
    {
        var tree = SidebarTreeBuilder.Build([], SortOption.Title, ["Archive"]);

        var archive = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(tree));
        Assert.Equal("Archive", archive.Name);
        Assert.Equal("Archive", archive.TagPath);
        Assert.Empty(archive.Children);
    }

    [Fact]
    public void Build_IncludesAncestorsOfNestedExplicitFoldersAndPreservesOrdering()
    {
        var rootNote = CreateNote("root", tags: []);

        var tree = SidebarTreeBuilder.Build(
            [rootNote],
            SortOption.Title,
            [" Projects / Empty / Child ", "archive"]);

        Assert.Equal(new[] { "archive", "Projects", "root" }, tree.Select(node => node.Name));
        var projects = Assert.IsType<SidebarTreeFolderNode>(tree[1]);
        var empty = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(projects.Children));
        var child = Assert.IsType<SidebarTreeFolderNode>(Assert.Single(empty.Children));
        Assert.Equal("Projects/Empty/Child", child.TagPath);
        Assert.Empty(child.Children);
    }

    private static NoteSummary CreateNote(
        string name,
        IReadOnlyList<string> tags,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        return new NoteSummary
        {
            Id = name,
            FilePath = $"/notes/{name}.md",
            Title = name,
            Tags = tags,
            CreatedAt = createdAt ?? DateTime.UnixEpoch,
            UpdatedAt = updatedAt ?? DateTime.UnixEpoch
        };
    }
}
