using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using GroundNotes.Styles;

namespace GroundNotes.Editors;

internal sealed class MarkdownCodeBlockCopyLayer : Control, IDisposable
{
    private const string ButtonText = "copy";
    private const double ButtonWidth = 48;
    private const double ButtonHeight = 22;
    private const double ButtonMargin = 8;
    private const double CornerRadius = 2;
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly TextView _textView;
    private readonly MarkdownColorizingTransformer _colorizer;
    private readonly Func<string, Task>? _copyAsync;
    private bool _isDisposed;
    private bool _isEnabled = true;
    private bool _isRefreshQueued;
    private TextDocument? _document;
    private MarkdownCodeBlockCopyInfo? _activeBlock;
    private Rect _buttonBounds;
    private bool _isPointerOverButton;
    private bool _isPressed;
    private IBrush? _backgroundBrush;
    private IBrush? _hoverBrush;
    private IBrush? _pressedBrush;
    private IBrush? _borderBrush;
    private IBrush? _focusBorderBrush;
    private IBrush? _textBrush;

    public MarkdownCodeBlockCopyLayer(TextView textView, MarkdownColorizingTransformer colorizer, Func<string, Task>? copyAsync)
    {
        _textView = textView;
        _colorizer = colorizer;
        _copyAsync = copyAsync;
        IsHitTestVisible = false;

        _textView.PointerMoved += OnTextViewPointerMoved;
        _textView.PointerExited += OnTextViewPointerExited;
        _textView.VisualLinesChanged += OnVisualLinesChanged;
        _textView.ScrollOffsetChanged += OnVisualLinesChanged;
        _textView.DocumentChanged += OnTextViewDocumentChanged;
        _textView.AddHandler(InputElement.PointerPressedEvent, OnTextViewPointerPressed, RoutingStrategies.Tunnel);
        _textView.AddHandler(InputElement.PointerReleasedEvent, OnTextViewPointerReleased, RoutingStrategies.Tunnel);
        AttachDocument(_textView.Document);
    }

    public void SetEnabled(bool enabled)
    {
        if (_isEnabled == enabled)
        {
            return;
        }

        _isEnabled = enabled;
        if (!enabled)
        {
            ClearState();
        }
    }

    public void InvalidateResources()
    {
        _backgroundBrush = null;
        _hoverBrush = null;
        _pressedBrush = null;
        _borderBrush = null;
        _focusBorderBrush = null;
        _textBrush = null;
        InvalidateVisual();
    }

    public void ClearState()
    {
        _activeBlock = null;
        _buttonBounds = default;
        SetPointerOverButton(false);
        _isPressed = false;
        InvalidateVisual();
    }

