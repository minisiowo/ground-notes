using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace GroundNotes.Views;

public sealed class WindowChromeController
{
    private const double WindowResizeBorderThickness = 6;
    private const double WindowCornerResizeThickness = 10;

    private readonly Window _window;
    private readonly Options _options;
    private WindowEdge? _activeResizeEdge;

    public WindowChromeController(Window window, Options? options = null)
    {
        _window = window;
        _options = options ?? new Options();
    }

    public void OnWindowPointerMoved(PointerEventArgs e)
    {
        if (_options.CheckCanResizeOnHover && !_window.CanResize)
        {
            ClearWindowResizeCursor();
            return;
        }

        if (_options.CheckWindowStateOnHover && _window.WindowState != WindowState.Normal)
        {
            ClearWindowResizeCursor();
            return;
        }

        var edge = TryGetResizeEdge(_window.Bounds.Size, e.GetPosition(_window));
        var isCorner = edge is WindowEdge.NorthWest or WindowEdge.NorthEast
            or WindowEdge.SouthWest or WindowEdge.SouthEast;

        if (!isCorner && _options.IsInteractiveControl?.Invoke(e) == true)
        {
            ClearWindowResizeCursor();
            return;
        }

        _activeResizeEdge = edge;
        _window.Cursor = _activeResizeEdge switch
        {
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => _options.IdleCursor
        };
    }

    public void OnWindowPointerExited()
    {
        ClearWindowResizeCursor();
    }

    public void OnWindowPointerPressed(PointerPressedEventArgs e)
    {
        if (!_window.CanResize)
        {
            return;
        }

        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_options.CheckWindowStateOnResizePressed && _window.WindowState != WindowState.Normal)
        {
            return;
        }

        var edge = _activeResizeEdge ?? TryGetResizeEdge(_window.Bounds.Size, e.GetPosition(_window));
        if (edge is null)
        {
            return;
        }

        var isCorner = edge is WindowEdge.NorthWest or WindowEdge.NorthEast
            or WindowEdge.SouthWest or WindowEdge.SouthEast;

        if (!isCorner && _options.IsInteractiveControl?.Invoke(e) == true)
        {
            return;
        }

        try
        {
            e.Handled = true;
            _window.BeginResizeDrag(edge.Value, e);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _activeResizeEdge = null;
        }
    }

    public void OnTitleBarPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_window).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (_options.IsInteractiveControl?.Invoke(e) == true)
        {
            return;
        }

        try
        {
            _window.BeginMoveDrag(e);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void OnTitleBarDoubleTapped(TappedEventArgs e)
    {
        if (!_window.CanResize)
        {
            return;
        }

        if (_options.ShouldSuppressTitleBarDoubleTap?.Invoke(e) == true)
        {
            return;
        }

        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    public void OnMinimizeClick()
    {
        _window.WindowState = WindowState.Minimized;
    }

    public void OnMaximizeRestoreClick()
    {
        if (!_window.CanResize)
        {
            return;
        }

        _window.WindowState = _window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    public void OnCloseClick()
    {
        _window.Close();
    }

    private void ClearWindowResizeCursor()
    {
        _activeResizeEdge = null;
        _window.Cursor = _options.IdleCursor;
    }

    public static WindowEdge? TryGetResizeEdge(Size bounds, Point point)
    {
        var width = bounds.Width;
        var height = bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var cornerLeft = point.X >= 0 && point.X <= WindowCornerResizeThickness;
        var cornerRight = point.X <= width && point.X >= width - WindowCornerResizeThickness;
        var cornerTop = point.Y >= 0 && point.Y <= WindowCornerResizeThickness;
        var cornerBottom = point.Y <= height && point.Y >= height - WindowCornerResizeThickness;

        if (cornerTop && cornerLeft)
        {
            return WindowEdge.NorthWest;
        }

        if (cornerTop && cornerRight)
        {
            return WindowEdge.NorthEast;
        }

        if (cornerBottom && cornerLeft)
        {
            return WindowEdge.SouthWest;
        }

        if (cornerBottom && cornerRight)
        {
            return WindowEdge.SouthEast;
        }

        var onLeft = point.X >= 0 && point.X <= WindowResizeBorderThickness;
        var onRight = point.X <= width && point.X >= width - WindowResizeBorderThickness;
        var onTop = point.Y >= 0 && point.Y <= WindowResizeBorderThickness;
        var onBottom = point.Y <= height && point.Y >= height - WindowResizeBorderThickness;

        if (onLeft)
        {
            return WindowEdge.West;
        }

        if (onRight)
        {
            return WindowEdge.East;
        }

        if (onTop)
        {
            return WindowEdge.North;
        }

        if (onBottom)
        {
            return WindowEdge.South;
        }

        return null;
    }
    public sealed record Options
    {
        public Cursor? IdleCursor { get; init; }

        public bool CheckCanResizeOnHover { get; init; } = true;

        public bool CheckWindowStateOnHover { get; init; } = true;

        public bool CheckWindowStateOnResizePressed { get; init; } = true;

        public Func<PointerEventArgs, bool>? IsInteractiveControl { get; init; }

        public Func<TappedEventArgs, bool>? ShouldSuppressTitleBarDoubleTap { get; init; }
    }
}
