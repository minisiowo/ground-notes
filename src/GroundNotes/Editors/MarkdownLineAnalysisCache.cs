using AvaloniaEdit.Document;

namespace GroundNotes.Editors;

internal sealed class MarkdownLineAnalysisCache : IDisposable
{
    private TextDocument? _document;
    private readonly Dictionary<int, CacheEntry> _entries = [];

    public MarkdownLineAnalysis GetOrAdd(TextDocument document, int lineNumber, string lineText, MarkdownFenceState fenceStateBeforeLine)
    {
        Attach(document);

        if (_entries.TryGetValue(lineNumber, out var entry)
            && string.Equals(entry.LineText, lineText, StringComparison.Ordinal)
            && entry.FenceStateBeforeLine.Equals(fenceStateBeforeLine))
        {
            MarkdownDiagnostics.RecordAnalysisCacheHit();
            return entry.Analysis;
        }

        MarkdownDiagnostics.RecordAnalysisCacheMiss();
        var analysis = MarkdownLineParser.Analyze(lineText, fenceStateBeforeLine);
        _entries[lineNumber] = new CacheEntry(lineText, fenceStateBeforeLine, analysis);
        return analysis;
    }

    public void Invalidate()
    {
        _entries.Clear();
    }

    public void Dispose()
    {
        Detach();
    }

    private void Attach(TextDocument document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        Detach();
        _document = document;
        _document.Changed += OnDocumentChanged;
        Invalidate();
    }

    private void Detach()
    {
        if (_document is null)
        {
            return;
        }

        _document.Changed -= OnDocumentChanged;
        _document = null;
        Invalidate();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_document is null)
        {
            Invalidate();
            return;
        }

        var changedLine = _document.GetLineByOffset(Math.Min(e.Offset, _document.TextLength)).LineNumber;
        var keysToRemove = _entries.Keys.Where(key => key >= changedLine).ToArray();
        foreach (var key in keysToRemove)
        {
            _entries.Remove(key);
        }
    }

    private sealed record CacheEntry(string LineText, MarkdownFenceState FenceStateBeforeLine, MarkdownLineAnalysis Analysis);
}
