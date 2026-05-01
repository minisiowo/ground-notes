using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

using GroundNotes.Styles;

namespace GroundNotes.Views;

internal sealed class ImageAnnotationLayer : Control
{
    private readonly Func<Size> _getImageSize;
    private readonly Func<Rect> _getImageBounds;
    private readonly Func<IReadOnlyList<ImageAnnotation>> _getAnnotations;
    private readonly Func<ImageAnnotationStroke?> _getActiveStroke;

    public ImageAnnotationLayer(
        Func<Size> getImageSize,
        Func<Rect> getImageBounds,
        Func<IReadOnlyList<ImageAnnotation>> getAnnotations,
        Func<ImageAnnotationStroke?> getActiveStroke)
    {
        _getImageSize = getImageSize;
        _getImageBounds = getImageBounds;
        _getAnnotations = getAnnotations;
        _getActiveStroke = getActiveStroke;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var imageSize = _getImageSize();
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return;
        }

        var imageBounds = _getImageBounds();
        foreach (var annotation in _getAnnotations())
        {
            DrawAnnotation(context, annotation, imageSize, imageBounds);
        }

        var activeStroke = _getActiveStroke();
        if (activeStroke is not null)
        {
            DrawStroke(context, activeStroke, imageSize, imageBounds);
        }
    }

    internal static void DrawAnnotation(
        DrawingContext context,
        ImageAnnotation annotation,
        Size imageSize,
        Rect imageBounds)
    {
        switch (annotation)
        {
            case ImageAnnotationStroke stroke:
                DrawStroke(context, stroke, imageSize, imageBounds);
                break;
            case ImageAnnotationText text:
                DrawText(context, text, imageSize, imageBounds);
                break;
        }
    }

    private static void DrawStroke(
        DrawingContext context,
        ImageAnnotationStroke stroke,
        Size imageSize,
        Rect imageBounds)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        var thickness = Math.Max(1, stroke.Thickness * imageBounds.Width / imageSize.Width);
        var pen = new Pen(
            new SolidColorBrush(stroke.Color),
            thickness,
            dashStyle: null,
            lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round);
        var points = MapToViewport(stroke.Points, imageSize, imageBounds);
        if (stroke.Points.Count == 1)
        {
            context.DrawEllipse(new SolidColorBrush(stroke.Color), null, points[0], pen.Thickness / 2, pen.Thickness / 2);
            return;
        }

        var geometry = BuildSmoothGeometry(points);
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawText(
        DrawingContext context,
        ImageAnnotationText text,
        Size imageSize,
        Rect imageBounds)
    {
        if (string.IsNullOrWhiteSpace(text.Text))
        {
            return;
        }

        var scale = imageBounds.Width / imageSize.Width;
        var origin = MapToViewport(text.Location, imageSize, imageBounds);
        var layout = CreateTextLayout(text.Text, text.Color, Math.Max(8, text.FontSize * scale));
        layout.Draw(context, origin);
    }

    private static Point MapToViewport(Point imagePoint, Size imageSize, Rect imageBounds)
    {
        return new Point(
            imageBounds.X + imagePoint.X / imageSize.Width * imageBounds.Width,
            imageBounds.Y + imagePoint.Y / imageSize.Height * imageBounds.Height);
    }

    private static Point[] MapToViewport(IReadOnlyList<Point> imagePoints, Size imageSize, Rect imageBounds)
    {
        var points = new Point[imagePoints.Count];
        for (var i = 0; i < imagePoints.Count; i++)
        {
            points[i] = MapToViewport(imagePoints[i], imageSize, imageBounds);
        }

        return points;
    }

    internal static StreamGeometry BuildSmoothGeometry(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(points[0], isFilled: false);

        if (points.Count == 2)
        {
            context.LineTo(points[1]);
        }
        else
        {
            for (var i = 1; i < points.Count - 1; i++)
            {
                var midpoint = new Point(
                    (points[i].X + points[i + 1].X) / 2,
                    (points[i].Y + points[i + 1].Y) / 2);
                context.QuadraticBezierTo(points[i], midpoint);
            }

            context.LineTo(points[^1]);
        }

        context.EndFigure(isClosed: false);
        return geometry;
    }

    internal static TextLayout CreateTextLayout(string text, Color color, double fontSize)
    {
        return new TextLayout(
            text,
            ResolveTextTypeface(),
            fontSize,
            new SolidColorBrush(color),
            TextAlignment.Left,
            TextWrapping.NoWrap,
            TextTrimming.None);
    }

    private static Typeface ResolveTextTypeface()
    {
        var resources = Application.Current?.Resources;
        var fontFamily = resources?[ThemeKeys.SidebarFont] as FontFamily ?? FontFamily.Default;
        var fontWeight = resources?[ThemeKeys.SidebarFontWeight] as FontWeight? ?? FontWeight.Normal;
        var fontStyle = resources?[ThemeKeys.SidebarFontStyle] as FontStyle? ?? FontStyle.Normal;
        return new Typeface(fontFamily, fontStyle, fontWeight, FontStretch.Normal);
    }
}

internal abstract class ImageAnnotation
{
}

internal sealed class ImageAnnotationStroke : ImageAnnotation
{
    public ImageAnnotationStroke(Color color, double thickness, IReadOnlyList<Point> points)
    {
        Color = color;
        Thickness = thickness;
        Points = points;
    }

    public Color Color { get; }

    public double Thickness { get; }

    public IReadOnlyList<Point> Points { get; }
}

internal sealed class ImageAnnotationText : ImageAnnotation
{
    public ImageAnnotationText(string text, Point location, Color color, double fontSize)
    {
        Text = text;
        Location = location;
        Color = color;
        FontSize = fontSize;
    }

    public string Text { get; }

    public Point Location { get; }

    public Color Color { get; }

    public double FontSize { get; }
}
