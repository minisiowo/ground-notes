using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class MarkdownImagePreviewLayer : Canvas, IDisposable
{
    private const double VerticalSpacing = 6;

    private readonly TextView _textView;
    private readonly MarkdownImagePreviewProvider _previewProvider;

    public MarkdownImagePreviewLayer(TextView textView, MarkdownImagePreviewProvider previewProvider)
    {
        _textView = textView;
        _previewProvider = previewProvider;
        IsHitTestVisible = false;

        _textView.VisualLinesChanged += OnVisualLinesChanged;
        _textView.ScrollOffsetChanged += OnVisualLinesChanged;
    }

    public void Refresh()
    {
        Children.Clear();
        if (!_textView.VisualLinesValid)
        {
            return;
        }

        foreach (var visualLine in _textView.VisualLines)
        {
            var preview = _previewProvider.GetPreview(_textView.Document, visualLine.FirstDocumentLine);
            if (preview is null || visualLine.TextLines.Count == 0)
            {
                continue;
            }

            var lineStart = visualLine.GetVisualPosition(0, VisualYPosition.LineTop);
            var lastTextLine = visualLine.TextLines[visualLine.TextLines.Count - 1];
            var textBottom = visualLine.GetTextLineVisualYPosition(lastTextLine, VisualYPosition.LineBottom);

            var image = new Image
            {
                Source = preview.Value.Bitmap,
                Stretch = Stretch.Uniform,
                Width = preview.Value.Width,
                Height = preview.Value.Height,
                MaxWidth = preview.Value.Width,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false
            };

            SetLeft(image, lineStart.X - _textView.ScrollOffset.X);
            SetTop(image, textBottom - _textView.ScrollOffset.Y + VerticalSpacing);
            Children.Add(image);
        }
    }

    public void Dispose()
    {
        _textView.VisualLinesChanged -= OnVisualLinesChanged;
        _textView.ScrollOffsetChanged -= OnVisualLinesChanged;
        Children.Clear();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => Refresh();
}
