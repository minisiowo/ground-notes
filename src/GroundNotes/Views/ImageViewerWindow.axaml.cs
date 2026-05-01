using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace GroundNotes.Views;

public partial class ImageViewerWindow : Window
{
    private const double DefaultAnnotationSize = 6;
    private const double MinAnnotationSize = 2;
    private const double MaxAnnotationSize = 24;
    private const double AnnotationThumbSize = 18;
    private static readonly Color DefaultAnnotationColor = Color.Parse("#E5484D");

    private readonly Func<string, RenderTargetBitmap, bool, Task<ImageViewerSaveResult>>? _saveAsync;
    private readonly Func<RenderTargetBitmap, Task<bool>>? _copyAsync;
    private readonly List<ImageAnnotation> _annotations = [];
    private readonly List<Point> _activeStrokePoints = [];
    private Bitmap? _bitmap;
    private string _imagePath = string.Empty;
    private ImageAnnotationLayer? _annotationLayer;
    private double _zoom = 1;
    private Vector _pan;
    private bool _isDragging;
    private bool _isDrawingAnnotation;
    private bool _isChangingAnnotationSize;
    private bool _isDraggingTextAnnotation;
    private bool _isPointerDownInAnnotationToolbar;
    private Color _annotationColor = DefaultAnnotationColor;
    private double _annotationSize = DefaultAnnotationSize;
    private ImageAnnotationTool _activeTool = ImageAnnotationTool.None;
    private Point? _pendingTextLocation;
    private Color _pendingTextColor = DefaultAnnotationColor;
    private double _pendingTextFontSize;
    private int? _editingTextAnnotationIndex;
    private ImageAnnotationText? _editingOriginalTextAnnotation;
    private Point _textDragStartPoint;
    private Point _textDragStartLocation;
    private Point _dragStartPoint;
    private Vector _dragStartPan;

    public ImageViewerWindow()
    {
        InitializeComponent();
        Topmost = true;
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnWindowPointerPressedForAnnotationToolbar, RoutingStrategies.Tunnel, handledEventsToo: true);
        AnnotationTextEditor.AddHandler(PointerPressedEvent, OnAnnotationTextEditorPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AnnotationTextEditor.AddHandler(PointerMovedEvent, OnAnnotationTextEditorPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AnnotationTextEditor.AddHandler(PointerReleasedEvent, OnAnnotationTextEditorPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AnnotationSizeControl.SizeChanged += (_, _) => UpdateAnnotationSizeThumb();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private ImageViewerWindow(
        string imagePath,
        Bitmap bitmap,
        Func<string, RenderTargetBitmap, bool, Task<ImageViewerSaveResult>>? saveAsync,
        Func<RenderTargetBitmap, Task<bool>>? copyAsync) : this()
    {
        _imagePath = imagePath;
        _bitmap = bitmap;
        _saveAsync = saveAsync;
        _copyAsync = copyAsync;
        ViewerImage.Source = bitmap;
        _annotationLayer = new ImageAnnotationLayer(GetBitmapSize, GetImageBounds, () => _annotations, GetActiveStroke);
        ViewerViewport.Children.Add(_annotationLayer);
        UpdateAnnotationSizeThumb();
        UpdateAnnotationButtons();
    }

    public static bool TryOpen(
        Window owner,
        string imagePath,
        Func<string, RenderTargetBitmap, bool, Task<ImageViewerSaveResult>>? saveAsync = null,
        Func<RenderTargetBitmap, Task<bool>>? copyAsync = null)
    {
        if (!File.Exists(imagePath))
        {
            return false;
        }

        Bitmap bitmap;
        try
        {
            bitmap = LoadBitmap(imagePath);
        }
        catch
        {
            return false;
        }

        var window = new ImageViewerWindow(imagePath, bitmap, saveAsync, copyAsync);
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
        CancelTextAnnotationEdit(commit: false);
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
        UpdateActiveTextEditorPosition();
        _annotationLayer?.InvalidateVisual();
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
        if (e.Key == Key.Escape && AnnotationTextEditor.IsVisible)
        {
            e.Handled = true;
            CancelTextAnnotationEdit(commit: false);
            return;
        }

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
            if (_activeTool == ImageAnnotationTool.None)
            {
                Close();
            }

            return;
        }

        if (_activeTool == ImageAnnotationTool.Text)
        {
            if (TryActivateTextAnnotation(_dragStartPoint))
            {
                e.Handled = true;
                return;
            }

            BeginTextAnnotationEdit(_dragStartPoint, text: string.Empty, existingIndex: null);
            e.Handled = true;
            return;
        }

        if (_activeTool == ImageAnnotationTool.Pen)
        {
            _isDrawingAnnotation = true;
            _activeStrokePoints.Clear();
            _activeStrokePoints.Add(MapViewportToImage(_dragStartPoint));
            e.Pointer.Capture(ViewerViewport);
            e.Handled = true;
            return;
        }

        _isDragging = true;
        _dragStartPan = _pan;
        e.Pointer.Capture(ViewerViewport);
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDrawingAnnotation)
        {
            var annotationPoint = e.GetPosition(ViewerViewport);
            if (GetImageBounds().Contains(annotationPoint))
            {
                AddActiveAnnotationPoint(MapViewportToImage(annotationPoint));
            }

            e.Handled = true;
            return;
        }

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
        if (_isDrawingAnnotation)
        {
            _isDrawingAnnotation = false;
            e.Pointer.Capture(null);
            CommitActiveAnnotationStroke();
            e.Handled = true;
            return;
        }

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

    private void OnPenToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (PenToggleButton.IsChecked == true)
        {
            SetActiveTool(ImageAnnotationTool.Pen);
        }
        else if (_activeTool == ImageAnnotationTool.Pen)
        {
            SetActiveTool(ImageAnnotationTool.None);
        }
    }

    private void OnTextToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (TextToggleButton.IsChecked == true)
        {
            SetActiveTool(ImageAnnotationTool.Text);
        }
        else if (_activeTool == ImageAnnotationTool.Text)
        {
            SetActiveTool(ImageAnnotationTool.None);
        }
    }

