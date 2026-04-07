using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class MarkdownImagePreviewLayer : Control, IDisposable
{
    private const double VerticalSpacing = 6;

    private readonly TextView _textView;
    private readonly MarkdownImagePreviewProvider _previewProvider;
    private readonly Dictionary<int, RenderedPreview> _renderedPreviews = [];
    private readonly Dictionary<int, RenderedLineState> _renderedLineStates = [];
    private bool _isDisposed;
    private bool _isRefreshQueued;
    private int _refreshVersion;
    private int _scheduledRefreshVersion;
    private RefreshSnapshot? _lastRefreshSnapshot;

    public MarkdownImagePreviewLayer(TextView textView, MarkdownImagePreviewProvider previewProvider, bool subscribeToTextViewEvents = true)
    {
        _textView = textView;
        _previewProvider = previewProvider;
        IsHitTestVisible = false;

        if (subscribeToTextViewEvents)
        {
            _textView.VisualLinesChanged += OnVisualLinesChanged;
            _textView.ScrollOffsetChanged += OnVisualLinesChanged;
        }
    }

    public void Refresh()
    {
        _isRefreshQueued = false;
        _refreshVersion++;
        MarkdownDiagnostics.RecordPreviewLayerRefresh();

        if (!_textView.VisualLinesValid)
        {
            ClearRenderedState();
            return;
        }

        var refreshSnapshot = CreateRefreshSnapshot();
        if (_lastRefreshSnapshot is not null && _lastRefreshSnapshot.Equals(refreshSnapshot))
        {
            MarkdownDiagnostics.RecordPreviewLayerRefreshSkip();
            return;
        }

        HashSet<int> visibleLineNumbers = [];
        foreach (var visualLine in _textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            visibleLineNumbers.Add(lineNumber);

            var lineState = CreateVisibleLineSnapshot(visualLine);
            if (_renderedLineStates.TryGetValue(lineNumber, out var renderedLineState)
                && renderedLineState.LineSnapshot.Equals(lineState))
            {
                MarkdownDiagnostics.RecordPreviewLayerLineStateReuse();
                if (renderedLineState.HasPreview
                    && _renderedPreviews.TryGetValue(lineNumber, out var renderedPreview))
                {
                    _renderedPreviews[lineNumber] = renderedPreview with
                    {
                        Bounds = GetPreviewRect(visualLine, renderedPreview.Width, renderedPreview.Height)
                    };
                }

                continue;
            }

            var preview = _previewProvider.GetPreview(_textView.Document, visualLine.FirstDocumentLine);
            if (preview is null || visualLine.TextLines.Count == 0)
            {
                _renderedPreviews.Remove(lineNumber);
                _renderedLineStates[lineNumber] = new RenderedLineState(lineState, false);
                continue;
            }

            _renderedPreviews[lineNumber] = new RenderedPreview(
                preview.Value.Bitmap,
                preview.Value.Width,
                preview.Value.Height,
                GetPreviewRect(visualLine, preview.Value.Width, preview.Value.Height));
            _renderedLineStates[lineNumber] = new RenderedLineState(lineState, true);
        }

        var linesToRemove = _renderedLineStates.Keys.Where(key => !visibleLineNumbers.Contains(key)).ToArray();
        foreach (var lineNumber in linesToRemove)
        {
            _renderedLineStates.Remove(lineNumber);
            _renderedPreviews.Remove(lineNumber);
        }

        _lastRefreshSnapshot = refreshSnapshot;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        MarkdownDiagnostics.RecordPreviewLayerDraw();

        if (_renderedPreviews.Count == 0)
        {
            return;
        }

        using var _ = context.PushClip(new Rect(Bounds.Size));
        foreach (var renderedPreview in _renderedPreviews.Values)
        {
            context.DrawImage(renderedPreview.Bitmap, new Rect(renderedPreview.Bitmap.Size), renderedPreview.Bounds);
            MarkdownDiagnostics.RecordPreviewLayerDrawnPreview();
        }
    }

    public void InvalidateRefreshState(bool clearRenderedLineStates = true)
    {
        _lastRefreshSnapshot = null;
        if (clearRenderedLineStates)
        {
            _renderedLineStates.Clear();
        }
    }

    public void RequestRefresh()
    {
        MarkdownDiagnostics.RecordPreviewLayerRefreshRequest();

        if (_isDisposed || _isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        var scheduledVersion = _refreshVersion + 1;
        _scheduledRefreshVersion = scheduledVersion;
        MarkdownDiagnostics.RecordPreviewLayerRefreshPost();
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            _isRefreshQueued = false;
            if (_refreshVersion >= scheduledVersion || _scheduledRefreshVersion != scheduledVersion)
            {
                return;
            }

            Refresh();
        }, DispatcherPriority.Render);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _isRefreshQueued = false;
        _lastRefreshSnapshot = null;
        _renderedLineStates.Clear();
        _textView.VisualLinesChanged -= OnVisualLinesChanged;
        _textView.ScrollOffsetChanged -= OnVisualLinesChanged;
        ClearRenderedState();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => RequestRefresh();

    private void ClearRenderedState()
    {
        _lastRefreshSnapshot = null;
        _renderedLineStates.Clear();
        _renderedPreviews.Clear();
        InvalidateVisual();
    }

    private VisibleLineSnapshot CreateVisibleLineSnapshot(VisualLine visualLine)
        => new(
            visualLine.FirstDocumentLine.LineNumber,
            visualLine.FirstDocumentLine.Length,
            visualLine.VisualTop,
            visualLine.Height,
            visualLine.TextLines.Count);

    private Rect GetPreviewRect(VisualLine visualLine, double width, double height)
    {
        var lineStart = visualLine.GetVisualPosition(0, VisualYPosition.LineTop);
        var lastTextLine = visualLine.TextLines[visualLine.TextLines.Count - 1];
        var textBottom = visualLine.GetTextLineVisualYPosition(lastTextLine, VisualYPosition.LineBottom);
        return new Rect(
            lineStart.X - _textView.ScrollOffset.X,
            textBottom - _textView.ScrollOffset.Y + VerticalSpacing,
            width,
            height);
    }

    private RefreshSnapshot CreateRefreshSnapshot()
    {
        List<VisibleLineSnapshot> visibleLines = [];
        foreach (var visualLine in _textView.VisualLines)
        {
            visibleLines.Add(new VisibleLineSnapshot(
                visualLine.FirstDocumentLine.LineNumber,
                visualLine.FirstDocumentLine.Length,
                visualLine.VisualTop,
                visualLine.Height,
                visualLine.TextLines.Count));
        }

        return new RefreshSnapshot(_textView.ScrollOffset.X, _textView.ScrollOffset.Y, visibleLines);
    }

    private sealed class RefreshSnapshot : IEquatable<RefreshSnapshot>
    {
        private const double Tolerance = 0.01;
        private readonly double _scrollOffsetX;
        private readonly double _scrollOffsetY;
        private readonly List<VisibleLineSnapshot> _visibleLines;

        public RefreshSnapshot(double scrollOffsetX, double scrollOffsetY, List<VisibleLineSnapshot> visibleLines)
        {
            _scrollOffsetX = scrollOffsetX;
            _scrollOffsetY = scrollOffsetY;
            _visibleLines = visibleLines;
        }

        public bool Equals(RefreshSnapshot? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Math.Abs(_scrollOffsetX - other._scrollOffsetX) > Tolerance
                || Math.Abs(_scrollOffsetY - other._scrollOffsetY) > Tolerance
                || _visibleLines.Count != other._visibleLines.Count)
            {
                return false;
            }

            for (var i = 0; i < _visibleLines.Count; i++)
            {
                if (!_visibleLines[i].Equals(other._visibleLines[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is RefreshSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Math.Round(_scrollOffsetX, 2));
            hash.Add(Math.Round(_scrollOffsetY, 2));
            foreach (var visibleLine in _visibleLines)
            {
                hash.Add(visibleLine);
            }

            return hash.ToHashCode();
        }
    }

    private readonly record struct RenderedPreview(Avalonia.Media.Imaging.Bitmap Bitmap, double Width, double Height, Rect Bounds);

    private readonly record struct VisibleLineSnapshot(int LineNumber, int DocumentLineLength, double VisualTop, double Height, int TextLineCount)
    {
        private const double Tolerance = 0.01;

        public bool Equals(VisibleLineSnapshot other)
        {
            return LineNumber == other.LineNumber
                && DocumentLineLength == other.DocumentLineLength
                && Math.Abs(VisualTop - other.VisualTop) <= Tolerance
                && Math.Abs(Height - other.Height) <= Tolerance
                && TextLineCount == other.TextLineCount;
        }

        public override int GetHashCode()
            => HashCode.Combine(LineNumber, DocumentLineLength, Math.Round(VisualTop, 2), Math.Round(Height, 2), TextLineCount);
    }

    private readonly record struct RenderedLineState(VisibleLineSnapshot LineSnapshot, bool HasPreview);
}
