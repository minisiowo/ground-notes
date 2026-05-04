using Avalonia;
using Xunit;

using GroundNotes.Views;

namespace GroundNotes.Tests;

public sealed class ImageAnnotationStrokeSmootherTests
{
    [Fact]
    public void Add_IgnoresJitterBelowThreshold()
    {
        var smoother = new ImageAnnotationStrokeSmoother(thickness: 6);
        smoother.Begin(new Point(10, 10), timestamp: 0);

        var changed = smoother.Add(new Point(10.1, 10.1), timestamp: 4);

        Assert.False(changed);
        Assert.Single(smoother.Points);
    }

    [Fact]
    public void Add_ResamplesLongSegmentsIntoStableSpacing()
    {
        var smoother = new ImageAnnotationStrokeSmoother(thickness: 6);
        smoother.Begin(new Point(0, 0), timestamp: 0);

        var changed = smoother.Add(new Point(40, 0), timestamp: 20);

        Assert.True(changed);
        Assert.True(smoother.Points.Count > 2);
        for (var i = 1; i < smoother.Points.Count; i++)
        {
            var distance = Distance(smoother.Points[i - 1].Location, smoother.Points[i].Location);
            Assert.InRange(distance, 0.5, 2.0);
        }
    }

    [Fact]
    public void Add_UsesLowerWidthScaleForFastMovement()
    {
        var slow = new ImageAnnotationStrokeSmoother(thickness: 6);
        slow.Begin(new Point(0, 0), timestamp: 0);
        slow.Add(new Point(30, 0), timestamp: 120);

        var fast = new ImageAnnotationStrokeSmoother(thickness: 6);
        fast.Begin(new Point(0, 0), timestamp: 0);
        fast.Add(new Point(30, 0), timestamp: 5);

        Assert.True(slow.Points[^1].WidthScale > fast.Points[^1].WidthScale);
    }

    [Fact]
    public void Finish_PreservesSinglePointStroke()
    {
        var smoother = new ImageAnnotationStrokeSmoother(thickness: 6);
        smoother.Begin(new Point(4, 8), timestamp: 0);

        smoother.Finish(new Point(4, 8), timestamp: 15);

        var point = Assert.Single(smoother.Points);
        Assert.Equal(new Point(4, 8), point.Location);
    }

    private static double Distance(Point first, Point second)
    {
        var x = second.X - first.X;
        var y = second.Y - first.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
