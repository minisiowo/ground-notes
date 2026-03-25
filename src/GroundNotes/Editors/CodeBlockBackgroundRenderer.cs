using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using GroundNotes.Styles;

namespace GroundNotes.Editors;

internal sealed class CodeBlockBackgroundRenderer : IBackgroundRenderer
{
    private const double VerticalPadding = 2.0;

    private readonly MarkdownColorizingTransformer _colorizer;
    private IBrush? _brush;

    public CodeBlockBackgroundRenderer(MarkdownColorizingTransformer colorizer)
    {
        _colorizer = colorizer;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void InvalidateBrush()
    {
        _brush = null;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        if (textView.Document is null)
        {
            return;
        }

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0)
        {
            return;
        }

        _brush ??= Application.Current?.Resources[ThemeKeys.MarkdownCodeBlockBackgroundBrush] as IBrush;
        if (_brush is null)
        {
            return;
        }

        var viewWidth = textView.Bounds.Width;
        double? blockTop = null;
        double blockBottom = 0;

        foreach (var visualLine in visualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            var isFenced = _colorizer.QueryIsFencedCodeLine(textView.Document, lineNumber);

            if (isFenced)
            {
                var lineTop = visualLine.VisualTop - textView.VerticalOffset;
                var lineBottom = lineTop + visualLine.Height;

                if (blockTop is null)
                {
                    blockTop = lineTop;
                }

                blockBottom = lineBottom;
            }
            else if (blockTop is not null)
            {
                FillBlockBackground(drawingContext, _brush, blockTop.Value, blockBottom, viewWidth);
                blockTop = null;
            }
        }

        if (blockTop is not null)
        {
            FillBlockBackground(drawingContext, _brush, blockTop.Value, blockBottom, viewWidth);
        }
    }

    private static void FillBlockBackground(DrawingContext drawingContext, IBrush brush, double top, double bottom, double viewWidth)
    {
        var rect = new Rect(
            0,
            top - VerticalPadding,
            viewWidth,
            bottom - top + 2 * VerticalPadding);

        drawingContext.FillRectangle(brush, rect);
    }
}
