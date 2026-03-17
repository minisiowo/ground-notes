using AvaloniaEdit.Document;
using QuickNotesTxt.Editors;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class MarkdownDiagnosticsTests
{
    [Fact]
    public void Analyze_RecordsLineAnalysisCount()
    {
        MarkdownDiagnostics.Reset();

        MarkdownLineParser.Analyze("# Title", MarkdownFenceState.None);
        MarkdownLineParser.Analyze("Plain text", MarkdownFenceState.None);

        var snapshot = MarkdownDiagnostics.Snapshot();
        Assert.Equal(2, snapshot.LinesAnalyzed);
    }

    [Fact]
    public void AnalysisCache_RecordsHitsAndMisses()
    {
        var document = new TextDocument("# Title");
        using var cache = new MarkdownLineAnalysisCache();

        MarkdownDiagnostics.Reset();

        _ = cache.GetOrAdd(document, 1, "# Title", MarkdownFenceState.None);
        _ = cache.GetOrAdd(document, 1, "# Title", MarkdownFenceState.None);

        var snapshot = MarkdownDiagnostics.Snapshot();
        Assert.True(snapshot.AnalysisCacheMisses >= 1);
        Assert.True(snapshot.AnalysisCacheHits >= 1);
        Assert.True(snapshot.LinesAnalyzed >= 1);
    }

    [Fact]
    public void FenceTracker_RecordsHitsMissesAndInvalidations()
    {
        var document = new TextDocument("before\n```\ninside\n```\nafter");
        using var tracker = new MarkdownFenceStateTracker();

        MarkdownDiagnostics.Reset();

        _ = tracker.GetStateBeforeLine(document, 5);
        _ = tracker.GetStateBeforeLine(document, 5);
        document.Replace(document.GetLineByNumber(4).Offset, 3, "---");
        _ = tracker.GetStateBeforeLine(document, 5);

        var snapshot = MarkdownDiagnostics.Snapshot();
        Assert.True(snapshot.FenceCacheHits >= 1);
        Assert.True(snapshot.FenceCacheMisses >= 2);
        Assert.True(snapshot.FenceInvalidations >= 1);
    }
}
