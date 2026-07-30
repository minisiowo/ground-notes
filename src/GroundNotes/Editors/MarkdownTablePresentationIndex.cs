using AvaloniaEdit.Document;

namespace GroundNotes.Editors;

internal sealed class MarkdownTablePresentationIndex : IDisposable
{
    private TextDocument? _document;
    private IReadOnlyList<MarkdownTable>? _tables;
    private Dictionary<int, MarkdownTable>? _tablesByLine;

    public event EventHandler<MarkdownTablePresentationInvalidatedEventArgs>? Invalidated;

    public IReadOnlyList<MarkdownTable> GetTables(TextDocument document)
    {
        Attach(document);
        if (_tables is null)
        {
            BuildIndex(document);
        }

        return _tables!;
    }

    public MarkdownTable? GetTableForLine(TextDocument document, int lineNumber)
    {
        GetTables(document);
        return _tablesByLine!.GetValueOrDefault(lineNumber);
    }

    public void Invalidate()
    {
        _tables = null;
        _tablesByLine = null;
    }

    public void Dispose()
    {
        if (_document is not null)
        {
            _document.Changed -= OnDocumentChanged;
            _document = null;
        }

        Invalidate();
    }

    private void Attach(TextDocument document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        if (_document is not null)
        {
            _document.Changed -= OnDocumentChanged;
        }

        _document = document;
        _document.Changed += OnDocumentChanged;
        Invalidate();
    }

    private void BuildIndex(TextDocument document)
    {
        MarkdownDiagnostics.RecordTableParse();
        _tables = MarkdownTableParser.FindTables(document.Text);
        _tablesByLine = [];
        foreach (var table in _tables)
        {
            for (var lineNumber = table.StartLineNumber; lineNumber <= table.EndLineNumber; lineNumber++)
            {
                _tablesByLine[lineNumber] = table;
            }
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_document is null)
        {
            Invalidate();
            return;
        }

        var previousTables = _tables;
        var changedLine = _document.GetLineByOffset(Math.Min(e.Offset, _document.TextLength)).LineNumber;
        var previousTable = previousTables?.FirstOrDefault(table =>
            e.Offset <= table.Start + table.Length
            && e.Offset + e.RemovalLength >= table.Start);
        var currentLine = _document.GetLineByNumber(changedLine);
        var currentLineText = _document.GetText(currentLine.Offset, currentLine.Length);
        var couldChangeTableTopology = previousTable is not null
                                       || e.InsertedText.Text.Contains('|')
                                       || e.RemovedText.Text.Contains('|')
                                       || currentLineText.Contains('|')
                                       || HasPipeOnAdjacentLine(_document, changedLine - 1)
                                       || HasPipeOnAdjacentLine(_document, changedLine + 1);

        Invalidate();
        if (!couldChangeTableTopology)
        {
            return;
        }

        BuildIndex(_document);
        var tablesByLine = _tablesByLine!;
        var currentTable = tablesByLine.GetValueOrDefault(changedLine)
                           ?? tablesByLine.GetValueOrDefault(changedLine - 1)
                           ?? tablesByLine.GetValueOrDefault(changedLine + 1);
        var startLine = Math.Min(previousTable?.StartLineNumber ?? changedLine, currentTable?.StartLineNumber ?? changedLine);
        var endLine = Math.Max(previousTable?.EndLineNumber ?? changedLine, currentTable?.EndLineNumber ?? changedLine);
        Invalidated?.Invoke(this, new MarkdownTablePresentationInvalidatedEventArgs(startLine, endLine));
    }

    private static bool HasPipeOnAdjacentLine(TextDocument document, int lineNumber)
    {
        if (lineNumber < 1 || lineNumber > document.LineCount)
        {
            return false;
        }

        var line = document.GetLineByNumber(lineNumber);
        return document.GetText(line.Offset, line.Length).Contains('|');
    }
}

internal sealed record MarkdownTablePresentationInvalidatedEventArgs(int StartLine, int EndLine);
