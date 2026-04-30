using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace GroundNotes.Views;

public partial class ImageViewerWindow : Window
{
    private Bitmap? _bitmap;
    private double _zoom = 1;
    private Vector _pan;
    private bool _isDragging;
    private Point _dragStartPoint;
    private Vector _dragStartPan;

    public ImageViewerWindow()
    {
        InitializeComponent();
        Topmost = true;
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private ImageViewerWindow(Bitmap bitmap) : this()
    {
        _bitmap = bitmap;
        ViewerImage.Source = bitmap;
    }

    public static bool TryOpen(Window owner, string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return false;
        }

        Bitmap bitmap;
        try
        {
            bitmap = new Bitmap(imagePath);
        }
        catch
        {
            return false;
        }

        var window = new ImageViewerWindow(bitmap);
        window.ApplyScreenBounds(owner);
        window.Show(owner);
        Dispatcher.UIThread.Post(window.ActivateViewer, DispatcherPriority.Loaded);
        return true;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        WindowState = WindowState.FullScreen;
        Dispatcher.UIThread.Post(() =>
        {
            ActivateViewer();
            FitToViewport();
        }, DispatcherPriority.Loaded);
    }

    private void ActivateViewer()
    {
        Activate();
        Focus();
        ViewerViewport.Focus();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ViewerImage.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private void ApplyScreenBounds(Window owner)
    {
        var screen = Screens.ScreenFromWindow(owner) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }

        Position = screen.Bounds.Position;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;
    }

    private void FitToViewport()
    {
        if (_bitmap is null)
        {
            return;
        }

        var availableWidth = Math.Max(1, ViewerViewport.Bounds.Width - 24);
        var availableHeight = Math.Max(1, ViewerViewport.Bounds.Height - 24);
        var scaleX = availableWidth / Math.Max(1, _bitmap.Size.Width);
        var scaleY = availableHeight / Math.Max(1, _bitmap.Size.Height);
        _zoom = Math.Clamp(Math.Min(1, Math.Min(scaleX, scaleY)), 0.1, 8);
        _pan = default;
        UpdateImageLayout();
    }

    private void Zoom(double factor, Point? anchorPoint = null)
    {
        if (_bitmap is null)
        {
            return;
        }

        var previousZoom = _zoom;
        var nextZoom = Math.Clamp(previousZoom * factor, 0.1, 8);
        if (Math.Abs(nextZoom - previousZoom) < 0.001)
        {
            return;
        }

        var anchor = anchorPoint ?? new Point(ViewerViewport.Bounds.Width / 2, ViewerViewport.Bounds.Height / 2);
        var viewportCenter = new Point(ViewerViewport.Bounds.Width / 2, ViewerViewport.Bounds.Height / 2);
        var imageCenterBefore = viewportCenter + _pan;
        var vectorFromCenter = anchor - imageCenterBefore;
        var imageCenterAfter = anchor - (vectorFromCenter * (nextZoom / previousZoom));
        _pan = imageCenterAfter - viewportCenter;
        _zoom = nextZoom;
        UpdateImageLayout();
    }

    private void UpdateImageLayout()
    {
        if (_bitmap is null)
        {
            return;
        }

        ViewerImage.Width = Math.Max(1, _bitmap.Size.Width * _zoom);
        ViewerImage.Height = Math.Max(1, _bitmap.Size.Height * _zoom);
        ViewerImage.RenderTransform = new TranslateTransform(_pan.X, _pan.Y);
        ZoomResetButton.Content = $"{Math.Round(_zoom * 100)}%";
    }

    private Rect GetImageBounds()
    {
        var width = ViewerImage.Width;
        var height = ViewerImage.Height;
        var x = (ViewerViewport.Bounds.Width - width) / 2 + _pan.X;
        var y = (ViewerViewport.Bounds.Height - height) / 2 + _pan.Y;
        return new Rect(x, y, width, height);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_bitmap is null || !e.GetCurrentPoint(ViewerViewport).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStartPoint = e.GetPosition(ViewerViewport);
        if (!GetImageBounds().Contains(_dragStartPoint))
        {
            e.Handled = true;
            Close();
            return;
        }

        _isDragging = true;
        _dragStartPan = _pan;
        e.Pointer.Capture(ViewerViewport);
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var currentPoint = e.GetPosition(ViewerViewport);
        _pan = _dragStartPan + (currentPoint - _dragStartPoint);
        UpdateImageLayout();
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        Zoom(e.Delta.Y > 0 ? 1.15 : 1 / 1.15, e.GetPosition(ViewerViewport));
        e.Handled = true;
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e) => FitToViewport();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => Zoom(1 / 1.2);

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => Zoom(1.2);

    private void OnResetZoomClick(object? sender, RoutedEventArgs e) => FitToViewport();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
