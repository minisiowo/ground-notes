using Avalonia;
using Avalonia.Controls;
using QuickNotesTxt.Views;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class WindowChromeControllerTests
{
    [Theory]
    [InlineData(2, 2, WindowEdge.NorthWest)]
    [InlineData(398, 2, WindowEdge.NorthEast)]
    [InlineData(2, 298, WindowEdge.SouthWest)]
    [InlineData(398, 298, WindowEdge.SouthEast)]
    [InlineData(200, 2, WindowEdge.North)]
    [InlineData(200, 298, WindowEdge.South)]
    [InlineData(2, 150, WindowEdge.West)]
    [InlineData(398, 150, WindowEdge.East)]
    public void TryGetResizeEdge_DetectsExpectedZone(double x, double y, WindowEdge expected)
    {
        var edge = WindowChromeController.TryGetResizeEdge(new Size(400, 300), new Point(x, y));

        Assert.Equal(expected, edge);
    }

    [Fact]
    public void TryGetResizeEdge_ReturnsNullOutsideResizeZones()
    {
        var edge = WindowChromeController.TryGetResizeEdge(new Size(400, 300), new Point(200, 150));

        Assert.Null(edge);
    }
}
