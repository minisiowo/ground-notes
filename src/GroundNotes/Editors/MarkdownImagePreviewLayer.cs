using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class MarkdownImagePreviewLayer : Canvas, IDisposable
{
    private const double VerticalSpacing = 6;

    private readonly TextView _textView;
    private readonly MarkdownImagePreviewProvider _previewProvider;
    private readonly Dictionary<int, Image> _imagesByLineNumber = [];
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
            RemoveAllImages();
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
                if (renderedLineState.HasPreview && _imagesByLineNumber.TryGetValue(lineNumber, out var existingImage))
                {
                    UpdateImagePosition(existingImage, visualLine);
                }

                continue;
            }

            var preview = _previewProvider.GetPreview(_textView.Document, visualLine.FirstDocumentLine);
            if (preview is null || visualLine.TextLines.Count == 0)
            {
                RemoveImage(lineNumber);
                _renderedLineStates[lineNumber] = new RenderedLineState(lineState, false);
                continue;
            }

            if (!_imagesByLineNumber.TryGetValue(lineNumber, out var image))
            {
                image = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsHitTestVisible = false
                };
                _imagesByLineNumber[lineNumber] = image;
                Children.Add(image);
                MarkdownDiagnostics.RecordPreviewLayerImageCreate();
            }
            else
            {
                MarkdownDiagnostics.RecordPreviewLayerImageReuse();
            }

            image.Source = preview.Value.Bitmap;
            image.Width = preview.Value.Width;
            image.Height = preview.Value.Height;
            image.MaxWidth = preview.Value.Width;
            UpdateImagePosition(image, visualLine);
            _renderedLineStates[lineNumber] = new RenderedLineState(lineState, true);
        }

        var linesToRemove = _renderedLineStates.Keys.Where(key => !visibleLineNumbers.Contains(key)).ToArray();
        foreach (var lineNumber in linesToRemove)
        {
            _renderedLineStates.Remove(lineNumber);
            RemoveImage(lineNumber);
        }

        _lastRefreshSnapshot = refreshSnapshot;
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
        RemoveAllImages();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => RequestRefresh();

    private void RemoveAllImages()
    {
        _lastRefreshSnapshot = null;
        _renderedLineStates.Clear();
        var lineNumbers = _imagesByLineNumber.Keys.ToArray();
        foreach (var lineNumber in lineNumbers)
        {
            RemoveImage(lineNumber);
        }
    }

    private VisibleLineSnapshot CreateVisibleLineSnapshot(VisualLine visualLine)
        => new(
            visualLine.FirstDocumentLine.LineNumber,
            visualLine.FirstDocumentLine.Length,
            visualLine.VisualTop,
            visualLine.Height,
            visualLine.TextLines.Count);

    private void UpdateImagePosition(Image image, VisualLine visualLine)
    {
        var lineStart = visualLine.GetVisualPosition(0, VisualYPosition.LineTop);
        var lastTextLine = visualLine.TextLines[visualLine.TextLines.Count - 1];
        var textBottom = visualLine.GetTextLineVisualYPosition(lastTextLine, VisualYPosition.LineBottom);
        SetLeft(image, lineStart.X - _textView.ScrollOffset.X);
        SetTop(image, textBottom - _textView.ScrollOffset.Y + VerticalSpacing);
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

    private void RemoveImage(int lineNumber)
    {
        if (!_imagesByLineNumber.Remove(lineNumber, out var image))
        {
            return;
        }

        Children.Remove(image);
        MarkdownDiagnostics.RecordPreviewLayerImageRemoval();
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
