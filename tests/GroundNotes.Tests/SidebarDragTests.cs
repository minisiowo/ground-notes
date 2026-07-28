using Avalonia;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class SidebarDragTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(100, 0, true)]
    [InlineData(0, 40, true)]
    [InlineData(100, 40, true)]
    [InlineData(50, 20, true)]
    [InlineData(-0.1, 20, false)]
    [InlineData(100.1, 20, false)]
    [InlineData(50, -0.1, false)]
    [InlineData(50, 40.1, false)]
    public void IsPointWithinSidebarDropTarget_IncludesBoundsOnly(double x, double y, bool expected)
    {
        var result = MainWindow.IsPointWithinSidebarDropTarget(new Point(x, y), new Size(100, 40));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseSidebarDragPaths_TrimsSplitsAndDeduplicatesPaths()
    {
        var paths = MainWindow.ParseSidebarDragPaths(
        [
            " /notes/alpha.md \n/notes/beta.md",
            "",
            "/notes/ALPHA.md\n /notes/gamma.md "
        ]);

        Assert.Equal(
            ["/notes/alpha.md", "/notes/beta.md", "/notes/gamma.md"],
            paths);
    }

    [Fact]
    public void ParseSidebarDragPaths_ReturnsEmptyForMissingValues()
    {
        Assert.Empty(MainWindow.ParseSidebarDragPaths(null));
    }

    [Fact]
    public void SidebarDragGhostPositionState_CoalescesPendingPositions()
    {
        var state = new SidebarDragGhostPositionState();

        Assert.True(state.Queue(new Point(10, 20)));
        Assert.False(state.Queue(new Point(30, 40)));
        Assert.True(state.TryConsume(out var position));
        Assert.Equal(new Point(30, 40), position);
        Assert.False(state.TryConsume(out _));
    }

    [Fact]
    public void SidebarDragGhostPositionState_ResetDiscardsPendingPosition()
    {
        var state = new SidebarDragGhostPositionState();

        Assert.True(state.Queue(new Point(10, 20)));
        state.Reset();

        Assert.False(state.TryConsume(out _));
        Assert.True(state.Queue(new Point(30, 40)));
    }
}
