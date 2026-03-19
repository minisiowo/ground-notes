using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using QuickNotesTxt.Editors;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Views;

internal sealed class EditorHostController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly MarkdownColorizingTransformer _colorizer;

    public EditorHostController(TextEditor editor, MarkdownColorizingTransformer colorizer)
    {
        _editor = editor;
        _colorizer = colorizer;

        _colorizer.RedrawRequested += OnColorizerRedrawRequested;

        _editor.Options.ConvertTabsToSpaces = false;
        _editor.Options.EnableRectangularSelection = false;
        _editor.Options.WordWrapIndentation = 0;
        _editor.Options.InheritWordWrapIndentation = false;
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);

        ApplySelectionTheme();
    }

    public bool IsUpdatingEditorFromViewModel { get; private set; }

    public bool IsUpdatingViewModelFromEditor { get; private set; }

    public string GetText()
    {
        return _editor.Document?.Text ?? string.Empty;
    }

    public void ApplySelectionTheme()
    {
        var resources = Application.Current?.Resources;
        _editor.TextArea.SelectionBrush = resources?[ThemeKeys.EditorTextSelectionBrush] as IBrush;
        _editor.TextArea.SelectionForeground = null;
        _editor.TextArea.SelectionBorder = null;
        _editor.TextArea.SelectionCornerRadius = 0;
    }

    public void RefreshThemeResources()
    {
        _colorizer.InvalidateResourceCache();
        ApplySelectionTheme();
        _editor.TextArea.TextView.InvalidateVisual();
    }

    public bool SyncFromViewModel(string? text, bool appendSuffixWhenPossible, out bool appendedOnly)
    {
        appendedOnly = false;
        if (IsUpdatingViewModelFromEditor)
        {
            return false;
        }

        var document = _editor.Document;
        if (document is null)
        {
            return false;
        }

        text ??= string.Empty;
        var currentText = document.Text;
        if (string.Equals(currentText, text, StringComparison.Ordinal))
        {
            return false;
        }

        var caretOffset = Math.Min(_editor.CaretOffset, text.Length);

        IsUpdatingEditorFromViewModel = true;
        try
        {
            if (appendSuffixWhenPossible && text.StartsWith(currentText, StringComparison.Ordinal))
            {
                var suffix = text[currentText.Length..];
                if (suffix.Length > 0)
                {
                    document.Insert(document.TextLength, suffix);
                    appendedOnly = true;
                }
            }
            else
            {
                document.BeginUpdate();
                try
                {
                    document.Replace(0, document.TextLength, text);
                }
                finally
                {
                    document.EndUpdate();
                }

                _editor.CaretOffset = caretOffset;
                _editor.Select(caretOffset, 0);
            }
        }
        finally
        {
            IsUpdatingEditorFromViewModel = false;
        }

        return true;
    }

    public bool SyncToViewModel(Func<string> getViewModelText, Action<string> setViewModelText)
    {
        ArgumentNullException.ThrowIfNull(getViewModelText);
        ArgumentNullException.ThrowIfNull(setViewModelText);

        if (IsUpdatingEditorFromViewModel)
        {
            return false;
        }

        var text = GetText();
        if (string.Equals(getViewModelText(), text, StringComparison.Ordinal))
        {
            return false;
        }

        IsUpdatingViewModelFromEditor = true;
        try
        {
            setViewModelText(text);
        }
        finally
        {
            IsUpdatingViewModelFromEditor = false;
        }

        return true;
    }

    public void Dispose()
    {
        _colorizer.RedrawRequested -= OnColorizerRedrawRequested;
        _editor.TextArea.TextView.LineTransformers.Remove(_colorizer);
    }

    private void OnColorizerRedrawRequested(object? sender, int startLine)
    {
        var document = _editor.Document;
        if (document is null || startLine > document.LineCount)
        {
            return;
        }

        var startLineSegment = document.GetLineByNumber(startLine);
        var lastLineSegment = document.GetLineByNumber(document.LineCount);
        _editor.TextArea.TextView.Redraw(startLineSegment.Offset, lastLineSegment.EndOffset - startLineSegment.Offset);
    }
}
