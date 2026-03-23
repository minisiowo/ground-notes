using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using GroundNotes.Editors;
using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Views;

internal sealed class EditorThemeController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly MarkdownColorizingTransformer _colorizer;
    private EditorAppearanceSignature _lastAppearanceSignature;

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
        _editor.ResourcesChanged += OnEditorResourcesChanged;

        _lastAppearanceSignature = CaptureAppearanceSignature();
        ApplyEditorOptions(_lastAppearanceSignature);
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

    public void RefreshVisualResources()
    {
        _lastAppearanceSignature = CaptureAppearanceSignature();
        _colorizer.InvalidateResourceCache();
        ApplySelectionTheme();
        _editor.TextArea.TextView.InvalidateVisual();
    }

    public void RefreshTypographyResources()
    {
        var currentSignature = CaptureAppearanceSignature();
        if (currentSignature.Equals(_lastAppearanceSignature))
        {
            return;
        }

        RefreshTypographyResources(currentSignature);
    }

    public void ForceRefreshTypographyResources()
    {
        RefreshTypographyResources(CaptureAppearanceSignature());
    }

    private void RefreshTypographyResources(EditorAppearanceSignature currentSignature)
    {
        _lastAppearanceSignature = currentSignature;
        ApplyEditorOptions(currentSignature);
        _colorizer.InvalidateResourceCache();
        ApplySelectionTheme();

        var textView = _editor.TextArea.TextView;
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        _editor.InvalidateMeasure();
        _editor.InvalidateArrange();
        _editor.InvalidateVisual();

        Dispatcher.UIThread.Post(() =>
        {
            textView.InvalidateMeasure();
            textView.InvalidateArrange();
            textView.InvalidateVisual();
            textView.Redraw();
            textView.EnsureVisualLines();
        }, DispatcherPriority.Render);
    }

    public void Dispose()
    {
        _editor.ResourcesChanged -= OnEditorResourcesChanged;
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

    private void OnEditorResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        var currentSignature = CaptureAppearanceSignature();
        if (!currentSignature.Equals(_lastAppearanceSignature))
        {
            RefreshTypographyResources(currentSignature);
            return;
        }

        RefreshVisualResources();
    }

    private static EditorAppearanceSignature CaptureAppearanceSignature()
    {
        var resources = Application.Current?.Resources;
        var fontFamily = resources?[ThemeKeys.CodeFont] as FontFamily;
        var fontWeight = resources?[ThemeKeys.CodeFontWeight] is FontWeight weight ? weight : FontWeight.Normal;
        var fontStyle = resources?[ThemeKeys.CodeFontStyle] is FontStyle style ? style : FontStyle.Normal;
        return new EditorAppearanceSignature(fontFamily?.ToString() ?? string.Empty, fontWeight, fontStyle);
    }

    private static void ApplyEditorOptions(EditorAppearanceSignature signature)
    {
    }

    private readonly record struct EditorAppearanceSignature(
        string FontFamily,
        FontWeight Weight,
        FontStyle Style);
}