    private void OnWindowPointerPressedForAnnotationToolbar(object? sender, PointerPressedEventArgs e)
    {
        _isPointerDownInAnnotationToolbar = e.Source is Control control
            && control.FindAncestorOfType<DockPanel>() is DockPanel dockPanel
            && ReferenceEquals(dockPanel.Parent, RootGrid);
    }

    private void OnAnnotationColorClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _annotationColor = ResolveAnnotationButtonColor(button);
        UpdateActiveTextColorFromAnnotationColor();
        if (_activeTool == ImageAnnotationTool.None)
        {
            SetActiveTool(ImageAnnotationTool.Pen);
        }
    }

    private void OnAnnotationSizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(AnnotationSizeControl).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isChangingAnnotationSize = true;
        e.Pointer.Capture(AnnotationSizeControl);
        UpdateAnnotationSizeFromPoint(e.GetPosition(AnnotationSizeControl));
        e.Handled = true;
    }

    private void OnAnnotationSizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isChangingAnnotationSize)
        {
            return;
        }

        UpdateAnnotationSizeFromPoint(e.GetPosition(AnnotationSizeControl));
        e.Handled = true;
    }

    private void OnAnnotationSizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isChangingAnnotationSize)
        {
            return;
        }

        _isChangingAnnotationSize = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnUndoAnnotationClick(object? sender, RoutedEventArgs e)
    {
        if (_annotations.Count == 0)
        {
            return;
        }

        _annotations.RemoveAt(_annotations.Count - 1);
        _annotationLayer?.InvalidateVisual();
        UpdateAnnotationButtons();
    }

    private async void OnSaveCopyClick(object? sender, RoutedEventArgs e) => await SaveAnnotatedImageAsync(overwrite: false);

    private async void OnOverwriteClick(object? sender, RoutedEventArgs e) => await SaveAnnotatedImageAsync(overwrite: true);

    private async void OnCopyAnnotatedClick(object? sender, RoutedEventArgs e) => await CopyAnnotatedImageAsync();

    private async Task SaveAnnotatedImageAsync(bool overwrite)
    {
        CancelTextAnnotationEdit(commit: true);
        if (_bitmap is null || _saveAsync is null || _annotations.Count == 0)
        {
            return;
        }

        SaveCopyButton.IsEnabled = false;
        OverwriteButton.IsEnabled = false;
        try
        {
            using var annotatedBitmap = RenderAnnotatedBitmap();
            var result = await _saveAsync(_imagePath, annotatedBitmap, overwrite);
            if (!result.Success || string.IsNullOrWhiteSpace(result.ImagePath))
            {
                return;
            }

            _imagePath = result.ImagePath;
            ReplaceDisplayedBitmap(LoadBitmap(_imagePath));
            _annotations.Clear();
            _activeStrokePoints.Clear();
            _annotationLayer?.InvalidateVisual();
        }
        finally
        {
            UpdateAnnotationButtons();
        }
    }

    private async Task CopyAnnotatedImageAsync()
    {
        CancelTextAnnotationEdit(commit: true);
        if (_bitmap is null || _copyAsync is null || _annotations.Count == 0)
        {
            return;
        }

        CopyAnnotatedButton.IsEnabled = false;
        RenderTargetBitmap? annotatedBitmap = null;
        try
        {
            annotatedBitmap = RenderAnnotatedBitmap();
            var copied = await _copyAsync(annotatedBitmap);
            if (copied)
            {
                annotatedBitmap = null;
            }
        }
        finally
        {
            annotatedBitmap?.Dispose();
            UpdateAnnotationButtons();
        }
    }

    private RenderTargetBitmap RenderAnnotatedBitmap()
    {
        if (_bitmap is null)
        {
            throw new InvalidOperationException("Cannot render annotations without a bitmap.");
        }

        var pixelSize = new PixelSize(Math.Max(1, (int)Math.Round(_bitmap.Size.Width)), Math.Max(1, (int)Math.Round(_bitmap.Size.Height)));
        var renderTarget = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        using var context = renderTarget.CreateDrawingContext();
        var imageRect = new Rect(0, 0, pixelSize.Width, pixelSize.Height);
        context.DrawImage(_bitmap, imageRect);
        foreach (var annotation in _annotations)
        {
            DrawAnnotation(context, annotation, new Size(pixelSize.Width, pixelSize.Height), imageRect);
        }

        return renderTarget;
    }

    private static void DrawAnnotation(DrawingContext context, ImageAnnotation annotation, Size imageSize, Rect imageBounds)
    {
        ImageAnnotationLayer.DrawAnnotation(context, annotation, imageSize, imageBounds);
    }

    private void ReplaceDisplayedBitmap(Bitmap bitmap)
    {
        ViewerImage.Source = null;
        _bitmap?.Dispose();
        _bitmap = bitmap;
        ViewerImage.Source = bitmap;
        FitToViewport();
    }

    private static Bitmap LoadBitmap(string imagePath)
    {
        using var stream = File.OpenRead(imagePath);
        return new Bitmap(stream);
    }

    private Size GetBitmapSize()
    {
        return _bitmap is null
            ? default
            : new Size(_bitmap.Size.Width, _bitmap.Size.Height);
    }

    private ImageAnnotationStroke? GetActiveStroke()
    {
        return _activeStrokePoints.Count == 0
            ? null
            : new ImageAnnotationStroke(_annotationColor, _annotationSize, _activeStrokePoints);
    }

    private Point MapViewportToImage(Point viewportPoint)
    {
        if (_bitmap is null)
        {
            return default;
        }

        var bounds = GetImageBounds();
        var x = Math.Clamp((viewportPoint.X - bounds.X) / bounds.Width * _bitmap.Size.Width, 0, _bitmap.Size.Width);
        var y = Math.Clamp((viewportPoint.Y - bounds.Y) / bounds.Height * _bitmap.Size.Height, 0, _bitmap.Size.Height);
        return new Point(x, y);
    }

    private void AddActiveAnnotationPoint(Point point)
    {
        if (_activeStrokePoints.Count > 0)
        {
            var previousPoint = _activeStrokePoints[^1];
            if (Math.Abs(previousPoint.X - point.X) < 0.5 && Math.Abs(previousPoint.Y - point.Y) < 0.5)
            {
                return;
            }
        }

        _activeStrokePoints.Add(point);
        _annotationLayer?.InvalidateVisual();
    }

    private void CommitActiveAnnotationStroke()
    {
        if (_activeStrokePoints.Count == 0)
        {
            return;
        }

        _annotations.Add(new ImageAnnotationStroke(_annotationColor, _annotationSize, _activeStrokePoints.ToArray()));
        _activeStrokePoints.Clear();
        _annotationLayer?.InvalidateVisual();
        UpdateAnnotationButtons();
    }

    private void SetActiveTool(ImageAnnotationTool tool)
    {
        if (_activeTool == tool)
        {
            return;
        }

        CancelTextAnnotationEdit(commit: true);
        _activeTool = tool;
        PenToggleButton.IsChecked = tool == ImageAnnotationTool.Pen;
        TextToggleButton.IsChecked = tool == ImageAnnotationTool.Text;
        ViewerViewport.Cursor = tool == ImageAnnotationTool.None
            ? null
            : new Cursor(StandardCursorType.Cross);
    }

    private bool TryActivateTextAnnotation(Point viewportPoint)
    {
        for (var i = _annotations.Count - 1; i >= 0; i--)
        {
            if (_annotations[i] is not ImageAnnotationText text)
            {
                continue;
            }

            if (!GetTextAnnotationViewportBounds(text).Contains(viewportPoint))
            {
                continue;
            }

            var origin = MapImageToViewport(text.Location);
            BeginTextAnnotationEdit(origin, text.Text, i, text.Location, text.Color, text.FontSize);
            return true;
        }

        return false;
    }

    private void BeginTextAnnotationEdit(Point viewportPoint, string text, int? existingIndex)
    {
        BeginTextAnnotationEdit(
            viewportPoint,
            text,
            existingIndex,
            MapViewportToImage(viewportPoint),
            _annotationColor,
            GetTextFontSize());
    }

    private void BeginTextAnnotationEdit(
        Point viewportPoint,
        string text,
        int? existingIndex,
        Point imageLocation,
        Color color,
        double fontSize)
    {
        CancelTextAnnotationEdit(commit: true);

        if (existingIndex is int index)
        {
            _editingOriginalTextAnnotation = _annotations[index] as ImageAnnotationText;
            _annotations.RemoveAt(index);
            _editingTextAnnotationIndex = index;
        }
        else
        {
            _editingTextAnnotationIndex = null;
            _editingOriginalTextAnnotation = null;
        }

        _pendingTextLocation = imageLocation;
        _pendingTextColor = color;
        _pendingTextFontSize = fontSize;
        _annotationSize = AnnotationSizeFromTextFontSize(fontSize);
        UpdateAnnotationSizeThumb();
        AnnotationTextBox.Text = text;
        ApplyAnnotationTextBoxColor(color);
        AnnotationTextBox.FontSize = fontSize * GetViewportImageScale();
        AnnotationTextEditor.Width = Math.Max(160, MeasureTextEditorWidth(text, AnnotationTextBox.FontSize));
        AnnotationTextEditor.IsVisible = true;
        UpdateActiveTextEditorPosition(viewportPoint);
        _annotationLayer?.InvalidateVisual();
        AnnotationTextBox.Focus();
        AnnotationTextBox.CaretIndex = AnnotationTextBox.Text?.Length ?? 0;
        UpdateAnnotationButtons();
    }

    private void OnAnnotationTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CancelTextAnnotationEdit(commit: true);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelTextAnnotationEdit(commit: false);
        }
    }

    private void OnAnnotationTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isPointerDownInAnnotationToolbar)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isPointerDownInAnnotationToolbar = false;
                if (AnnotationTextEditor.IsVisible)
                {
                    AnnotationTextBox.Focus();
                }
            }, DispatcherPriority.Input);
            return;
        }

        CancelTextAnnotationEdit(commit: true);
    }

    private void OnAnnotationTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!AnnotationTextEditor.IsVisible)
        {
            return;
        }

        AnnotationTextEditor.Width = Math.Max(160, MeasureTextEditorWidth(AnnotationTextBox.Text ?? string.Empty, AnnotationTextBox.FontSize));
    }

    private void OnAnnotationTextEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pendingTextLocation is not Point location
            || !e.GetCurrentPoint(AnnotationEditCanvas).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDraggingTextAnnotation = true;
        _textDragStartPoint = e.GetPosition(AnnotationEditCanvas);
        _textDragStartLocation = location;
        e.Pointer.Capture(AnnotationTextEditor);
        AnnotationTextBox.Focus();
        e.Handled = true;
    }

    private void OnAnnotationTextEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingTextAnnotation || _bitmap is null)
        {
            return;
        }

        var currentPoint = e.GetPosition(AnnotationEditCanvas);
        var delta = currentPoint - _textDragStartPoint;
        var imageBounds = GetImageBounds();
        var x = Math.Clamp(_textDragStartLocation.X + delta.X / imageBounds.Width * _bitmap.Size.Width, 0, _bitmap.Size.Width);
        var y = Math.Clamp(_textDragStartLocation.Y + delta.Y / imageBounds.Height * _bitmap.Size.Height, 0, _bitmap.Size.Height);
        _pendingTextLocation = new Point(x, y);
        UpdateActiveTextEditorPosition();
        e.Handled = true;
    }

    private void OnAnnotationTextEditorPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingTextAnnotation)
        {
            return;
        }

        _isDraggingTextAnnotation = false;
        e.Pointer.Capture(null);
        AnnotationTextBox.Focus();
        e.Handled = true;
    }

    private void CancelTextAnnotationEdit(bool commit)
    {
        if (!AnnotationTextEditor.IsVisible)
        {
            return;
        }

        var text = AnnotationTextBox.Text?.Trim();
        if (commit && _pendingTextLocation is Point location && !string.IsNullOrWhiteSpace(text))
        {
            var annotation = new ImageAnnotationText(text, location, _pendingTextColor, _pendingTextFontSize);
            if (_editingTextAnnotationIndex is int index && index >= 0 && index <= _annotations.Count)
            {
                _annotations.Insert(index, annotation);
            }
            else
            {
                _annotations.Add(annotation);
            }
        }
        else if (!commit && _editingOriginalTextAnnotation is not null)
        {
            var index = Math.Clamp(_editingTextAnnotationIndex ?? _annotations.Count, 0, _annotations.Count);
            _annotations.Insert(index, _editingOriginalTextAnnotation);
        }

        _pendingTextLocation = null;
        _editingTextAnnotationIndex = null;
        _editingOriginalTextAnnotation = null;
        _isDraggingTextAnnotation = false;
        AnnotationTextBox.Text = string.Empty;
        AnnotationTextEditor.IsVisible = false;
        _annotationLayer?.InvalidateVisual();
        UpdateAnnotationButtons();
    }

    private double GetTextFontSize() => Math.Clamp(_annotationSize * 4, 12, 96);

    private static double AnnotationSizeFromTextFontSize(double fontSize)
    {
        return Math.Clamp(fontSize / 4, MinAnnotationSize, MaxAnnotationSize);
    }

    private Point MapImageToViewport(Point imagePoint)
    {
        if (_bitmap is null)
        {
            return default;
        }

        var bounds = GetImageBounds();
        return new Point(
            bounds.X + imagePoint.X / Math.Max(1, _bitmap.Size.Width) * bounds.Width,
            bounds.Y + imagePoint.Y / Math.Max(1, _bitmap.Size.Height) * bounds.Height);
    }

    private double GetViewportImageScale()
    {
        if (_bitmap is null)
        {
            return 1;
        }

        return GetImageBounds().Width / Math.Max(1, _bitmap.Size.Width);
    }

    private Rect GetTextAnnotationViewportBounds(ImageAnnotationText text)
    {
        var origin = MapImageToViewport(text.Location);
        var fontSize = text.FontSize * GetViewportImageScale();
        var layout = ImageAnnotationLayer.CreateTextLayout(text.Text, text.Color, fontSize);
        return new Rect(origin, new Size(Math.Max(1, layout.WidthIncludingTrailingWhitespace), Math.Max(1, layout.Height)));
    }

    private void UpdateActiveTextEditorPosition()
    {
        if (_pendingTextLocation is Point location)
        {
            UpdateActiveTextEditorPosition(MapImageToViewport(location));
        }
    }

    private void UpdateActiveTextEditorPosition(Point viewportPoint)
    {
        if (AnnotationTextEditor.IsVisible)
        {
            AnnotationTextBox.FontSize = _pendingTextFontSize * GetViewportImageScale();
            AnnotationTextEditor.Width = Math.Max(
                160,
                MeasureTextEditorWidth(AnnotationTextBox.Text ?? string.Empty, AnnotationTextBox.FontSize));
        }

        Canvas.SetLeft(AnnotationTextEditor, viewportPoint.X);
        Canvas.SetTop(AnnotationTextEditor, viewportPoint.Y);
    }

    private static double MeasureTextEditorWidth(string text, double fontSize)
    {
        var layout = ImageAnnotationLayer.CreateTextLayout(
            string.IsNullOrEmpty(text) ? "Text" : text,
            Colors.White,
            fontSize);
        return layout.WidthIncludingTrailingWhitespace + 8;
    }

    private void UpdateAnnotationSizeFromPoint(Point point)
    {
        var usableWidth = Math.Max(1, AnnotationSizeControl.Bounds.Width - AnnotationThumbSize);
        var x = Math.Clamp(point.X - AnnotationThumbSize / 2, 0, usableWidth);
        var ratio = x / usableWidth;
        _annotationSize = MinAnnotationSize + ratio * (MaxAnnotationSize - MinAnnotationSize);
        UpdateAnnotationSizeThumb();
        UpdateActiveTextSizeFromAnnotationSize();
    }

    private void UpdateActiveTextSizeFromAnnotationSize()
    {
        if (!AnnotationTextEditor.IsVisible || _activeTool != ImageAnnotationTool.Text)
        {
            return;
        }

        _pendingTextFontSize = GetTextFontSize();
        UpdateActiveTextEditorPosition();
    }

    private void UpdateActiveTextColorFromAnnotationColor()
    {
        if (!AnnotationTextEditor.IsVisible || _activeTool != ImageAnnotationTool.Text)
        {
            return;
        }

        _pendingTextColor = _annotationColor;
        ApplyAnnotationTextBoxColor(_pendingTextColor);
        _annotationLayer?.InvalidateVisual();
    }

    private void ApplyAnnotationTextBoxColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        AnnotationTextBox.Foreground = brush;
        AnnotationTextBox.CaretBrush = brush;
    }

    private static Color ResolveAnnotationButtonColor(Button button)
    {
        if (button.Tag is string colorText)
        {
            return Color.Parse(colorText);
        }

        return button.Background is ISolidColorBrush brush
            ? brush.Color
            : DefaultAnnotationColor;
    }

    private void UpdateAnnotationSizeThumb()
    {
        var usableWidth = Math.Max(1, AnnotationSizeControl.Bounds.Width - AnnotationThumbSize);
        var ratio = Math.Clamp((_annotationSize - MinAnnotationSize) / (MaxAnnotationSize - MinAnnotationSize), 0, 1);
        Canvas.SetLeft(AnnotationSizeThumb, ratio * usableWidth);
    }

    private void UpdateAnnotationButtons()
    {
        var hasAnnotations = _annotations.Count > 0;
        UndoAnnotationButton.IsEnabled = hasAnnotations;
        SaveCopyButton.IsEnabled = hasAnnotations && _saveAsync is not null;
        CopyAnnotatedButton.IsEnabled = hasAnnotations && _copyAsync is not null;
        OverwriteButton.IsEnabled = hasAnnotations && _saveAsync is not null;
    }
}

public readonly record struct ImageViewerSaveResult(bool Success, string? ImagePath);

internal enum ImageAnnotationTool
{
    None,
    Pen,
    Text
}
