using AvaloniaEdit.Document;

namespace QuickNotesTxt.Editors;

internal sealed class MarkdownFenceStateTracker : IDisposable
{
    private TextDocument? _document;
    private readonly Dictionary<int, MarkdownFenceState> _stateBeforeLine = [];
    private int _highestComputedLine = 1;

    public event EventHandler<int>? RedrawRequested;

    public MarkdownFenceState GetStateBeforeLine(TextDocument document, int lineNumber)
    {
        Attach(document);

        if (lineNumber <= 1)
        {
            return MarkdownFenceState.None;
        }

        if (_stateBeforeLine.TryGetValue(lineNumber, out var cached))
        {
            MarkdownDiagnostics.RecordFenceCacheHit();
            return cached;
        }

        MarkdownDiagnostics.RecordFenceCacheMiss();
        var currentLineNumber = _highestComputedLine;
        var currentState = _stateBeforeLine[currentLineNumber];

        while (currentLineNumber < lineNumber)
        {
            var line = document.GetLineByNumber(currentLineNumber);
            var lineText = document.GetText(line.Offset, line.Length);
            currentState = MarkdownLineParser.AdvanceFenceState(currentState, lineText);
            currentLineNumber++;
            _stateBeforeLine[currentLineNumber] = currentState;
        }

        _highestComputedLine = Math.Max(_highestComputedLine, lineNumber);
        return currentState;
    }

    public void Invalidate()
    {
        MarkdownDiagnostics.RecordFenceInvalidation();
        _stateBeforeLine.Clear();
        _stateBeforeLine[1] = MarkdownFenceState.None;
        _highestComputedLine = 1;
        RedrawRequested?.Invoke(this, 1);
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
        InvalidateFromLine(changedLine);
    }

    private void InvalidateFromLine(int lineNumber)
    {
        if (lineNumber <= 1)
        {
            Invalidate();
            return;
        }

        MarkdownDiagnostics.RecordFenceInvalidation();

        var keysToRemove = _stateBeforeLine.Keys
            .Where(key => key >= lineNumber)
            .ToArray();

        foreach (var key in keysToRemove)
        {
            _stateBeforeLine.Remove(key);
        }

        _highestComputedLine = _stateBeforeLine.Count == 0 ? 1 : _stateBeforeLine.Keys.Max();
        _stateBeforeLine[1] = MarkdownFenceState.None;
        RedrawRequested?.Invoke(this, lineNumber);
    }
}