    public void RequestRefresh()
    {
        if (_isDisposed || _isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            _isRefreshQueued = false;
            Refresh();
        }, DispatcherPriority.Render);
    }

    public void Refresh()
    {
        if (!_isEnabled || _activeBlock is null)
        {
            return;
        }

        if (!MarkdownCodeBlockCopyHelper.TryResolve(_textView.Document, _activeBlock.Value.StartLineNumber, out var refreshedBlock))
        {
            ClearState();
            return;
        }

        _activeBlock = refreshedBlock;
        if (!TryUpdateButtonBounds(refreshedBlock))
        {
            ClearState();
            return;
        }

        InvalidateVisual();
    }

    public MarkdownCodeBlockCopyHitTestResult? TryHitTestButton(Point point)
    {
        if (!_isEnabled || _activeBlock is not { } block || !_buttonBounds.Contains(point))
        {
            return null;
        }

        return new MarkdownCodeBlockCopyHitTestResult(block.Text, _buttonBounds, block.StartLineNumber, block.EndLineNumber);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_activeBlock is null || _buttonBounds == default)
        {
            return;
        }

        ResolveBrushes();

        var background = _isPressed
            ? _pressedBrush
            : _isPointerOverButton
                ? _hoverBrush
                : _backgroundBrush;
        var border = _isPointerOverButton ? _focusBorderBrush : _borderBrush;
        var radius = new CornerRadius(CornerRadius);

        context.DrawRectangle(background, new Pen(border, 1), _buttonBounds, radius.TopLeft, radius.TopLeft);

        var layout = CreateButtonTextLayout();
        var origin = new Point(
            _buttonBounds.X + Math.Max(0, (_buttonBounds.Width - layout.Width) / 2),
            _buttonBounds.Y + Math.Max(0, (_buttonBounds.Height - layout.Height) / 2));
        layout.Draw(context, origin);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _isRefreshQueued = false;
        _textView.PointerMoved -= OnTextViewPointerMoved;
        _textView.PointerExited -= OnTextViewPointerExited;
        _textView.VisualLinesChanged -= OnVisualLinesChanged;
        _textView.ScrollOffsetChanged -= OnVisualLinesChanged;
        _textView.DocumentChanged -= OnTextViewDocumentChanged;
        _textView.RemoveHandler(InputElement.PointerPressedEvent, OnTextViewPointerPressed);
        _textView.RemoveHandler(InputElement.PointerReleasedEvent, OnTextViewPointerReleased);
        DetachDocument();
        ClearState();
    }

    private void OnTextViewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isEnabled || _copyAsync is null)
        {
            ClearState();
            return;
        }

        var point = e.GetPosition(_textView);
        SetPointerOverButton(_buttonBounds.Contains(point));
        if (_isPointerOverButton && _activeBlock is not null)
        {
            InvalidateVisual();
            return;
        }

        UpdateHover(point);
    }

    private void OnTextViewPointerExited(object? sender, PointerEventArgs e) => ClearState();

    private void OnVisualLinesChanged(object? sender, EventArgs e) => RequestRefresh();

    private async void OnTextViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isEnabled || _copyAsync is null || !e.GetCurrentPoint(_textView).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var hit = TryHitTestButton(e.GetPosition(_textView));
        if (hit is null)
        {
            return;
        }

        e.Handled = true;
        _isPressed = true;
        InvalidateVisual();

        try
        {
            await _copyAsync(hit.Value.Text);
        }
        catch (Exception)
        {
        }
        finally
        {
            _isPressed = false;
            InvalidateVisual();
        }
    }

    private void OnTextViewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
        {
            return;
        }

        _isPressed = false;
        e.Handled = true;
        InvalidateVisual();
    }

    private void UpdateHover(Point point)
    {
        if (!_isEnabled || !_textView.VisualLinesValid || _textView.Document is null)
        {
            ClearState();
            return;
        }

        var documentPoint = point + _textView.ScrollOffset;
        var position = _textView.GetPosition(documentPoint);
        if (position is null)
        {
            ClearState();
            return;
        }

        var lineNumber = position.Value.Line;
        if (!_colorizer.QueryIsFencedCodeLine(_textView.Document, lineNumber)
            || !MarkdownCodeBlockCopyHelper.TryResolve(_textView.Document, lineNumber, out var block)
            || !TryUpdateButtonBounds(block))
        {
            ClearState();
            return;
        }

        _activeBlock = block;
        SetPointerOverButton(_buttonBounds.Contains(point));
        InvalidateVisual();
    }

    private void SetPointerOverButton(bool isPointerOverButton)
    {
        if (_isPointerOverButton == isPointerOverButton)
        {
            if (isPointerOverButton)
            {
                _textView.Cursor = HandCursor;
            }

            return;
        }

        _isPointerOverButton = isPointerOverButton;
        _textView.Cursor = isPointerOverButton
            ? HandCursor
            : ResolveTextViewDefaultCursor();
    }

    private Cursor? ResolveTextViewDefaultCursor()
    {
        return _textView.Parent is AvaloniaObject parent
            ? parent.GetValue(InputElement.CursorProperty)
            : null;
    }

    private bool TryUpdateButtonBounds(MarkdownCodeBlockCopyInfo block)
    {
        if (!_isEnabled || !_textView.VisualLinesValid || _textView.Document is null || _textView.VisualLines.Count == 0)
        {
            return false;
        }

        double? top = null;
        double bottom = 0;
        foreach (var visualLine in _textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            if (lineNumber < block.StartLineNumber || lineNumber > block.EndLineNumber)
            {
                continue;
            }

            if (!_colorizer.QueryIsFencedCodeLine(_textView.Document, lineNumber))
            {
                continue;
            }

            var lineTop = visualLine.VisualTop - _textView.VerticalOffset;
            var lineBottom = lineTop + visualLine.Height;
            top = top is null ? lineTop : Math.Min(top.Value, lineTop);
            bottom = Math.Max(bottom, lineBottom);
        }

        if (top is null)
        {
            return false;
        }

        var x = Math.Max(ButtonMargin, Bounds.Width - ButtonWidth - ButtonMargin);
        var y = Math.Max(ButtonMargin / 2, top.Value + ButtonMargin / 2);
        if (Bounds.Height > ButtonHeight + ButtonMargin)
        {
            y = Math.Min(y, Bounds.Height - ButtonHeight - ButtonMargin / 2);
        }

        if (bottom > 0)
        {
            y = Math.Min(y, Math.Max(ButtonMargin / 2, bottom - ButtonHeight - ButtonMargin / 2));
        }

        _buttonBounds = new Rect(x, y, ButtonWidth, ButtonHeight);
        return true;
    }

    private TextLayout CreateButtonTextLayout()
    {
        ResolveBrushes();
        return new TextLayout(
            ButtonText,
            ResolveTypeface(),
            ResolveFontSize(),
            _textBrush,
            TextAlignment.Center,
            TextWrapping.NoWrap,
            TextTrimming.None);
    }

    private void ResolveBrushes()
    {
        var resources = Application.Current?.Resources;
        _backgroundBrush ??= resources?[ThemeKeys.PaneBackgroundBrush] as IBrush ?? Brushes.Transparent;
        _hoverBrush ??= resources?[ThemeKeys.SurfaceHoverBrush] as IBrush ?? _backgroundBrush;
        _pressedBrush ??= resources?[ThemeKeys.SurfacePressedBrush] as IBrush ?? _hoverBrush;
        _borderBrush ??= resources?[ThemeKeys.BorderBrushBase] as IBrush ?? Brushes.Transparent;
        _focusBorderBrush ??= resources?[ThemeKeys.FocusBorderBrush] as IBrush ?? _borderBrush;
        _textBrush ??= resources?[ThemeKeys.SecondaryTextBrush] as IBrush ?? Brushes.Gray;
    }

    private void OnTextViewDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        DetachDocument();
        AttachDocument(e.NewDocument);
        ClearState();
    }

    private void AttachDocument(TextDocument? document)
    {
        if (ReferenceEquals(_document, document))
        {
            return;
        }

        DetachDocument();
        _document = document;
        if (_document is not null)
        {
            _document.Changed += OnDocumentChanged;
        }
    }

    private void DetachDocument()
    {
        if (_document is null)
        {
            return;
        }

        _document.Changed -= OnDocumentChanged;
        _document = null;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e) => ClearState();

    private static Typeface ResolveTypeface()
    {
        var resources = Application.Current?.Resources;
        var fontFamily = resources?[ThemeKeys.TerminalFont] as FontFamily ?? FontFamily.Default;
        var fontWeight = resources?[ThemeKeys.TerminalFontWeight] as FontWeight? ?? FontWeight.Normal;
        var fontStyle = resources?[ThemeKeys.TerminalFontStyle] as FontStyle? ?? FontStyle.Normal;
        return new Typeface(fontFamily, fontStyle, fontWeight, FontStretch.Normal);
    }

    private static double ResolveFontSize()
    {
        var value = Application.Current?.Resources[ThemeKeys.AppFontSizeSmall];
        return value is double fontSize ? fontSize : 11;
    }
}

internal readonly record struct MarkdownCodeBlockCopyHitTestResult(
    string Text,
    Rect Bounds,
    int StartLineNumber,
    int EndLineNumber);
