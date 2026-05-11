using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace GroundNotes.Views;

internal sealed class EditorSmoothScrollController : IDisposable
{
    private const double WheelDeltaScrollPixels = 80;
    private const double AnimationStepFactor = 0.45;
    private const double SnapDistance = 0.5;
    private static readonly TimeSpan AnimationInterval = TimeSpan.FromMilliseconds(16);

    private readonly TextEditor _editor;
    private readonly DispatcherTimer _animationTimer;
    private ScrollViewer? _scrollViewer;
    private double _targetVerticalOffset;
    private bool _hasTargetVerticalOffset;

    public EditorSmoothScrollController(TextEditor editor)
    {
        _editor = editor;
        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = AnimationInterval,
        };
        _animationTimer.Tick += OnAnimationTimerTick;
        _editor.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    public void Dispose()
    {
        _editor.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTimerTick;
    }

    internal void CancelPendingScroll() => StopAnimation();

    internal void SetScrollOffset(Vector offset)
    {
        CancelPendingScroll();
        SetOffset(offset);
    }

    internal static SmoothScrollDecision CalculateVerticalWheelScroll(
        double currentVerticalOffset,
        double targetVerticalOffset,
        bool hasTargetVerticalOffset,
        double extentHeight,
        double viewportHeight,
        double deltaY,
        KeyModifiers keyModifiers)
    {
        if (keyModifiers.HasFlag(KeyModifiers.Shift)
            || keyModifiers.HasFlag(KeyModifiers.Control)
            || deltaY == 0
            || !double.IsFinite(deltaY)
            || !IsFinitePositive(extentHeight)
            || !IsFinitePositive(viewportHeight))
        {
            return SmoothScrollDecision.NotHandled;
        }

        var maxVerticalOffset = Math.Max(0, extentHeight - viewportHeight);
        if (maxVerticalOffset <= 0)
        {
            return SmoothScrollDecision.NotHandled;
        }

        var baseVerticalOffset = hasTargetVerticalOffset ? targetVerticalOffset : currentVerticalOffset;
        var nextVerticalOffset = Math.Clamp(baseVerticalOffset - (deltaY * WheelDeltaScrollPixels), 0, maxVerticalOffset);
        return new SmoothScrollDecision(true, nextVerticalOffset);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || _editor.Document is null || !TryGetScrollState(out var offset, out var extent, out var viewport))
        {
            return;
        }

        var decision = CalculateVerticalWheelScroll(
            offset.Y,
            _targetVerticalOffset,
            _hasTargetVerticalOffset,
            extent.Height,
            viewport.Height,
            e.Delta.Y,
            e.KeyModifiers);
        if (!decision.ShouldHandle)
        {
            return;
        }

        _targetVerticalOffset = decision.NextVerticalOffset;
        _hasTargetVerticalOffset = true;
        e.Handled = true;

        if (!_animationTimer.IsEnabled)
        {
            _animationTimer.Start();
        }

        AdvanceAnimation();
    }

    private void OnAnimationTimerTick(object? sender, EventArgs e) => AdvanceAnimation();

    private void AdvanceAnimation()
    {
        if (!_hasTargetVerticalOffset || !TryGetScrollState(out var currentOffset, out var extent, out var viewport))
        {
            StopAnimation();
            return;
        }

        var maxVerticalOffset = Math.Max(0, extent.Height - viewport.Height);
        _targetVerticalOffset = Math.Clamp(_targetVerticalOffset, 0, maxVerticalOffset);

        var remaining = _targetVerticalOffset - currentOffset.Y;
        if (Math.Abs(remaining) <= SnapDistance)
        {
            SetOffset(new Vector(currentOffset.X, _targetVerticalOffset));
            StopAnimation();
            return;
        }

        SetOffset(new Vector(currentOffset.X, currentOffset.Y + (remaining * AnimationStepFactor)));
    }

    private bool TryGetScrollState(out Vector offset, out Size extent, out Size viewport)
    {
        var scrollViewer = GetScrollViewer();
        if (scrollViewer is not null)
        {
            offset = scrollViewer.Offset;
            extent = scrollViewer.Extent;
            viewport = scrollViewer.Viewport;
            return true;
        }

        if (_editor.TextArea is IScrollable scrollable)
        {
            offset = scrollable.Offset;
            extent = scrollable.Extent;
            viewport = scrollable.Viewport;
            return true;
        }

        offset = default;
        extent = default;
        viewport = default;
        return false;
    }

    private void SetOffset(Vector offset)
    {
        var scrollViewer = GetScrollViewer();
        if (scrollViewer is not null)
        {
            scrollViewer.Offset = offset;
            return;
        }

        if (_editor.TextArea is IScrollable scrollable)
        {
            scrollable.Offset = offset;
        }
    }

    private ScrollViewer? GetScrollViewer()
    {
        if (_scrollViewer is not null)
        {
            return _scrollViewer;
        }

        _editor.ApplyTemplate();
        _scrollViewer = _editor.GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault(scrollViewer => string.Equals(scrollViewer.Name, "PART_ScrollViewer", StringComparison.Ordinal));
        return _scrollViewer;
    }

    private void StopAnimation()
    {
        _animationTimer.Stop();
        _hasTargetVerticalOffset = false;
    }

    private static bool IsFinitePositive(double value) => double.IsFinite(value) && value > 0;

    internal readonly record struct SmoothScrollDecision(bool ShouldHandle, double NextVerticalOffset)
    {
        public static SmoothScrollDecision NotHandled { get; } = new(false, 0);
    }
}
