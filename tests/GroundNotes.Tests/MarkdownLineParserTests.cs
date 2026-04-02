using AvaloniaEdit.Document;
using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownLineParserTests
{
    [Fact]
    public void Analyze_DetectsHeadingWithClosingMarkers()
    {
        var analysis = MarkdownLineParser.Analyze("## Heading ##", MarkdownFenceState.None);

        var heading = Assert.IsType<MarkdownHeadingMatch>(analysis.Heading);
        Assert.Equal(2, heading.Level);
        Assert.Equal("Heading", "## Heading ##"[heading.Text.Start..heading.Text.End]);
        Assert.True(heading.Closing.HasValue);
    }

    [Fact]
    public void Analyze_SkipsInlineMarkdownInsideCodeSpans()
    {
        var analysis = MarkdownLineParser.Analyze("`[link](https://example.com)` and ~~real~~", MarkdownFenceState.None);

        Assert.Empty(analysis.Links);
        Assert.Single(analysis.StrikethroughSpans);
        Assert.Single(analysis.InlineCodeSpans);
    }

    [Fact]
    public void Analyze_DetectsMarkdownLinksAndBareUrlsSeparately()
    {
        var analysis = MarkdownLineParser.Analyze("See [docs](https://example.com) and https://example.org", MarkdownFenceState.None);

        var link = Assert.Single(analysis.Links);
        Assert.Equal("docs", "See [docs](https://example.com) and https://example.org"[link.Label.Start..link.Label.End]);
        var bareUrl = Assert.Single(analysis.BareUrls);
        Assert.Equal("https://example.org", "See [docs](https://example.com) and https://example.org"[bareUrl.Start..bareUrl.End]);
    }

    [Fact]
    public void Analyze_DetectsMarkdownImagesWithOptionalScale()
    {
        const string line = "![](assets/photo.png)|50";

        var analysis = MarkdownLineParser.Analyze(line, MarkdownFenceState.None);

        var image = Assert.Single(analysis.Images);
        Assert.Equal("assets/photo.png", line[image.Url.Start..image.Url.End]);
        Assert.Equal(50, image.ScalePercent);
        Assert.True(image.IsStandalone);
        Assert.Empty(analysis.Links);
        Assert.Empty(analysis.BareUrls);
    }

    [Fact]
    public void Analyze_DetectsMarkdownImagesWithoutScale()
    {
        const string line = "  ![cover](assets/cover.png)  ";

        var analysis = MarkdownLineParser.Analyze(line, MarkdownFenceState.None);

        var image = Assert.Single(analysis.Images);
        Assert.Equal("cover", line[image.AltText.Start..image.AltText.End]);
        Assert.Null(image.ScalePercent);
        Assert.True(image.IsStandalone);
    }

    [Fact]
    public void Analyze_DoesNotTreatImagesAsLinks()
    {
        const string line = "![cover](assets/cover.png) and [docs](https://example.com)";

        var analysis = MarkdownLineParser.Analyze(line, MarkdownFenceState.None);

        Assert.Single(analysis.Images);
        Assert.Single(analysis.Links);
    }

    [Fact]
    public void Analyze_DetectsMultipleLinksWithoutBareUrlDuplication()
    {
        const string line = "[one](https://one.example) and [two](https://two.example)";

        var analysis = MarkdownLineParser.Analyze(line, MarkdownFenceState.None);

        Assert.Equal(2, analysis.Links.Count);
        Assert.Empty(analysis.BareUrls);
    }

    [Fact]
    public void Analyze_DetectsTaskListStateAndText()
    {
        var analysis = MarkdownLineParser.Analyze("    - [x] done item", MarkdownFenceState.None);

        var task = Assert.IsType<MarkdownListMatch>(analysis.TaskList);
        Assert.True(task.IsChecked);
        Assert.Equal(4, task.IndentLength);
        Assert.NotNull(task.Text);
        Assert.Equal("done item", "    - [x] done item"[task.Text!.Value.Start..task.Text.Value.End]);
        Assert.Null(analysis.ListMarker);
    }

    [Fact]
    public void TryGetTaskListMatch_ReturnsCheckboxRange()
    {
        var task = MarkdownLineParser.TryGetTaskListMatch("  - [ ] todo item");

        var match = Assert.IsType<MarkdownListMatch>(task);
        Assert.NotNull(match.Checkbox);
        Assert.Equal("[ ]", "  - [ ] todo item"[match.Checkbox!.Value.Start..match.Checkbox.Value.End]);
    }

    [Fact]
    public void Analyze_DetectsBulletListTextStart()
    {
        var analysis = MarkdownLineParser.Analyze("  - list item", MarkdownFenceState.None);

        var list = Assert.IsType<MarkdownListMatch>(analysis.ListMarker);
        Assert.NotNull(list.Text);
        Assert.Equal("list item", "  - list item"[list.Text!.Value.Start..list.Text.Value.End]);
    }

    [Fact]
    public void Analyze_DetectsOrderedListTextStart()
    {
        var analysis = MarkdownLineParser.Analyze("  12. ordered item", MarkdownFenceState.None);

        var list = Assert.IsType<MarkdownListMatch>(analysis.ListMarker);
        Assert.NotNull(list.Text);
        Assert.Equal("ordered item", "  12. ordered item"[list.Text!.Value.Start..list.Text.Value.End]);
    }

    [Fact]
    public void Analyze_TreatsLinesInsideFenceAsCodeOnly()
    {
        var analysis = MarkdownLineParser.Analyze("## not a heading", new MarkdownFenceState(true, '`', 3));

        Assert.True(analysis.IsFencedCodeLine);
        Assert.Null(analysis.Heading);
        Assert.Empty(analysis.BoldSpans);
    }

    [Fact]
    public void AdvanceFenceState_OpensAndClosesMatchingFence()
    {
        var state = MarkdownLineParser.AdvanceFenceState(MarkdownFenceState.None, "```csharp");
        Assert.True(state.IsInsideFence);

        state = MarkdownLineParser.AdvanceFenceState(state, "``` ");
        Assert.False(state.IsInsideFence);
    }

    [Fact]
    public void AdvanceFenceState_ClosesOnlyWithMatchingMarkerType()
    {
        var state = MarkdownLineParser.AdvanceFenceState(MarkdownFenceState.None, "~~~json");
        Assert.True(state.IsInsideFence);

        state = MarkdownLineParser.AdvanceFenceState(state, "```");
        Assert.True(state.IsInsideFence);

        state = MarkdownLineParser.AdvanceFenceState(state, "~~~");
        Assert.False(state.IsInsideFence);
    }

    [Fact]
    public void FenceTracker_ReusesComputedStateUntilDocumentChanges()
    {
        var document = new TextDocument("# Title\n```csharp\ncode\n```\ntext");
        using var tracker = new MarkdownFenceStateTracker();

        var stateBeforeLine4 = tracker.GetStateBeforeLine(document, 4);
        Assert.True(stateBeforeLine4.IsInsideFence);

        document.Insert(document.GetLineByNumber(2).Offset, "```\n");

        var stateBeforeLine4AfterEdit = tracker.GetStateBeforeLine(document, 4);
        Assert.False(stateBeforeLine4AfterEdit.IsInsideFence);
    }

    [Fact]
    public void FenceTracker_InvalidatesOnlyFromChangedLineForward()
    {
        var document = new TextDocument("before\n```\ninside\n```\nafter");
        using var tracker = new MarkdownFenceStateTracker();

        var stateBeforeLine2 = tracker.GetStateBeforeLine(document, 2);
        var stateBeforeLine5 = tracker.GetStateBeforeLine(document, 5);

        Assert.False(stateBeforeLine2.IsInsideFence);
        Assert.False(stateBeforeLine5.IsInsideFence);

        document.Replace(document.GetLineByNumber(4).Offset, 3, "---");

        var stateBeforeLine2AfterEdit = tracker.GetStateBeforeLine(document, 2);
        var stateBeforeLine5AfterEdit = tracker.GetStateBeforeLine(document, 5);

        Assert.False(stateBeforeLine2AfterEdit.IsInsideFence);
        Assert.True(stateBeforeLine5AfterEdit.IsInsideFence);
    }

    [Fact]
    public void LineAnalysisCache_ReusesAnalysisForUnchangedLine()
    {
        var document = new TextDocument("# Title\ntext");
        using var cache = new MarkdownLineAnalysisCache();

        var first = cache.GetOrAdd(document, 1, "# Title", MarkdownFenceState.None);
        var second = cache.GetOrAdd(document, 1, "# Title", MarkdownFenceState.None);

        Assert.Same(first, second);
    }

    [Fact]
    public void LineAnalysisCache_InvalidatesChangedLineAndFollowingLines()
    {
        var document = new TextDocument("before\n```\ninside\n```\nafter");
        using var fenceTracker = new MarkdownFenceStateTracker();
        using var cache = new MarkdownLineAnalysisCache();

        var line2State = fenceTracker.GetStateBeforeLine(document, 2);
        var line5State = fenceTracker.GetStateBeforeLine(document, 5);
        var line2Before = cache.GetOrAdd(document, 2, document.GetText(document.GetLineByNumber(2)), line2State);
        var line5Before = cache.GetOrAdd(document, 5, document.GetText(document.GetLineByNumber(5)), line5State);

        document.Replace(document.GetLineByNumber(4).Offset, 3, "---");

        var line2After = cache.GetOrAdd(document, 2, document.GetText(document.GetLineByNumber(2)), fenceTracker.GetStateBeforeLine(document, 2));
        var line5After = cache.GetOrAdd(document, 5, document.GetText(document.GetLineByNumber(5)), fenceTracker.GetStateBeforeLine(document, 5));

        Assert.Same(line2Before, line2After);
        Assert.NotSame(line5Before, line5After);
        Assert.True(line5After.IsFencedCodeLine);
    }
}
