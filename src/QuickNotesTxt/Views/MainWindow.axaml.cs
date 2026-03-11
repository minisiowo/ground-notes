using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Views;

public partial class MainWindow : Window
{
    private const double WindowResizeBorderThickness = 6;
    private ISettingsService? _settingsService;
    private WindowEdge? _activeResizeEdge;

    public MainWindow()
    {
        InitializeComponent();

        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;
        PointerPressed += OnWindowPointerPressed;

        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PickFolderAsync = PickFolderAsync;
                vm.ConfirmDeleteAsync = ConfirmDeleteAsync;
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.FocusEditorRequested += OnFocusEditorRequested;
            }

            await RestoreWindowLayoutAsync();

            // Reveal the window after layout has been fully applied.
            Opacity = 1;
        };

        Closing += (_, e) =>
        {
            SaveWindowLayout();
        };

        PositionChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _lastNormalX = e.Point.X;
                _lastNormalY = e.Point.Y;
            }
        };
    }

    public void SetSettingsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void ApplyInitialWindowLayout(WindowLayout layout, bool isOnScreen)
    {
        if (isOnScreen)
        {
            Position = new PixelPoint((int)layout.X, (int)layout.Y);
        }

        Width = layout.Width;
        Height = layout.Height;

        _lastNormalWidth = layout.Width;
        _lastNormalHeight = layout.Height;
        _lastNormalX = layout.X;
        _lastNormalY = layout.Y;

        if (layout.SidebarWidth is > 0)
        {
            _sidebarWidthBeforeCollapse = layout.SidebarWidth.Value;
            if (layout.SidebarCollapsed != true)
            {
                SidebarCol.Width = new GridLength(layout.SidebarWidth.Value, GridUnitType.Pixel);
            }
        }

        if (layout.SidebarCollapsed == true && DataContext is MainViewModel vm)
        {
            vm.SidebarCollapsed = true;
        }

        if (layout.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async Task RestoreWindowLayoutAsync()
    {
        if (_settingsService is null) return;

        var layout = await _settingsService.GetWindowLayoutAsync();
        if (layout is null) return;

        var isOnScreen = IsLayoutOnAnyScreen(layout, Screens);
        ApplyInitialWindowLayout(layout, isOnScreen);
    }

    public static bool IsLayoutOnAnyScreen(WindowLayout layout, Screens screens)
    {
        var savedBounds = new PixelRect(
            (int)layout.X, (int)layout.Y,
            (int)layout.Width, (int)layout.Height);

        foreach (var screen in screens.All)
        {
            if (screen.WorkingArea.Intersects(savedBounds))
            {
                return true;
            }
        }

        return false;
    }

    private async Task SaveWindowLayoutAsync()
    {
        if (_settingsService is null) return;

        var layout = BuildWindowLayout();
        await _settingsService.SetWindowLayoutAsync(layout);
    }

    private void SaveWindowLayout()
    {
        if (_settingsService is null) return;

        var layout = BuildWindowLayout();
        _settingsService.SetWindowLayoutSync(layout);
    }

    private WindowLayout BuildWindowLayout()
    {
        var isMaximized = WindowState == WindowState.Maximized;

        double width, height, x, y;

        if (isMaximized)
        {
            width = _lastNormalWidth ?? 1180;
            height = _lastNormalHeight ?? 760;
            x = _lastNormalX ?? Position.X;
            y = _lastNormalY ?? Position.Y;
        }
        else
        {
            width = Width;
            height = Height;
            x = Position.X;
            y = Position.Y;
        }

        var vm = DataContext as MainViewModel;
        var sidebarCollapsed = vm?.SidebarCollapsed ?? false;
        var sidebarWidth = sidebarCollapsed
            ? _sidebarWidthBeforeCollapse
            : SidebarCol.Width.Value;

        return new WindowLayout(width, height, x, y, isMaximized, sidebarWidth, sidebarCollapsed);
    }

    private double? _lastNormalWidth;
    private double? _lastNormalHeight;
    private double? _lastNormalX;
    private double? _lastNormalY;

    // ── Sidebar resize ──────────────────────────────────────
    private bool _isResizingSidebar;
    private Point _resizeStartPoint;
    private double _resizeStartWidth;
    private const double SidebarMinWidth = 200;
    private const double SidebarMaxWidth = 600;
    private double _sidebarWidthBeforeCollapse = 300;

    private ColumnDefinition SidebarCol => ContentGrid.ColumnDefinitions[0];
    private ColumnDefinition SplitterCol => ContentGrid.ColumnDefinitions[1];

    private void OnResizeHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isResizingSidebar = true;
        _resizeStartPoint = e.GetPosition(this);
        _resizeStartWidth = SidebarCol.Width.Value;
        e.Handled = true;
        ((Control)sender!).PointerCaptureLost += OnResizeHandleCaptureLost;
        e.Pointer.Capture((Control)sender!);
    }

    private void OnResizeHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizingSidebar)
            return;

        var currentPos = e.GetPosition(this);
        var delta = currentPos.X - _resizeStartPoint.X;
        var newWidth = _resizeStartWidth + delta;

        var maxWidth = Math.Min(SidebarMaxWidth, Bounds.Width * 0.5);
        newWidth = Math.Max(SidebarMinWidth, newWidth);
        newWidth = Math.Min(maxWidth, newWidth);

        SidebarCol.Width = new GridLength(newWidth, GridUnitType.Pixel);
        e.Handled = true;
    }

    private void OnResizeHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isResizingSidebar)
            return;

        _isResizingSidebar = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnResizeHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isResizingSidebar = false;
        if (sender is Control control)
            control.PointerCaptureLost -= OnResizeHandleCaptureLost;
    }

    private void OnFocusEditorRequested(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorTextBox.Focus(), Avalonia.Threading.DispatcherPriority.Input);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SidebarCollapsed))
            return;

        if (DataContext is not MainViewModel vm)
            return;

        if (vm.SidebarCollapsed)
        {
            _sidebarWidthBeforeCollapse = SidebarCol.Width.Value;
            SidebarCol.MinWidth = 0;
            SidebarCol.Width = new GridLength(0, GridUnitType.Pixel);
            SplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
            SidebarBorder.IsVisible = false;
            UpdateEditorMargins(collapsed: true);
        }
        else
        {
            SidebarCol.MinWidth = SidebarMinWidth;
            SidebarCol.Width = new GridLength(_sidebarWidthBeforeCollapse, GridUnitType.Pixel);
            SplitterCol.Width = new GridLength(6, GridUnitType.Pixel);
            SidebarBorder.IsVisible = true;
            UpdateEditorMargins(collapsed: false);
        }
    }

    private void UpdateEditorMargins(bool collapsed)
    {
        var left = collapsed ? 14.0 : 8.0;
        TitleTagsGrid.Margin = new Thickness(left, 12, 14, 10);
        EditorBorder.Margin = new Thickness(left, 0, 14, 14);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            var newState = (WindowState)(change.NewValue ?? WindowState.Normal);
            if (newState == WindowState.Normal)
            {
                // Schedule to capture after the layout pass when bounds are updated
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _lastNormalWidth = Width;
                    _lastNormalHeight = Height;
                    _lastNormalX = Position.X;
                    _lastNormalY = Position.Y;
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        if (change.Property == BoundsProperty && WindowState == WindowState.Normal)
        {
            _lastNormalWidth = Bounds.Width;
            _lastNormalHeight = Bounds.Height;
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Choose notes folder"
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private async Task<bool> ConfirmDeleteAsync(string noteName)
    {
        var dialog = new ConfirmDeleteWindow(noteName);
        return await dialog.ShowDialog<bool>(this);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (e.Key is Key.Add or Key.OemPlus)
        {
            e.Handled = true;
            if (hasShift)
            {
                await vm.IncreaseUiFontSizeCommand.ExecuteAsync(null);
            }
            else
            {
                await vm.IncreaseEditorFontSizeCommand.ExecuteAsync(null);
            }
        }
        else if (e.Key is Key.Subtract or Key.OemMinus)
        {
            e.Handled = true;
            if (hasShift)
            {
                await vm.DecreaseUiFontSizeCommand.ExecuteAsync(null);
            }
            else
            {
                await vm.DecreaseEditorFontSizeCommand.ExecuteAsync(null);
            }
        }
        else if (!hasShift && e.Key is Key.R)
        {
            e.Handled = true;
            await vm.ReloadCommand.ExecuteAsync(null);
        }
        else if (!hasShift && e.Key is Key.N)
        {
            e.Handled = true;
            await vm.NewNoteCommand.ExecuteAsync(null);
        }
    }

    private async void OnEditorCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var selectedText = textBox.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await ClipboardTextService.SetTextAsync(topLevel.Clipboard, selectedText);
        e.Handled = true;
    }

    private async void OnEditorCuttingToClipboard(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var selectedText = textBox.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await ClipboardTextService.SetTextAsync(topLevel.Clipboard, selectedText);
        textBox.SelectedText = string.Empty;
        e.Handled = true;
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!CanResize || WindowState != WindowState.Normal)
        {
            ClearWindowResizeCursor();
            return;
        }

        if (e.Source is Visual visual && !ReferenceEquals(visual, this) && visual.FindAncestorOfType<Button>() is not null)
        {
            ClearWindowResizeCursor();
            return;
        }

        _activeResizeEdge = TryGetResizeEdge(e.GetPosition(this));
        Cursor = _activeResizeEdge switch
        {
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.NorthWest or WindowEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.NorthEast or WindowEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => null
        };
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        ClearWindowResizeCursor();
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanResize)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            return;
        }

        if (e.Source is Visual visual && !ReferenceEquals(visual, this) && visual.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        var edge = _activeResizeEdge ?? TryGetResizeEdge(e.GetPosition(this));
        if (edge is null)
        {
            return;
        }

        try
        {
            BeginResizeDrag(edge.Value, e);
            e.Handled = true;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void ClearWindowResizeCursor()
    {
        _activeResizeEdge = null;
        Cursor = null;
    }

    private WindowEdge? TryGetResizeEdge(Point point)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return null;
        }

        var onLeft = point.X >= 0 && point.X <= WindowResizeBorderThickness;
        var onRight = point.X <= Bounds.Width && point.X >= Bounds.Width - WindowResizeBorderThickness;
        var onTop = point.Y >= 0 && point.Y <= WindowResizeBorderThickness;
        var onBottom = point.Y <= Bounds.Height && point.Y >= Bounds.Height - WindowResizeBorderThickness;

        if (onTop && onLeft)
        {
            return WindowEdge.NorthWest;
        }

        if (onTop && onRight)
        {
            return WindowEdge.NorthEast;
        }

        if (onBottom && onLeft)
        {
            return WindowEdge.SouthWest;
        }

        if (onBottom && onRight)
        {
            return WindowEdge.SouthEast;
        }

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

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Control control && control.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        try
        {
            BeginMoveDrag(e);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!CanResize)
        {
            return;
        }

        if (e.Source is Control control && control.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        if (!CanResize)
        {
            return;
        }

        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox || textBox.DataContext is not NoteSummary noteSummary)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await vm.CommitRenameAsync(noteSummary);
            Focus();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CancelRename(noteSummary);
            Focus();
        }
    }

    private async void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox || textBox.DataContext is not NoteSummary noteSummary || !noteSummary.IsRenaming)
        {
            return;
        }

        await vm.CommitRenameAsync(noteSummary);
    }
}
