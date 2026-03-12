using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Views;

public partial class MainWindow : Window
{
    private const double WindowResizeBorderThickness = 6;
    private const double WindowCornerResizeThickness = 10;
    private ISettingsService? _settingsService;
    private WindowEdge? _activeResizeEdge;

    public MainWindow()
    {
        InitializeComponent();

        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;

        // Use Tunnel routing so corner resize takes priority over title-bar buttons.
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Tunnel-route Tab on the editor so we intercept it before the TextBox
        // processes it (which would move focus or replace the selection).
        EditorTextBox.AddHandler(KeyDownEvent, OnEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);

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
        if (DataContext is not MainViewModel vm)
            return;

        if (e.PropertyName == nameof(MainViewModel.IsNotePickerOpen))
        {
            if (vm.IsNotePickerOpen)
            {
                FocusNotePickerSearchTextBox();
            }
            else
            {
                FocusEditorAfterNotePickerClosed(vm);
            }

            return;
        }

        if (e.PropertyName != nameof(MainViewModel.SidebarCollapsed))
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

    private void FocusNotePickerSearchTextBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            NotePickerSearchTextBox.Focus();
            NotePickerSearchTextBox.SelectionStart = 0;
            NotePickerSearchTextBox.SelectionEnd = NotePickerSearchTextBox.Text?.Length ?? 0;
        }, DispatcherPriority.Input);
    }

    private void FocusEditorAfterNotePickerClosed(MainViewModel vm)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (vm.HasSelectedFolder)
            {
                EditorTextBox.Focus();
            }
            else
            {
                Focus();
            }
        }, DispatcherPriority.Input);
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
        else if (!hasShift && e.Key is Key.O)
        {
            e.Handled = true;
            vm.OpenNotePickerCommand.Execute(null);
        }
        else if (!hasShift && e.Key is Key.D && !vm.IsNotePickerOpen)
        {
            e.Handled = true;
            await vm.DeleteCurrentNoteCommand.ExecuteAsync(null);
        }
    }

    private void OnNotePickerSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            vm.MoveNotePickerSelectionCommand.Execute(1);
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            vm.MoveNotePickerSelectionCommand.Execute(-1);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            vm.AcceptNotePickerSelectionCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CloseNotePickerCommand.Execute(null);
        }
    }

    private void OnNotePickerListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.AcceptNotePickerSelectionCommand.Execute(null);
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

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = true;

        var text = textBox.Text ?? string.Empty;
        var selStart = textBox.SelectionStart;
        var selEnd = textBox.SelectionEnd;
        var hasSelection = selStart != selEnd;
        var isUnindent = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (!hasSelection && !isUnindent)
        {
            // No selection, plain Tab: insert 4 spaces at caret.
            var caretIndex = textBox.CaretIndex;
            textBox.Text = text.Insert(caretIndex, "    ");
            textBox.CaretIndex = caretIndex + 4;
            return;
        }

        // Ensure selStart <= selEnd.
        if (selStart > selEnd)
        {
            (selStart, selEnd) = (selEnd, selStart);
        }

        // Find the start of the first selected line and end of the last selected line.
        var lineStart = text.LastIndexOf('\n', Math.Max(selStart - 1, 0));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var lineEnd = selEnd < text.Length ? text.IndexOf('\n', selEnd) : -1;
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var block = text[lineStart..lineEnd];
        var lines = block.Split('\n');
        var totalDelta = 0;
        var firstLineDelta = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (isUnindent)
            {
                // Remove up to 4 leading spaces.
                var removed = 0;
                while (removed < 4 && removed < lines[i].Length && lines[i][removed] == ' ')
                {
                    removed++;
                }

                if (removed > 0)
                {
                    lines[i] = lines[i][removed..];
                    totalDelta -= removed;
                    if (i == 0)
                    {
                        firstLineDelta = -removed;
                    }
                }
            }
            else
            {
                lines[i] = "    " + lines[i];
                totalDelta += 4;
                if (i == 0)
                {
                    firstLineDelta = 4;
                }
            }
        }

        var newBlock = string.Join('\n', lines);

        // Replace the affected block via SelectionStart/SelectedText so that
        // the TextBox handles the edit internally, keeping undo state cleaner.
        textBox.SelectionStart = lineStart;
        textBox.SelectionEnd = lineEnd;
        textBox.SelectedText = newBlock;

        // Restore the user's logical selection after the binding roundtrip.
        var newSelStart = Math.Max(lineStart, selStart + firstLineDelta);
        var newSelEnd = Math.Max(newSelStart, selEnd + totalDelta);
        Dispatcher.UIThread.Post(() =>
        {
            textBox.SelectionStart = newSelStart;
            textBox.SelectionEnd = newSelEnd;
            textBox.CaretIndex = newSelEnd;
        }, DispatcherPriority.Render);
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!CanResize || WindowState != WindowState.Normal)
        {
            ClearWindowResizeCursor();
            return;
        }

        var edge = TryGetResizeEdge(e.GetPosition(this));
        var isCorner = edge is WindowEdge.NorthWest or WindowEdge.NorthEast
                            or WindowEdge.SouthWest or WindowEdge.SouthEast;

        // Allow interactive controls to work normally unless we are in a corner resize zone.
        if (!isCorner && IsPointerOverInteractiveControl(e))
        {
            ClearWindowResizeCursor();
            return;
        }

        _activeResizeEdge = edge;
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

        var edge = _activeResizeEdge ?? TryGetResizeEdge(e.GetPosition(this));
        if (edge is null)
        {
            return;
        }

        var isCorner = edge is WindowEdge.NorthWest or WindowEdge.NorthEast
                            or WindowEdge.SouthWest or WindowEdge.SouthEast;

        // Let interactive controls handle the click unless we are in a corner resize zone.
        if (!isCorner && IsPointerOverInteractiveControl(e))
        {
            return;
        }

        try
        {
            e.Handled = true;
            BeginResizeDrag(edge.Value, e);
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

    /// <summary>
    /// Returns <c>true</c> when the pointer event originates from an interactive
    /// control (button, combo box, text box, list box, or an open popup/dropdown)
    /// that should receive input instead of triggering a window resize.
    /// </summary>
    private bool IsPointerOverInteractiveControl(PointerEventArgs e)
    {
        if (e.Source is not Visual visual || ReferenceEquals(visual, this))
        {
            return false;
        }

        // Elements inside an open ComboBox dropdown live under a PopupRoot,
        // which is a separate visual tree root — not a child of this Window.
        var root = visual.GetVisualRoot();
        if (root is not null && root != this)
        {
            return true;
        }

        return visual.FindAncestorOfType<ComboBox>() is not null
            || visual.FindAncestorOfType<Button>() is not null
            || visual.FindAncestorOfType<TextBox>() is not null
            || visual.FindAncestorOfType<ListBox>() is not null;
    }

    private WindowEdge? TryGetResizeEdge(Point point)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return null;
        }

        // Use a larger hit area for corners so they are easier to grab.
        var cornerLeft = point.X >= 0 && point.X <= WindowCornerResizeThickness;
        var cornerRight = point.X <= Bounds.Width && point.X >= Bounds.Width - WindowCornerResizeThickness;
        var cornerTop = point.Y >= 0 && point.Y <= WindowCornerResizeThickness;
        var cornerBottom = point.Y <= Bounds.Height && point.Y >= Bounds.Height - WindowCornerResizeThickness;

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
        var onRight = point.X <= Bounds.Width && point.X >= Bounds.Width - WindowResizeBorderThickness;
        var onTop = point.Y >= 0 && point.Y <= WindowResizeBorderThickness;
        var onBottom = point.Y <= Bounds.Height && point.Y >= Bounds.Height - WindowResizeBorderThickness;

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
