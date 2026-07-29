using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using GroundNotes.Styles;

namespace GroundNotes.Editors;

internal sealed class MarkdownTableColorizingTransformer : DocumentColorizingTransformer, IDisposable
{
    private TextDocument? _document;
    private IReadOnlyList<MarkdownTable>? _tables;

    public void Invalidate()
    {
        _tables = null;
    }

    public void Dispose()
    {
        if (_document is not null)
        {
            _document.Changed -= OnDocumentChanged;
            _document = null;
        }

        _tables = null;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var document = CurrentContext.Document;
        Attach(document);
        _tables ??= MarkdownTableParser.FindTables(document.Text);
        var table = _tables.FirstOrDefault(candidate => line.LineNumber >= candidate.StartLineNumber && line.LineNumber <= candidate.EndLineNumber);
        if (table is null)
        {
            return;
        }

        var row = table.Rows.FirstOrDefault(candidate => candidate.Start == line.Offset);
        if (row is null)
        {
            return;
        }

        if (row.IsDelimiter)
        {
            ApplyStyle(line.Offset, line.EndOffset, GetBrush(ThemeKeys.MarkdownRuleBrush), FontWeight.SemiBold);
            return;
        }

        if (row.Index == 0)
        {
            ApplyStyle(line.Offset, line.EndOffset, null, FontWeight.SemiBold);
        }

        var lineText = document.GetText(line.Offset, line.Length);
        var mutedBrush = GetBrush(ThemeKeys.MutedTextBrush);
        for (var i = 0; i < lineText.Length; i++)
        {
            if (lineText[i] == '|' && !IsEscaped(lineText, i))
            {
                ApplyStyle(line.Offset + i, line.Offset + i + 1, mutedBrush, null);
            }
        }
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
        _tables = null;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e) => _tables = null;

    private void ApplyStyle(int start, int end, IBrush? foreground, FontWeight? weight)
    {
        if (end <= start)
        {
            return;
        }

        ChangeLinePart(start, end, element =>
        {
            if (foreground is not null)
            {
                element.TextRunProperties.SetForegroundBrush(foreground);
            }

            if (weight is FontWeight fontWeight)
            {
                var current = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(current.FontFamily, current.Style, fontWeight, current.Stretch));
            }
        });
    }

    private static bool IsEscaped(string text, int index)
    {
        var backslashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 != 0;
    }

    private static IBrush? GetBrush(string key) => Application.Current?.Resources[key] as IBrush;
}
