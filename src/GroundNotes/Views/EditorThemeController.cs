using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using GroundNotes.Editors;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;

namespace GroundNotes.Views;

internal sealed class EditorThemeController : IDisposable
{
    private const double ResizeTolerance = 0.1;

    private readonly TextEditor _editor;
    private readonly MarkdownColorizingTransformer _colorizer;
    private readonly CodeBlockBackgroundRenderer _codeBlockRenderer;
    private readonly MarkdownImagePreviewProvider _imagePreviewProvider;
    private readonly MarkdownImageVisualLineTransformer _imageVisualLineTransformer;
    private readonly MarkdownImagePreviewLayer _imagePreviewLayer;
    private readonly MarkdownVisualLineIndentationProvider _visualLineIndentationProvider;
    private EditorAppearanceSignature _lastAppearanceSignature;
    private Size _lastTextViewBounds;
    private bool _isResizeRefreshQueued;

    public EditorThemeController(TextEditor editor, MarkdownColorizingTransformer colorizer)
    {
        _editor = editor;
        _colorizer = colorizer;
        _codeBlockRenderer = new CodeBlockBackgroundRenderer(colorizer);
        _imagePreviewProvider = new MarkdownImagePreviewProvider(colorizer, new NoteAssetService());
        _imageVisualLineTransformer = new MarkdownImageVisualLineTransformer(_imagePreviewProvider);
        _imagePreviewLayer = new MarkdownImagePreviewLayer(_editor.TextArea.TextView, _imagePreviewProvider);
        _visualLineIndentationProvider = new MarkdownVisualLineIndentationProvider(colorizer);

        _colorizer.RedrawRequested += OnColorizerRedrawRequested;

        ConfigureEditorOptions(_editor.Options);
        _editor.Options.WordWrapIndentation = 0;
        _editor.Options.InheritWordWrapIndentation = true;
        _editor.TextArea.TextView.VisualLineIndentationProvider = _visualLineIndentationProvider;
        _editor.TextArea.TextView.InsertLayer(_imagePreviewLayer, AvaloniaEdit.Rendering.KnownLayer.Text, AvaloniaEdit.Rendering.LayerInsertionPosition.Above);
        _editor.TextArea.TextView.LineTransformers.Add(_imageVisualLineTransformer);
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_codeBlockRenderer);
        _editor.ResourcesChanged += OnEditorResourcesChanged;
        _editor.SizeChanged += OnEditorSizeChanged;
        _editor.TextArea.TextView.PropertyChanged += OnTextViewPropertyChanged;

        _lastAppearanceSignature = CaptureAppearanceSignature();
        _lastTextViewBounds = _editor.TextArea.TextView.Bounds.Size;
        UpdatePreviewAvailableWidth(_lastTextViewBounds.Width);
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
        _codeBlockRenderer.InvalidateBrush();
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

    public void SetBaseDirectoryPath(string? baseDirectoryPath)
    {
        _imagePreviewProvider.SetBaseDirectoryPath(baseDirectoryPath);
        UpdatePreviewAvailableWidth(_editor.TextArea.TextView.Bounds.Width);
        _editor.TextArea.TextView.Redraw();
        _imagePreviewLayer.Refresh();
    }

    private void RefreshTypographyResources(EditorAppearanceSignature currentSignature)
    {
        _lastAppearanceSignature = currentSignature;
        ApplyEditorOptions(currentSignature);
        _colorizer.InvalidateResourceCache();
        _codeBlockRenderer.InvalidateBrush();
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
        _editor.SizeChanged -= OnEditorSizeChanged;
        _editor.TextArea.TextView.PropertyChanged -= OnTextViewPropertyChanged;
        _colorizer.RedrawRequested -= OnColorizerRedrawRequested;
        _editor.TextArea.TextView.VisualLineIndentationProvider = null;
        _editor.TextArea.TextView.Layers.Remove(_imagePreviewLayer);
        _editor.TextArea.TextView.LineTransformers.Remove(_imageVisualLineTransformer);
        _editor.TextArea.TextView.LineTransformers.Remove(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Remove(_codeBlockRenderer);
        _imagePreviewLayer.Dispose();
        _imagePreviewProvider.Dispose();
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

    private void OnEditorSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < ResizeTolerance)
        {
            return;
        }

        RefreshAfterResize();
    }

    private void OnTextViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.BoundsProperty || sender is not Control control)
        {
            return;
        }

        var newBounds = control.Bounds.Size;
        if (Math.Abs(newBounds.Width - _lastTextViewBounds.Width) < ResizeTolerance
            && Math.Abs(newBounds.Height - _lastTextViewBounds.Height) < ResizeTolerance)
        {
            return;
        }

        _lastTextViewBounds = newBounds;
        UpdatePreviewAvailableWidth(newBounds.Width);
        RefreshAfterResize();
    }

    private static EditorAppearanceSignature CaptureAppearanceSignature()
    {
        var resources = Application.Current?.Resources;
        var fontFamily = resources?[ThemeKeys.CodeFont] as FontFamily;
        var fontWeight = resources?[ThemeKeys.CodeFontWeight] is FontWeight weight ? weight : FontWeight.Normal;
        var fontStyle = resources?[ThemeKeys.CodeFontStyle] is FontStyle style ? style : FontStyle.Normal;
        return new EditorAppearanceSignature(fontFamily?.ToString() ?? string.Empty, fontWeight, fontStyle);
    }

    internal static void ConfigureEditorOptions(TextEditorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.ConvertTabsToSpaces = false;
        options.EnableRectangularSelection = false;
        options.EnableHyperlinks = false;
        options.EnableEmailHyperlinks = false;
        options.RequireControlModifierForHyperlinkClick = false;
    }

    private static void ApplyEditorOptions(EditorAppearanceSignature signature)
    {
    }

    private void RefreshAfterResize()
    {
        var textView = _editor.TextArea.TextView;
        UpdatePreviewAvailableWidth(textView.Bounds.Width);
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        textView.Redraw();

        if (textView.Bounds.Width > 0 && textView.Bounds.Height > 0)
        {
            textView.EnsureVisualLines();
        }

        if (_isResizeRefreshQueued)
        {
            return;
        }

        _isResizeRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isResizeRefreshQueued = false;
            var currentTextView = _editor.TextArea.TextView;
            currentTextView.InvalidateMeasure();
            currentTextView.InvalidateArrange();
            currentTextView.InvalidateVisual();
            currentTextView.Redraw();

            if (currentTextView.Bounds.Width > 0 && currentTextView.Bounds.Height > 0)
            {
                currentTextView.EnsureVisualLines();
            }
        }, DispatcherPriority.Render);
    }

    private void UpdatePreviewAvailableWidth(double width)
    {
        _imagePreviewProvider.SetAvailableWidth(width);
    }

    private readonly record struct EditorAppearanceSignature(
        string FontFamily,
        FontWeight Weight,
        FontStyle Style);
}
