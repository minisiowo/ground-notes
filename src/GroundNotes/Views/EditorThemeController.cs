using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly TextEditor _editor;
    private readonly MarkdownColorizingTransformer _colorizer;
    private readonly CodeBlockBackgroundRenderer _codeBlockRenderer;
    private readonly MarkdownCodeBlockCopyLayer _codeBlockCopyLayer;
    private readonly MarkdownImagePreviewProvider _imagePreviewProvider;
    private readonly MarkdownImageVisualLineTransformer _imageVisualLineTransformer;
    private readonly MarkdownImagePreviewLayer _imagePreviewLayer;
    private readonly MarkdownVisualLineIndentationProvider _visualLineIndentationProvider;
    private EditorAppearanceSignature _lastAppearanceSignature;
    private Size _lastTextViewBounds;
    private bool _isResizeRefreshQueued;
    private bool _markdownFormattingEnabled = true;
    private bool _isPointerOverInteractiveMarkdownTarget;

    public EditorThemeController(TextEditor editor, MarkdownColorizingTransformer colorizer, Func<string, Task>? copyCodeBlockAsync = null)
    {
        _editor = editor;
        _colorizer = colorizer;
        _codeBlockRenderer = new CodeBlockBackgroundRenderer(colorizer);
        _codeBlockCopyLayer = new MarkdownCodeBlockCopyLayer(_editor.TextArea.TextView, colorizer, copyCodeBlockAsync);
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
        _editor.TextArea.TextView.InsertLayer(_codeBlockCopyLayer, AvaloniaEdit.Rendering.KnownLayer.Text, AvaloniaEdit.Rendering.LayerInsertionPosition.Above);
        _editor.TextArea.TextView.LineTransformers.Add(_imageVisualLineTransformer);
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_codeBlockRenderer);
        _editor.ResourcesChanged += OnEditorResourcesChanged;
        _editor.SizeChanged += OnEditorSizeChanged;
        _editor.TextArea.TextView.PropertyChanged += OnTextViewPropertyChanged;
        _editor.TextArea.TextView.PointerMoved += OnTextViewPointerMoved;
        _editor.TextArea.TextView.PointerExited += OnTextViewPointerExited;

        _lastAppearanceSignature = CaptureAppearanceSignature();
        _lastTextViewBounds = _editor.TextArea.TextView.Bounds.Size;
        UpdatePreviewAvailableWidth(_lastTextViewBounds.Width);
        ApplyEditorOptions(_lastAppearanceSignature);
        ApplySelectionTheme();
    }

    public bool IsMarkdownFormattingEnabled => _markdownFormattingEnabled;

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
        _codeBlockCopyLayer.InvalidateResources();
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

    public MarkdownImagePreviewHitTestResult? TryHitTestImagePreview(Point point)
    {
        return _markdownFormattingEnabled
            ? _imagePreviewLayer.TryHitTestPreview(point)
            : null;
    }

    public MarkdownCodeBlockCopyHitTestResult? TryHitTestCodeBlockCopyButton(Point point)
    {
        return _markdownFormattingEnabled
            ? _codeBlockCopyLayer.TryHitTestButton(point)
            : null;
    }

    public void RefreshAfterDocumentReplace()
    {
        SetPointerOverInteractiveMarkdownTarget(false);
        _codeBlockCopyLayer.ClearState();
        _imagePreviewLayer.ClearRenderedState();
        if (_markdownFormattingEnabled)
        {
            _imagePreviewLayer.RequestRefresh();
        }
    }

    public void RefreshImagePreviews(string? resolvedImagePath = null)
    {
        if (!string.IsNullOrWhiteSpace(resolvedImagePath))
        {
            _imagePreviewProvider.InvalidateImage(resolvedImagePath);
        }

        _imagePreviewLayer.InvalidateRefreshState();
        _editor.TextArea.TextView.Redraw();
        if (_markdownFormattingEnabled)
        {
            _imagePreviewLayer.RequestRefresh();
        }
    }

    public void SetBaseDirectoryPath(string? baseDirectoryPath)
    {
        _imagePreviewProvider.SetBaseDirectoryPath(baseDirectoryPath);
        _imagePreviewLayer.InvalidateRefreshState();
        UpdatePreviewAvailableWidth(_editor.TextArea.TextView.Bounds.Width);
        _editor.TextArea.TextView.Redraw();
        if (_markdownFormattingEnabled)
        {
            _imagePreviewLayer.RequestRefresh();
        }
    }

    public void SetMarkdownFormattingEnabled(bool enabled)
    {
        if (_markdownFormattingEnabled == enabled)
        {
            return;
        }

        _markdownFormattingEnabled = enabled;

        if (enabled)
        {
            AttachMarkdownPresentation();
        }
        else
        {
            DetachMarkdownPresentation();
        }

        SetPointerOverInteractiveMarkdownTarget(false);
        RefreshPresentation();
    }

    private void RefreshTypographyResources(EditorAppearanceSignature currentSignature)
    {
        _lastAppearanceSignature = currentSignature;
        ApplyEditorOptions(currentSignature);
        _colorizer.InvalidateResourceCache();
        _codeBlockRenderer.InvalidateBrush();
        _codeBlockCopyLayer.InvalidateResources();
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
        _editor.TextArea.TextView.PointerMoved -= OnTextViewPointerMoved;
        _editor.TextArea.TextView.PointerExited -= OnTextViewPointerExited;
        _colorizer.RedrawRequested -= OnColorizerRedrawRequested;
        _editor.TextArea.TextView.VisualLineIndentationProvider = null;
        _editor.TextArea.TextView.Layers.Remove(_codeBlockCopyLayer);
        _editor.TextArea.TextView.Layers.Remove(_imagePreviewLayer);
        _editor.TextArea.TextView.LineTransformers.Remove(_imageVisualLineTransformer);
        _editor.TextArea.TextView.LineTransformers.Remove(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Remove(_codeBlockRenderer);
        _imagePreviewLayer.Dispose();
        _codeBlockCopyLayer.Dispose();
        _imagePreviewProvider.Dispose();
    }

    private void AttachMarkdownPresentation()
    {
        var textView = _editor.TextArea.TextView;

        _codeBlockCopyLayer.SetEnabled(true);
        textView.VisualLineIndentationProvider = _visualLineIndentationProvider;
        if (!textView.Layers.Contains(_imagePreviewLayer))
        {
            textView.InsertLayer(_imagePreviewLayer, AvaloniaEdit.Rendering.KnownLayer.Text, AvaloniaEdit.Rendering.LayerInsertionPosition.Above);
        }

        if (!textView.Layers.Contains(_codeBlockCopyLayer))
        {
            textView.InsertLayer(_codeBlockCopyLayer, AvaloniaEdit.Rendering.KnownLayer.Text, AvaloniaEdit.Rendering.LayerInsertionPosition.Above);
        }

        if (!textView.LineTransformers.Contains(_imageVisualLineTransformer))
        {
            textView.LineTransformers.Add(_imageVisualLineTransformer);
        }

        if (!textView.LineTransformers.Contains(_colorizer))
        {
            textView.LineTransformers.Add(_colorizer);
        }

        if (!textView.BackgroundRenderers.Contains(_codeBlockRenderer))
        {
            textView.BackgroundRenderers.Add(_codeBlockRenderer);
        }

        _imagePreviewLayer.InvalidateRefreshState();
        _imagePreviewLayer.RequestRefresh();
        _codeBlockCopyLayer.RequestRefresh();
    }

    private void DetachMarkdownPresentation()
    {
        var textView = _editor.TextArea.TextView;
        textView.VisualLineIndentationProvider = null;
        _codeBlockCopyLayer.SetEnabled(false);
        _codeBlockCopyLayer.ClearState();
        _imagePreviewLayer.ClearRenderedState();
        textView.Layers.Remove(_codeBlockCopyLayer);
        textView.Layers.Remove(_imagePreviewLayer);
        textView.LineTransformers.Remove(_imageVisualLineTransformer);
        textView.LineTransformers.Remove(_colorizer);
        textView.BackgroundRenderers.Remove(_codeBlockRenderer);
        textView.InvalidateVisual();
    }

    private void RefreshPresentation()
    {
        _colorizer.InvalidateResourceCache();
        _codeBlockRenderer.InvalidateBrush();
        _codeBlockCopyLayer.InvalidateResources();
        ApplySelectionTheme();

        var textView = _editor.TextArea.TextView;
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        textView.Redraw();

        if (textView.Bounds.Width > 0 && textView.Bounds.Height > 0)
        {
            textView.EnsureVisualLines();
        }

        _editor.InvalidateMeasure();
        _editor.InvalidateArrange();
        _editor.InvalidateVisual();
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

    private void OnTextViewPointerMoved(object? sender, PointerEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        var point = e.GetPosition(textView);
        SetPointerOverInteractiveMarkdownTarget(HasInteractiveMarkdownTarget(point));
    }

    private void OnTextViewPointerExited(object? sender, PointerEventArgs e)
    {
        SetPointerOverInteractiveMarkdownTarget(false);
    }

    private bool HasInteractiveMarkdownTarget(Point point)
    {
        return _markdownFormattingEnabled
            && (_imagePreviewLayer.TryHitTestPreview(point) is not null
                || _codeBlockCopyLayer.TryHitTestButton(point) is not null);
    }

    private void SetPointerOverInteractiveMarkdownTarget(bool isPointerOverInteractiveMarkdownTarget)
    {
        if (_isPointerOverInteractiveMarkdownTarget == isPointerOverInteractiveMarkdownTarget)
        {
            if (isPointerOverInteractiveMarkdownTarget)
            {
                _editor.TextArea.TextView.Cursor = HandCursor;
            }

            return;
        }

        _isPointerOverInteractiveMarkdownTarget = isPointerOverInteractiveMarkdownTarget;
        _editor.TextArea.TextView.Cursor = isPointerOverInteractiveMarkdownTarget
            ? HandCursor
            : ResolveTextViewDefaultCursor();
    }

    private Cursor? ResolveTextViewDefaultCursor()
    {
        return _editor.TextArea.TextView.Parent is AvaloniaObject parent
            ? parent.GetValue(InputElement.CursorProperty)
            : null;
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
        _imagePreviewLayer.InvalidateRefreshState();
        _codeBlockCopyLayer.RequestRefresh();
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
