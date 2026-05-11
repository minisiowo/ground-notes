using Avalonia.Input;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class EditorSmoothScrollControllerTests
{
    [Fact]
    public void CalculateVerticalWheelScroll_UsesSmallerWheelStep()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 100,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: -1,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(180, decision.NextVerticalOffset);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_PreservesFractionalWheelDelta()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 100,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: -0.25,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(120, decision.NextVerticalOffset);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CalculateVerticalWheelScroll_IgnoresNonFiniteWheelDelta(double deltaY)
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 100,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: deltaY,
            keyModifiers: KeyModifiers.None);

        Assert.False(decision.ShouldHandle);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_CoalescesAgainstPendingTarget()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 110,
            targetVerticalOffset: 160,
            hasTargetVerticalOffset: true,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: -1,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(240, decision.NextVerticalOffset);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_CancelsPendingTargetAtBoundary()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 0,
            targetVerticalOffset: 30,
            hasTargetVerticalOffset: true,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: 1,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(0, decision.NextVerticalOffset);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_CancelsPendingTargetAtBottomBoundary()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 800,
            targetVerticalOffset: 770,
            hasTargetVerticalOffset: true,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: -1,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(800, decision.NextVerticalOffset);
    }

    [Theory]
    [InlineData(KeyModifiers.Shift)]
    [InlineData(KeyModifiers.Control)]
    [InlineData(KeyModifiers.Shift | KeyModifiers.Control)]
    public void CalculateVerticalWheelScroll_IgnoresShiftAndControlWheel(KeyModifiers keyModifiers)
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 100,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: -1,
            keyModifiers: keyModifiers);

        Assert.False(decision.ShouldHandle);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_ClampsToTopAndHandlesBoundaryWheel()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 0,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: 1,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(0, decision.NextVerticalOffset);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_ClampsToBottom()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 790,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 1000,
            viewportHeight: 200,
            deltaY: -1,
            keyModifiers: KeyModifiers.None);

        Assert.True(decision.ShouldHandle);
        Assert.Equal(800, decision.NextVerticalOffset);
    }

    [Fact]
    public void CalculateVerticalWheelScroll_DoesNotHandleWhenContentDoesNotOverflow()
    {
        var decision = EditorSmoothScrollController.CalculateVerticalWheelScroll(
            currentVerticalOffset: 0,
            targetVerticalOffset: 0,
            hasTargetVerticalOffset: false,
            extentHeight: 200,
            viewportHeight: 200,
            deltaY: -1,
            keyModifiers: KeyModifiers.None);

        Assert.False(decision.ShouldHandle);
    }
}
