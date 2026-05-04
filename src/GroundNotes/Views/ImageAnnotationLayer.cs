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
        var points = MapToViewport(stroke.Points, imageSize, imageBounds);
        if (stroke.Points.Count == 1)
        {
            var radius = thickness * stroke.Points[0].WidthScale / 2;
            context.DrawEllipse(new SolidColorBrush(stroke.Color), null, points[0], radius, radius);
            return;
        }

        if (stroke.HasVariableWidth)
        {
            DrawVariableWidthStroke(context, stroke, points, thickness);
            return;
        }

        var pen = CreateStrokePen(stroke.Color, thickness);
        var geometry = BuildSmoothGeometry(points);
        context.DrawGeometry(null, pen, geometry);
    }

    private static void DrawVariableWidthStroke(
        DrawingContext context,
        ImageAnnotationStroke stroke,
        IReadOnlyList<Point> points,
        double thickness)
    {
        var brush = new SolidColorBrush(stroke.Color);
        var geometry = BuildVariableWidthGeometry(points, stroke.Points, thickness);
        context.DrawGeometry(brush, null, geometry);

        var startRadius = thickness * stroke.Points[0].WidthScale / 2;
        var endRadius = thickness * stroke.Points[^1].WidthScale / 2;
        context.DrawEllipse(brush, null, points[0], startRadius, startRadius);
        context.DrawEllipse(brush, null, points[^1], endRadius, endRadius);
    }

    private static Pen CreateStrokePen(Color color, double thickness)
    {
        return new Pen(
            new SolidColorBrush(color),
            thickness,
            dashStyle: null,
            lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round);
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

    private static Point[] MapToViewport(IReadOnlyList<ImageAnnotationStrokePoint> imagePoints, Size imageSize, Rect imageBounds)
    {
        var points = new Point[imagePoints.Count];
        for (var i = 0; i < imagePoints.Count; i++)
        {
            points[i] = MapToViewport(imagePoints[i].Location, imageSize, imageBounds);
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

    internal static StreamGeometry BuildVariableWidthGeometry(
        IReadOnlyList<Point> points,
        IReadOnlyList<ImageAnnotationStrokePoint> strokePoints,
        double thickness)
    {
        var leftEdge = new Point[points.Count];
        var rightEdge = new Point[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var previous = i == 0 ? points[i] : points[i - 1];
            var next = i == points.Count - 1 ? points[i] : points[i + 1];
            var tangent = next - previous;
            var length = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
            var normal = length <= 0.001
                ? new Vector(0, -1)
                : new Vector(-tangent.Y / length, tangent.X / length);
            var radius = Math.Max(0.5, thickness * strokePoints[i].WidthScale / 2);
            leftEdge[i] = points[i] + normal * radius;
            rightEdge[i] = points[i] - normal * radius;
        }

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.SetFillRule(FillRule.NonZero);
        context.BeginFigure(leftEdge[0], isFilled: true);
        DrawSmoothEdge(context, leftEdge, reverse: false);
        context.LineTo(rightEdge[^1]);
        DrawSmoothEdge(context, rightEdge, reverse: true);
        context.EndFigure(isClosed: true);
        return geometry;
    }

    private static void DrawSmoothEdge(StreamGeometryContext context, IReadOnlyList<Point> edge, bool reverse)
    {
        if (edge.Count == 2)
        {
            context.LineTo(reverse ? edge[0] : edge[1]);
            return;
        }

        var start = reverse ? edge.Count - 2 : 1;
        var end = reverse ? 0 : edge.Count - 1;
        var step = reverse ? -1 : 1;
        for (var i = start; reverse ? i > end : i < end; i += step)
        {
            var nextIndex = i + step;
            var midpoint = new Point(
                (edge[i].X + edge[nextIndex].X) / 2,
                (edge[i].Y + edge[nextIndex].Y) / 2);
            context.QuadraticBezierTo(edge[i], midpoint);
        }

        context.LineTo(edge[end]);
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
        : this(color, thickness, points.Select(point => new ImageAnnotationStrokePoint(point, 1)).ToArray())
    {
    }

    public ImageAnnotationStroke(Color color, double thickness, IReadOnlyList<ImageAnnotationStrokePoint> points)
    {
        Color = color;
        Thickness = thickness;
        Points = points;
        HasVariableWidth = points.Any(point => Math.Abs(point.WidthScale - 1) > 0.001);
    }

    public Color Color { get; }

    public double Thickness { get; }

    public IReadOnlyList<ImageAnnotationStrokePoint> Points { get; }

    public bool HasVariableWidth { get; }
}

internal readonly record struct ImageAnnotationStrokePoint(Point Location, double WidthScale);

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
