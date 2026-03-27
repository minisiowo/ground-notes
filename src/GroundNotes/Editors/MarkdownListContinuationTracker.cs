using AvaloniaEdit.Document;

namespace GroundNotes.Editors;

internal sealed class MarkdownListContinuationTracker : IDisposable
{
    private readonly MarkdownLineAnalysisCache _analysisCache;
    private readonly MarkdownFenceStateTracker _fenceStateTracker;
    private TextDocument? _document;
    private readonly Dictionary<int, int?> _continuationStartByLine = [];
    private int _highestComputedLine = 1;

    public event EventHandler<int>? RedrawRequested;

    public MarkdownListContinuationTracker(MarkdownLineAnalysisCache analysisCache, MarkdownFenceStateTracker fenceStateTracker)
    {
        _analysisCache = analysisCache;
        _fenceStateTracker = fenceStateTracker;
    }

    public int? GetContinuationStartColumn(TextDocument document, int lineNumber)
    {
        Attach(document);

        if (lineNumber <= 0)
        {
            return null;
        }

        if (_continuationStartByLine.TryGetValue(lineNumber, out var cached))
        {
            return cached;
        }

        var currentLineNumber = _highestComputedLine;

        while (currentLineNumber < lineNumber)
        {
            currentLineNumber++;
            _continuationStartByLine[currentLineNumber] = ComputeContinuationStartColumn(document, currentLineNumber);
        }

        _highestComputedLine = Math.Max(_highestComputedLine, lineNumber);
        return _continuationStartByLine[lineNumber];
    }

    public void Invalidate()
    {
        _continuationStartByLine.Clear();
        _continuationStartByLine[1] = ComputeInitialLineState();
        _highestComputedLine = 1;
        RedrawRequested?.Invoke(this, 1);
    }

    public void Dispose()
    {
        Detach();
    }

    private int? ComputeInitialLineState() => null;

    private int? ComputeContinuationStartColumn(TextDocument document, int lineNumber)
    {
        var line = document.GetLineByNumber(lineNumber);
        var lineText = document.GetText(line.Offset, line.Length);
        if (string.IsNullOrEmpty(lineText))
        {
            return null;
        }

        var fenceState = _fenceStateTracker.GetStateBeforeLine(document, lineNumber);
        var analysis = _analysisCache.GetOrAdd(document, lineNumber, lineText, fenceState);
        if (analysis.IsFencedCodeLine)
        {
            return null;
        }

        var directTextRange = analysis.TaskList?.Text ?? analysis.ListMarker?.Text;
        if (directTextRange.HasValue)
        {
            return directTextRange.Value.Start;
        }

        if (BreaksListContinuation(analysis))
        {
            return null;
        }

        return _continuationStartByLine.GetValueOrDefault(lineNumber - 1);
    }

    private static bool BreaksListContinuation(MarkdownLineAnalysis analysis)
    {
        return analysis.Heading is not null
            || analysis.HorizontalRule is not null
            || analysis.Blockquote is not null;
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
        InvalidateFromLine(changedLine);
    }

    private void InvalidateFromLine(int lineNumber)
    {
        if (lineNumber <= 1)
        {
            Invalidate();
            return;
        }

        var keysToRemove = _continuationStartByLine.Keys
            .Where(key => key >= lineNumber)
            .ToArray();

        foreach (var key in keysToRemove)
        {
            _continuationStartByLine.Remove(key);
        }

        _highestComputedLine = _continuationStartByLine.Count == 0 ? 1 : _continuationStartByLine.Keys.Max();
        _continuationStartByLine[1] = ComputeInitialLineState();
        RedrawRequested?.Invoke(this, lineNumber);
    }
}
