using Avalonia;

namespace GroundNotes.Views;

internal sealed class ImageAnnotationStrokeSmoother
{
    private const double PositionSmoothing = 0.62;
    private const double WidthSmoothing = 0.28;
    private const double MinWidthScale = 0.75;
    private const double MaxWidthScale = 1.22;
    private const double SpeedReference = 1.35;

    private readonly List<ImageAnnotationStrokePoint> _points = [];
    private readonly double _spacing;
    private readonly double _jitterDistance;
    private Point _lastRawPoint;
    private Point _lastFilteredPoint;
    private ulong _lastTimestamp;
    private double _lastWidthScale = 1;
    private bool _hasStarted;

    public ImageAnnotationStrokeSmoother(double thickness)
    {
        _spacing = Math.Clamp(thickness * 0.28, 1.0, 6.0);
        _jitterDistance = Math.Clamp(thickness * 0.08, 0.45, 1.8);
    }

    public IReadOnlyList<ImageAnnotationStrokePoint> Points => _points;

    public void Begin(Point point, ulong timestamp)
    {
        _points.Clear();
        _lastRawPoint = point;
        _lastFilteredPoint = point;
        _lastTimestamp = timestamp;
        _lastWidthScale = 1;
        _hasStarted = true;
        _points.Add(new ImageAnnotationStrokePoint(point, _lastWidthScale));
    }

    public bool Add(Point point, ulong timestamp)
    {
        if (!_hasStarted)
        {
            Begin(point, timestamp);
            return true;
        }

        return AddCore(point, timestamp, forceEndpoint: false);
    }

    public bool Finish(Point point, ulong timestamp)
    {
        if (!_hasStarted)
        {
            Begin(point, timestamp);
            return true;
        }

        return AddCore(point, timestamp, forceEndpoint: true);
    }

    public ImageAnnotationStrokePoint[] ToArray() => _points.ToArray();

    private bool AddCore(Point rawPoint, ulong timestamp, bool forceEndpoint)
    {
        var rawDistance = Distance(_lastRawPoint, rawPoint);
        if (!forceEndpoint && rawDistance < _jitterDistance)
        {
            return false;
        }

        var elapsed = timestamp > _lastTimestamp
            ? timestamp - _lastTimestamp
            : 8;
        var speed = rawDistance / elapsed;
        var targetWidthScale = ComputeWidthScale(speed);
        var filteredPoint = Lerp(_lastFilteredPoint, rawPoint, PositionSmoothing);
        var widthScale = Lerp(_lastWidthScale, targetWidthScale, WidthSmoothing);
        var changed = AddResampledPoints(filteredPoint, widthScale, forceEndpoint);

        _lastRawPoint = rawPoint;
        _lastFilteredPoint = filteredPoint;
        _lastTimestamp = timestamp;
        _lastWidthScale = widthScale;
        return changed;
    }

    private bool AddResampledPoints(Point targetPoint, double targetWidthScale, bool forceEndpoint)
    {
        var changed = false;
        var lastPoint = _points[^1];
        var distance = Distance(lastPoint.Location, targetPoint);
        if (distance <= 0)
        {
            return false;
        }

        while (distance >= _spacing)
        {
            var ratio = _spacing / distance;
            var nextLocation = Lerp(lastPoint.Location, targetPoint, ratio);
            var nextWidthScale = Lerp(lastPoint.WidthScale, targetWidthScale, ratio);
            lastPoint = new ImageAnnotationStrokePoint(nextLocation, nextWidthScale);
            _points.Add(lastPoint);
            changed = true;
            distance = Distance(lastPoint.Location, targetPoint);
        }

        if (forceEndpoint && Distance(_points[^1].Location, targetPoint) >= _jitterDistance)
        {
            _points.Add(new ImageAnnotationStrokePoint(targetPoint, targetWidthScale));
            changed = true;
        }

        return changed;
    }

    private static double ComputeWidthScale(double speed)
    {
        var normalizedSpeed = Math.Clamp(speed / SpeedReference, 0, 1);
        return MaxWidthScale - normalizedSpeed * (MaxWidthScale - MinWidthScale);
    }

    private static Point Lerp(Point from, Point to, double amount)
    {
        return new Point(
            Lerp(from.X, to.X, amount),
            Lerp(from.Y, to.Y, amount));
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + (to - from) * amount;
    }

    private static double Distance(Point first, Point second)
    {
        var x = second.X - first.X;
        var y = second.Y - first.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
