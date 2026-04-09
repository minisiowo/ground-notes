using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using GroundNotes.Editors;

namespace GroundNotes.Views;

internal sealed class EditorMarkdownListController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly MarkdownColorizingTransformer _colorizer;
    private bool _markdownFormattingEnabled = true;

    public EditorMarkdownListController(TextEditor editor, MarkdownColorizingTransformer colorizer)
    {
        _editor = editor;
        _colorizer = colorizer;
        _editor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
    }

    public void Dispose()
    {
        _editor.RemoveHandler(InputElement.KeyDownEvent, OnEditorKeyDown);
    }

    public void SetMarkdownFormattingEnabled(bool enabled)
    {
        _markdownFormattingEnabled = enabled;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _editor.Document is null || !_markdownFormattingEnabled)
        {
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            var lineNumber = _editor.Document.GetLineByOffset(Math.Min(_editor.CaretOffset, Math.Max(_editor.Document.TextLength - 1, 0))).LineNumber;
            if (!_colorizer.QueryIsFencedCodeLine(_editor.Document, lineNumber)
                && MarkdownListEditingCommands.TryInsertListItemBreak(
                    _editor.Document.Text,
                    _editor.CaretOffset,
                    _editor.SelectionLength,
                    Math.Max(1, _editor.Options.IndentationSize),
                    out var enterEdit))
            {
                ApplyEdit(enterEdit);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Back && e.KeyModifiers == KeyModifiers.None)
        {
            var lookupOffset = _editor.CaretOffset == 0 ? 0 : Math.Min(_editor.CaretOffset - 1, _editor.Document.TextLength - 1);
            var lineNumber = _editor.Document.GetLineByOffset(lookupOffset).LineNumber;
            if (MarkdownListEditingCommands.ShouldSuppressBackspaceInListPrefix(
                _editor.Document.Text,
                _editor.CaretOffset,
                _editor.SelectionLength))
            {
                e.Handled = true;
                return;
            }

            if (!_colorizer.QueryIsFencedCodeLine(_editor.Document, lineNumber)
                && MarkdownListEditingCommands.TryBackspaceListIndentation(
                    _editor.Document.Text,
                    _editor.CaretOffset,
                    _editor.SelectionLength,
                    Math.Max(1, _editor.Options.IndentationSize),
                    out var backspaceEdit))
            {
                ApplyEdit(backspaceEdit);
                if (string.IsNullOrWhiteSpace(backspaceEdit.Replacement))
                {
                    _colorizer.SuppressListContinuationForLine(lineNumber);
                }

                e.Handled = true;
            }
        }
    }

    private void ApplyEdit(MarkdownEditResult edit)
    {
        var document = _editor.Document;
        if (document is null)
        {
            return;
        }

        var start = Math.Clamp(edit.Start, 0, document.TextLength);
        var length = Math.Clamp(edit.Length, 0, document.TextLength - start);
        document.Replace(start, length, edit.Replacement);

        var selectionStart = Math.Clamp(edit.SelectionStart, 0, document.TextLength);
        var selectionLength = Math.Clamp(edit.SelectionLength, 0, document.TextLength - selectionStart);
        _editor.Select(selectionStart, selectionLength);
        _editor.CaretOffset = selectionStart + selectionLength;
        _editor.Focus();
    }
}
