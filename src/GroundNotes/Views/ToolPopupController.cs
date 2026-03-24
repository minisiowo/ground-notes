using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace GroundNotes.Views;

internal sealed class ToolPopupController
{
    private readonly Popup _popup;
    private readonly Control _popupContent;
    private bool _isRefreshQueued;
    private bool _needsPlacementReset;

    public ToolPopupController(Popup popup, Control popupContent)
    {
        _popup = popup;
        _popupContent = popupContent;

        _popup.Opened += OnPopupOpened;
        _popup.Closed += OnPopupClosed;
    }

    public void ScheduleRefresh(bool resetPlacement = false, DispatcherPriority? priority = null)
    {
        if (!_popup.IsOpen)
        {
            _needsPlacementReset |= resetPlacement;
            return;
        }

        _needsPlacementReset |= resetPlacement;
        if (_isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isRefreshQueued = false;
            Refresh();
        }, priority ?? DispatcherPriority.Render);
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        ScheduleRefresh(resetPlacement: true, priority: DispatcherPriority.Render);

        // Some compositors leave stale pixels until the next frame; force one more pass.
        Dispatcher.UIThread.Post(() =>
        {
            ScheduleRefresh(priority: DispatcherPriority.Background);
        }, DispatcherPriority.Render);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        _isRefreshQueued = false;
        _needsPlacementReset = false;
    }

    private void Refresh()
    {
        if (!_popup.IsOpen)
        {
            return;
        }

        try
        {
            _popupContent.InvalidateMeasure();
            _popupContent.InvalidateArrange();
            _popupContent.InvalidateVisual();
            _popupContent.UpdateLayout();
            _popupContent.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            if (_needsPlacementReset)
            {
                var placementRect = _popup.PlacementRect;
                _popup.PlacementRect = default;
                _popup.PlacementRect = placementRect;
                _needsPlacementReset = false;
            }

            _popup.InvalidateMeasure();
            _popup.InvalidateArrange();
            _popup.InvalidateVisual();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
