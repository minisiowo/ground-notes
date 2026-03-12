using QuickNotesTxt.Models;
using Xunit;

namespace QuickNotesTxt.Tests;

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
}
