using GroundNotes.Models;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class NoteSummaryTests
{
    [Fact]
    public void PickerTagsText_IsEmptyWhenNoTagsExist()
    {
        var summary = new NoteSummary();

        Assert.False(summary.HasPickerTags);
        Assert.Equal(string.Empty, summary.PickerTagsText);
    }

    [Fact]
    public void PickerTagsText_UsesFirstThreeTagsAndOverflowCount()
    {
        var summary = new NoteSummary
        {
            Tags = ["project", "roadmap", "draft", "urgent"]
        };

        Assert.True(summary.HasPickerTags);
        Assert.Equal("project, roadmap, draft +1", summary.PickerTagsText);
    }

    [Fact]
    public void PickerPreviewText_TrimsWhitespace()
    {
        var summary = new NoteSummary
        {
            Preview = "  first line preview  "
        };

        Assert.True(summary.HasPickerPreview);
        Assert.Equal("first line preview", summary.PickerPreviewText);
    }

    [Fact]
    public void PickerPreviewText_IsEmptyWhenPreviewIsBlank()
    {
        var summary = new NoteSummary
        {
            Preview = "   "
        };

        Assert.False(summary.HasPickerPreview);
        Assert.Equal(string.Empty, summary.PickerPreviewText);
    }

    [Fact]
    public void FromDocument_MapsDocumentFieldsAndPreview()
    {
        var document = new NoteDocument
        {
            Id = "sample-id",
            FilePath = "/tmp/sample-note.txt",
            Title = "sample-note",
            Body = "first line\nsecond line",
            Tags = ["alpha", "beta"],
            CreatedAt = new DateTime(2026, 3, 9, 7, 33, 0, DateTimeKind.Local),
            UpdatedAt = new DateTime(2026, 3, 9, 7, 34, 0, DateTimeKind.Local)
        };

        var summary = NoteSummary.FromDocument(document);

        Assert.Equal(document.Id, summary.Id);
        Assert.Equal(document.FilePath, summary.FilePath);
        Assert.Equal(document.Title, summary.Title);
        Assert.Equal(document.Tags, summary.Tags);
        Assert.Equal(document.CreatedAt, summary.CreatedAt);
        Assert.Equal(document.UpdatedAt, summary.UpdatedAt);
        Assert.Equal("first line second line", summary.Preview);
        Assert.Equal("sample-note first line\nsecond line alpha beta", summary.SearchText);
    }

    [Fact]
    public void NoteListItemViewModel_UsesDisplayNameAsInitialRenameText()
    {
        var summary = new NoteSummary
        {
            FilePath = "/tmp/renamed-note.txt",
            Title = "renamed-note"
        };

        var item = new NoteListItemViewModel(summary);

        Assert.Equal("renamed-note", item.RenameText);
        Assert.False(item.IsRenaming);
    }
}
