using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using QuickNotesTxt.Editors;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Views;

internal sealed class EditorThemeController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly MarkdownColorizingTransformer _colorizer;

    public EditorThemeController(TextEditor editor, MarkdownColorizingTransformer colorizer)
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
