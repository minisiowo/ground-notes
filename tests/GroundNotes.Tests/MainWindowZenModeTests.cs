using Avalonia;
using Avalonia.Input;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MainWindowZenModeTests
{
    [Fact]
    public void IsPaneVisibleInZenMode_ShowsPrimaryWhenNoSecondaryPaneIsActive()
    {
        Assert.True(MainWindow.IsPaneVisibleInZenMode(activeSecondaryPaneId: null, paneId: null));
        Assert.False(MainWindow.IsPaneVisibleInZenMode(activeSecondaryPaneId: null, Guid.NewGuid()));
    }

    [Fact]
    public void IsPaneVisibleInZenMode_ShowsOnlyActiveSecondaryPane()
    {
        var activePaneId = Guid.NewGuid();

        Assert.False(MainWindow.IsPaneVisibleInZenMode(activePaneId, paneId: null));
        Assert.True(MainWindow.IsPaneVisibleInZenMode(activePaneId, activePaneId));
        Assert.False(MainWindow.IsPaneVisibleInZenMode(activePaneId, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(718, 10, 10, 697)]
    [InlineData(558, 10, 10, 537)]
    [InlineData(400, 0, 0, 399)]
    public void CalculateZenPaneWidth_FillsWindowInsideWorkspaceMargins(
        double windowWidth,
        double leftMargin,
        double rightMargin,
        double expected)
    {
        var actual = MainWindow.CalculateZenPaneWidth(
            windowWidth,
            new Thickness(leftMargin, 10, rightMargin, 10));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1000d, null, 1000d)]
    [InlineData(1000d, 720d, 720d)]
    [InlineData(600d, 720d, 600d)]
    [InlineData(0d, 720d, 0d)]
    public void CalculateZenEditorWidth_UsesTransientPreferenceWithinAvailableWidth(
        double availableWidth,
        double? preferredWidth,
        double expected)
    {
        var actual = MainWindow.CalculateZenEditorWidth(availableWidth, preferredWidth);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(200, 5, KeyModifiers.None, true)]
    [InlineData(5, 150, KeyModifiers.None, true)]
    [InlineData(395, 150, KeyModifiers.None, true)]
    [InlineData(200, 295, KeyModifiers.None, true)]
    [InlineData(200, 150, KeyModifiers.None, false)]
    [InlineData(200, 150, KeyModifiers.Alt, true)]
    public void IsZenWindowDragGesture_UsesOuterGutterOrAltDrag(
        double x,
        double y,
        KeyModifiers modifiers,
        bool expected)
    {
        var actual = MainWindow.IsZenWindowDragGesture(
            new Size(400, 300),
            new Point(x, y),
            modifiers);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(16, 600, 32, 0)]
    [InlineData(300, 600, 32, 284)]
    [InlineData(590, 600, 32, 568)]
    [InlineData(10, 20, 32, 0)]
    public void CalculateEditorResizeStripeTop_FollowsPointerWithinHandleBounds(
        double pointerY,
        double handleHeight,
        double stripeHeight,
        double expected)
    {
        var actual = MainWindow.CalculateEditorResizeStripeTop(pointerY, handleHeight, stripeHeight);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, 320, false, 280, 0, 320)]
    [InlineData(true, 320, true, 280, 0, 320)]
    [InlineData(false, 320, true, 280, 0, 280)]
    [InlineData(false, 320, false, 280, 360, 360)]
    public void ResolveSidebarWidthForLayout_PreservesUnderlyingLayout(
        bool isZenMode,
        double sidebarWidthBeforeZenMode,
        bool sidebarCollapsed,
        double sidebarWidthBeforeCollapse,
        double currentSidebarWidth,
        double expected)
    {
        var actual = MainWindow.ResolveSidebarWidthForLayout(
            isZenMode,
            sidebarWidthBeforeZenMode,
            sidebarCollapsed,
            sidebarWidthBeforeCollapse,
            currentSidebarWidth);

        Assert.Equal(expected, actual);
    }
}
