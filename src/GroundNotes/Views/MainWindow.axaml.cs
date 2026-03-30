using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class MainWindow : Window
{
    private IWindowLayoutService? _windowLayoutService;
    private readonly MenuFlyout _editorContextFlyout = new();
    private readonly MarkdownColorizingTransformer _markdownColorizer = new();
    private readonly EditorHostController _editorHost;
    private readonly WindowChromeController _windowChrome;
    private readonly TaskCompletionSource _openedTaskSource = new();
    private IEditorLayoutState? _editorLayoutState;
    private bool _hasAppliedInitialEditorLayout;
    private bool _isUpdatingEditorFromViewModel;
    private bool _isUpdatingViewModelFromEditor;
    private readonly SlashCommandPopupController _slashCommandPopup;
    private readonly ToolPopupController _titleSuggestionsPopup;
    private readonly ToolPopupController _tagSuggestionsPopup;
    private readonly DispatcherTimer _resizeHandleHoverTimer;
    private CancellationTokenSource? _sidebarAnimationCts;
    private bool _isResizingEditorCanvas;
    private double? _editorCanvasPreferredWidth;
    private double _editorCanvasResizeStartWidth;
    private Point _editorCanvasResizeStartPoint;
    private int _editorCanvasResizeDirection;
    private Control? _pendingResizeHandleHoverControl;
    private double? _lastAppliedEditorCanvasWidth;

    public MainWindow()
    {
        InitializeComponent();

        _windowChrome = new WindowChromeController(
            this,
            new WindowChromeController.Options
            {
                IdleCursor = null,
                IsInteractiveControl = IsPointerOverInteractiveControl,
                ShouldSuppressTitleBarDoubleTap = e => e.Source is Control control && control.FindAncestorOfType<Button>() is not null
            });
        _editorHost = new EditorHostController(EditorTextEditor, _markdownColorizer);
        _slashCommandPopup = new SlashCommandPopupController(
            EditorTextEditor,
            EditorBorder,
            SlashCommandPopup,
            SlashCommandPopupContent,
            SlashCommandListBox,
            SlashCommandHintText);
        _titleSuggestionsPopup = new ToolPopupController(TitleSuggestionsPopup, TitleSuggestionsPopupContent);
        _tagSuggestionsPopup = new ToolPopupController(TagSuggestionsPopup, TagSuggestionsPopupContent);
        _resizeHandleHoverTimer = new DispatcherTimer
        {
            Interval = ResizeHandleHoverDelay
        };
        _resizeHandleHoverTimer.Tick += OnResizeHandleHoverTimerTick;

        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;

        // Use Tunnel routing so corner resize takes priority over title-bar buttons.
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        EditorTextEditor.AddHandler(KeyDownEvent, OnEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        EditorTagsTextBox.AddHandler(KeyDownEvent, OnEditorTagsTextBoxKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        EditorTextEditor.PointerPressed += OnEditorPointerPressed;
        EditorTextEditor.ContextRequested += OnEditorContextRequested;
        EditorTagsTextBox.PropertyChanged += OnEditorTagsTextBoxPropertyChanged;
        EditorTagsTextBox.GotFocus += OnEditorTagsTextBoxGotFocus;
        EditorTagsTextBox.LostFocus += OnEditorTagsTextBoxLostFocus;
        EditorTextEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        EditorTextEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorTextViewScrollOffsetChanged;
        EditorTextEditor.TextArea.TextView.VisualLinesChanged += OnEditorTextViewVisualLinesChanged;
        EditorPanel.SizeChanged += OnEditorPanelSizeChanged;
        SlashCommandPopup.PlacementTarget = EditorBorder;
        EditorTextEditor.TextChanged += OnEditorTextChanged;
        RebuildEditorContextFlyout();

        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.FocusEditorRequested += OnFocusEditorRequested;
                SyncEditorText(vm.EditorBody);
            }

            try
            {
                await RestoreWindowLayoutAsync();
            }
            finally
            {
                _openedTaskSource.TrySetResult();
            }
        };

        Closing += (_, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.FocusEditorRequested -= OnFocusEditorRequested;
            }

            if (_editorLayoutState is not null)
            {
                _editorLayoutState.SettingsChanged -= OnEditorLayoutSettingsChanged;
            }

            EditorTagsTextBox.PropertyChanged -= OnEditorTagsTextBoxPropertyChanged;
            EditorTagsTextBox.GotFocus -= OnEditorTagsTextBoxGotFocus;
            EditorTagsTextBox.LostFocus -= OnEditorTagsTextBoxLostFocus;
            _resizeHandleHoverTimer.Stop();
            _resizeHandleHoverTimer.Tick -= OnResizeHandleHoverTimerTick;
            _editorHost.Dispose();
            SaveWindowLayout();
        };

        PositionChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _lastNormalX = e.Point.X;
                _lastNormalY = e.Point.Y;
            }

            _slashCommandPopup.SchedulePositionUpdate();
            _titleSuggestionsPopup.ScheduleRefresh();
            _tagSuggestionsPopup.ScheduleRefresh();
        };

        SizeChanged += (_, _) =>
        {
            _slashCommandPopup.SchedulePositionUpdate();
            _titleSuggestionsPopup.ScheduleRefresh();
            _tagSuggestionsPopup.ScheduleRefresh();
        };

    }

    public void SetWindowLayoutService(IWindowLayoutService windowLayoutService)
    {
        _windowLayoutService = windowLayoutService;
    }

    public void SetEditorLayoutState(IEditorLayoutState editorLayoutState)
    {
        if (_editorLayoutState is not null)
        {
            _editorLayoutState.SettingsChanged -= OnEditorLayoutSettingsChanged;
        }

        _editorLayoutState = editorLayoutState;
        _editorLayoutState.SettingsChanged += OnEditorLayoutSettingsChanged;
    }

    public async Task CompleteStartupInitializationAsync()
    {
        await _openedTaskSource.Task;

        if (DataContext is MainViewModel vm)
        {
            SyncEditorText(vm.EditorBody);
        }

        if (_editorLayoutState is not null)
        {
            _editorHost.ApplyInitialLayout(_editorLayoutState.CurrentSettings);
            _hasAppliedInitialEditorLayout = true;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_editorLayoutState is not null)
            {
                _editorHost.ApplyRuntimeLayout(_editorLayoutState.CurrentSettings);
            }

            UpdateEditorCanvasWidth();
        }, DispatcherPriority.Render);

        Opacity = 1;
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

        _editorCanvasPreferredWidth = NormalizeEditorCanvasPreferredWidth(layout.EditorCanvasWidth);

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsCalendarExpanded = layout.SidebarCalendarExpanded == true;
        }

        if (layout.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async Task RestoreWindowLayoutAsync()
    {
        if (_windowLayoutService is null) return;

        var layout = await _windowLayoutService.GetWindowLayoutAsync();
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
        if (_windowLayoutService is null) return;

        var layout = BuildWindowLayout();
        await _windowLayoutService.SaveWindowLayoutAsync(layout);
    }

    private void SaveWindowLayout()
    {
        if (_windowLayoutService is null) return;

        var layout = BuildWindowLayout();
        _windowLayoutService.SaveWindowLayoutSync(layout);
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

        var isCalendarExpanded = vm?.IsCalendarExpanded ?? false;
        return new WindowLayout(width, height, x, y, isMaximized, sidebarWidth, sidebarCollapsed, isCalendarExpanded, _editorCanvasPreferredWidth);
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
    private const int SidebarAnimationDurationMs = 140;
    private static readonly TimeSpan ResizeHandleHoverDelay = TimeSpan.FromMilliseconds(150);
    private const double EditorCanvasMinWidth = 520;
    private const double EditorCanvasResetThreshold = 12;
    private double _sidebarWidthBeforeCollapse = 300;

    private ColumnDefinition SidebarCol => ContentGrid.ColumnDefinitions[0];
    private ColumnDefinition SplitterCol => ContentGrid.ColumnDefinitions[1];

    private void OnResizeHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!TryBeginResizeHandleInteraction(sender, e, OnResizeHandleCaptureLost, out var control))
            return;

        _isResizingSidebar = true;
        _resizeStartPoint = e.GetPosition(this);
        _resizeStartWidth = SidebarCol.Width.Value;
    }

    private void OnResizeHandlePointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control || control.Classes.Contains("active"))
        {
            return;
        }

        ScheduleResizeHandleHoverIntent(control);
    }

    private void OnResizeHandlePointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        CancelResizeHandleHoverIntent(control);
        SetResizeHandleHoverIntent(control, isActive: false);
    }

    private void OnResizeHandleHoverTimerTick(object? sender, EventArgs e)
    {
        _resizeHandleHoverTimer.Stop();

        if (_pendingResizeHandleHoverControl is not { } control || control.Classes.Contains("active"))
        {
            return;
        }

        SetResizeHandleHoverIntent(control, isActive: true);
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
        EndResizeHandleInteraction(sender, e);
    }

    private void OnResizeHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isResizingSidebar = false;
        CleanupResizeHandleInteraction(sender, OnResizeHandleCaptureLost);
    }

    private void OnEditorResizeHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!TryBeginResizeHandleInteraction(sender, e, OnEditorResizeHandleCaptureLost, out var control))
        {
            return;
        }

        _isResizingEditorCanvas = true;
        _editorCanvasResizeStartPoint = e.GetPosition(this);
        _editorCanvasResizeStartWidth = GetEffectiveEditorCanvasWidth();
        _editorCanvasResizeDirection = ReferenceEquals(control, LeftEditorResizeHandle) ? -1 : 1;
    }

    private void OnEditorResizeHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizingEditorCanvas)
        {
            return;
        }

        var availableWidth = GetAvailableEditorCanvasWidth();
        if (availableWidth <= 0)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        var delta = (currentPosition.X - _editorCanvasResizeStartPoint.X) * _editorCanvasResizeDirection;
        var requestedWidth = _editorCanvasResizeStartWidth + (delta * 2);

        _editorCanvasPreferredWidth = NormalizeDraggedEditorCanvasWidth(requestedWidth, availableWidth);
        UpdateEditorCanvasWidth();
        e.Handled = true;
    }

    private void OnEditorResizeHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isResizingEditorCanvas)
        {
            return;
        }

        _isResizingEditorCanvas = false;
        EndResizeHandleInteraction(sender, e);
    }

    private void OnEditorResizeHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isResizingEditorCanvas = false;
        CleanupResizeHandleInteraction(sender, OnEditorResizeHandleCaptureLost);
    }

    private bool TryBeginResizeHandleInteraction(
        object? sender,
        PointerPressedEventArgs e,
        EventHandler<PointerCaptureLostEventArgs> captureLostHandler,
        out Control control)
    {
        if (sender is not Control senderControl || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            control = null!;
            return false;
        }

        control = senderControl;
        CancelResizeHandleHoverIntent(control);
        SetResizeHandleActive(control, isActive: true);
        e.Handled = true;
        control.PointerCaptureLost += captureLostHandler;
        e.Pointer.Capture(control);
        return true;
    }

    private void EndResizeHandleInteraction(object? sender, PointerReleasedEventArgs e)
    {
        CleanupResizeHandleInteraction(sender, null);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void CleanupResizeHandleInteraction(
        object? sender,
        EventHandler<PointerCaptureLostEventArgs>? captureLostHandler)
    {
        if (sender is not Control control)
        {
            return;
        }

        CancelResizeHandleHoverIntent(control);
        SetResizeHandleActive(control, isActive: false);

        if (captureLostHandler is not null)
        {
            control.PointerCaptureLost -= captureLostHandler;
        }
    }

    private void ScheduleResizeHandleHoverIntent(Control control)
    {
        CancelResizeHandleHoverIntent(control);
        _pendingResizeHandleHoverControl = control;
        _resizeHandleHoverTimer.Stop();
        _resizeHandleHoverTimer.Start();
    }

    private void CancelResizeHandleHoverIntent(Control control)
    {
        if (ReferenceEquals(_pendingResizeHandleHoverControl, control))
        {
            _pendingResizeHandleHoverControl = null;
            _resizeHandleHoverTimer.Stop();
        }
    }

    private static void SetResizeHandleActive(Control control, bool isActive)
    {
        control.Classes.Set("active", isActive);
    }

    private static void SetResizeHandleHoverIntent(Control control, bool isActive)
    {
        control.Classes.Set("hover-intent", isActive);
    }

    private void OnEditorPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateEditorCanvasWidth();
    }

    private void UpdateEditorCanvasWidth()
    {
        var effectiveWidth = GetEffectiveEditorCanvasWidth();
        if (effectiveWidth <= 0)
        {
            return;
        }

        if (_lastAppliedEditorCanvasWidth is { } lastAppliedWidth
            && Math.Abs(lastAppliedWidth - effectiveWidth) < 0.1)
        {
            return;
        }

        EditorCanvasHost.Width = effectiveWidth;
        _lastAppliedEditorCanvasWidth = effectiveWidth;
    }

    private double GetEffectiveEditorCanvasWidth()
    {
        var availableWidth = GetAvailableEditorCanvasWidth();
        if (availableWidth <= 0)
        {
            return 0;
        }

        return _editorCanvasPreferredWidth is { } preferredWidth
            ? Math.Min(preferredWidth, availableWidth)
            : availableWidth;
    }

    private double GetAvailableEditorCanvasWidth()
    {
        return Math.Max(0, EditorPanel.Bounds.Width);
    }

    private static double? NormalizeDraggedEditorCanvasWidth(double requestedWidth, double availableWidth)
    {
        var clampedWidth = Math.Clamp(requestedWidth, Math.Min(EditorCanvasMinWidth, availableWidth), availableWidth);
        if (availableWidth - clampedWidth <= EditorCanvasResetThreshold)
        {
            return null;
        }

        return clampedWidth;
    }

    private static double? NormalizeEditorCanvasPreferredWidth(double? width)
    {
        if (width is null || width <= 0)
        {
            return null;
        }

        return Math.Max(EditorCanvasMinWidth, width.Value);
    }

    private void OnFocusEditorRequested(object? sender, EventArgs e)
    {
        var moveCaretToEnd = e is FocusEditorRequestEventArgs fe && fe.MoveCaretToEndOfBody;
        TryFocusEditor(moveCaretToEnd);
    }

    /// <summary>
    /// Defers focus to after layout/render so sidebar ListBox / picker controls do not reclaim
    /// keyboard focus when pointer routing completes after a selection change.
    /// </summary>
    private void TryFocusEditor(bool moveCaretToEndOfBody)
    {
        void ApplyFocusAndCaret()
        {
            if (moveCaretToEndOfBody && EditorTextEditor.Document is not null)
            {
                var end = EditorTextEditor.Document.TextLength;
                EditorTextEditor.CaretOffset = end;
                EditorTextEditor.Select(end, 0);
            }

            Activate();
            EditorTextEditor.Focus();
        }

        Dispatcher.UIThread.Post(() =>
        {
            Dispatcher.UIThread.Post(ApplyFocusAndCaret, DispatcherPriority.Input);
        }, DispatcherPriority.Render);
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
                UpdateNotePickerHeight();
            }
            else
            {
                FocusEditorAfterNotePickerClosed(vm);
            }

            return;
        }

        if (e.PropertyName == nameof(MainViewModel.NotePickerResults))
        {
            UpdateNotePickerHeight();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.IsTitleSuggestionsOpen)
            or nameof(MainViewModel.TitleSuggestions)
            or nameof(MainViewModel.IsGeneratingTitleSuggestions))
        {
            _titleSuggestionsPopup.ScheduleRefresh(resetPlacement: e.PropertyName == nameof(MainViewModel.IsTitleSuggestionsOpen));
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.IsTagSuggestionsOpen)
            or nameof(MainViewModel.TagSuggestions)
            or nameof(MainViewModel.SelectedTagSuggestion))
        {
            _tagSuggestionsPopup.ScheduleRefresh(resetPlacement: e.PropertyName == nameof(MainViewModel.IsTagSuggestionsOpen));
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.AiPrompts)
            or nameof(MainViewModel.IsAiBusy)
            or nameof(MainViewModel.SelectedAiModel)
            or nameof(MainViewModel.IsAiEnabled))
        {
            RebuildEditorContextFlyout();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedThemeName))
        {
            _editorHost.RefreshVisualResources();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedCodeFontFamilyName)
            or nameof(MainViewModel.SelectedCodeFontVariantName))
        {
            _editorHost.RefreshTypographyResources();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.EditorBody))
        {
            SyncEditorText(vm.EditorBody);
            return;
        }

        if (e.PropertyName != nameof(MainViewModel.SidebarCollapsed))
            return;

        AnimateSidebar(vm.SidebarCollapsed);
    }

    private async void AnimateSidebar(bool collapse)
    {
        _sidebarAnimationCts?.Cancel();
        _sidebarAnimationCts?.Dispose();
        _sidebarAnimationCts = new CancellationTokenSource();
        var cancellationToken = _sidebarAnimationCts.Token;

        var startWidth = SidebarCol.Width.Value;
        var targetWidth = collapse ? 0 : Math.Max(_sidebarWidthBeforeCollapse, SidebarMinWidth);

        if (collapse && startWidth > 0)
        {
            _sidebarWidthBeforeCollapse = startWidth;
        }

        SidebarBorder.IsVisible = true;
        SplitterCol.Width = new GridLength(6, GridUnitType.Pixel);
        SidebarCol.MinWidth = 0;

        var startOpacity = SidebarBorder.Opacity;
        var targetOpacity = collapse ? 0 : 1;
        var startMargin = EditorPanel.Margin;
        var targetMargin = collapse
            ? new Thickness(14, 14, 14, 14)
            : new Thickness(8, 14, 14, 14);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / SidebarAnimationDurationMs, 0, 1);
                var eased = 1 - Math.Cos((progress * Math.PI) / 2);

                SidebarCol.Width = new GridLength(Lerp(startWidth, targetWidth, eased), GridUnitType.Pixel);
                SidebarBorder.Opacity = Lerp(startOpacity, targetOpacity, eased);
                EditorPanel.Margin = Lerp(startMargin, targetMargin, eased);
                UpdateEditorCanvasWidth();

                if (progress >= 1)
                {
                    break;
                }

                await Task.Delay(16, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        SidebarCol.Width = new GridLength(targetWidth, GridUnitType.Pixel);
        SidebarBorder.Opacity = targetOpacity;
        EditorPanel.Margin = targetMargin;
        UpdateEditorCanvasWidth();
        SidebarCol.MinWidth = collapse ? 0 : SidebarMinWidth;
        SplitterCol.Width = new GridLength(collapse ? 0 : 6, GridUnitType.Pixel);
        SidebarBorder.IsVisible = !collapse;
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + ((to - from) * progress);
    }

    private static Thickness Lerp(Thickness from, Thickness to, double progress)
    {
        return new Thickness(
            Lerp(from.Left, to.Left, progress),
            Lerp(from.Top, to.Top, progress),
            Lerp(from.Right, to.Right, progress),
            Lerp(from.Bottom, to.Bottom, progress));
    }

    private void UpdateNotePickerHeight()
    {
        if (DataContext is not MainViewModel vm || !vm.IsNotePickerOpen)
        {
            return;
        }

        // Defer to let the ListBox items update first
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                // Measure the border with the current width and unlimited height
                NotePickerBorder.InvalidateMeasure();
                NotePickerBorder.UpdateLayout();
                NotePickerBorder.Measure(new Size(NotePickerBorder.Bounds.Width, double.PositiveInfinity));

                // Clamp to window height minus some margin to prevent cutting off
                var maxAllowedHeight = Bounds.Height - 80;
                var targetHeight = Math.Min(maxAllowedHeight, NotePickerBorder.DesiredSize.Height);

                // Set the height to trigger the DoubleTransition
                NotePickerBorder.Height = targetHeight;
            }
            catch (InvalidOperationException)
            {
            }
        }, Avalonia.Threading.DispatcherPriority.Render);
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

    private void OnEditorTagsTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox)
        {
            return;
        }

        if (e.Property != TextBox.TextProperty && e.Property != TextBox.CaretIndexProperty)
        {
            return;
        }

        vm.UpdateTagSuggestions(textBox.CaretIndex);
        _tagSuggestionsPopup.ScheduleRefresh();
    }

    private void OnEditorTagsTextBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.UpdateTagSuggestions(EditorTagsTextBox.CaretIndex);
        _tagSuggestionsPopup.ScheduleRefresh(resetPlacement: true);
    }

    private void OnEditorTagsTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            if (EditorTagsTextBox.IsFocused || TagSuggestionsListBox.IsPointerOver)
            {
                return;
            }

            vm.DismissTagSuggestions();
        }, DispatcherPriority.Background);
    }

    private async void OnEditorTagsTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Down && vm.SelectNextTagSuggestion(1))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && vm.SelectNextTagSuggestion(-1))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab && vm.TryApplySelectedTagSuggestion(EditorTagsTextBox.CaretIndex, out var nextCaretIndex))
        {
            await ApplyTagSuggestionAsync(nextCaretIndex, commitAfterApply: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await vm.CommitEditorTagsAsync();
            EditorTagsTextBox.CaretIndex = EditorTagsTextBox.Text?.Length ?? 0;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && vm.IsTagSuggestionsOpen)
        {
            vm.DismissTagSuggestions();
            e.Handled = true;
        }
    }

    private async void OnTagSuggestionsListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!vm.TryApplySelectedTagSuggestion(EditorTagsTextBox.CaretIndex, out var nextCaretIndex))
        {
            return;
        }

        await ApplyTagSuggestionAsync(nextCaretIndex, commitAfterApply: true);
        e.Handled = true;
    }

    private async Task ApplyTagSuggestionAsync(int caretIndex, bool commitAfterApply)
    {
        var vm = DataContext as MainViewModel;
        EditorTagsTextBox.Text = vm?.EditorTags;
        EditorTagsTextBox.CaretIndex = Math.Min(caretIndex, EditorTagsTextBox.Text?.Length ?? 0);

        if (commitAfterApply && vm is not null)
        {
            await vm.CommitEditorTagsAsync();
            EditorTagsTextBox.Text = vm.EditorTags;
            EditorTagsTextBox.CaretIndex = EditorTagsTextBox.Text?.Length ?? 0;
        }

        EditorTagsTextBox.Focus();
        _tagSuggestionsPopup.ScheduleRefresh();
    }

    private void FocusEditorAfterNotePickerClosed(MainViewModel vm)
    {
        if (!vm.HasSelectedFolder)
        {
            Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Input);
        }
    }

    private void FocusNotesListBox()
    {
        Dispatcher.UIThread.Post(() => NotesListBox.Focus(), DispatcherPriority.Input);
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

    private void RebuildEditorContextFlyout()
    {
        _editorContextFlyout.Items.Clear();
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Cut", EditorTextEditor.CanCut, async (_, _) => await CutEditorSelectionAsync()));
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Copy", EditorTextEditor.CanCopy, async (_, _) => await CopyEditorSelectionAsync()));
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Paste", EditorTextEditor.CanPaste, (_, _) => EditorTextEditor.Paste()));

        if (DataContext is MainViewModel { IsAiEnabled: true })
        {
            _editorContextFlyout.Items.Add(new Separator());
            AddAiMenuSection();
            _editorContextFlyout.Items.Add(new Separator());
        }

        AddSettingsMenuItem();
    }

    private MenuItem CreateEditorMenuItem(string header, bool isEnabled, EventHandler<RoutedEventArgs> onClick)
    {
        var item = new MenuItem
        {
            Header = header,
            IsEnabled = isEnabled
        };
        item.Click += onClick;
        return item;
    }

    private void AddAiMenuSection()
    {
        if (DataContext is not MainViewModel vm)
        {
            _editorContextFlyout.Items.Add(new MenuItem
            {
                Header = "AI",
                IsEnabled = false
            });
            return;
        }

        _editorContextFlyout.Items.Add(new MenuItem
        {
            Header = "AI",
            IsEnabled = false
        });

        if (!vm.HasAiPrompts)
        {
            _editorContextFlyout.Items.Add(new MenuItem
            {
                Header = "No prompts found",
                IsEnabled = false
            });
        }
        else
        {
            foreach (var prompt in vm.AiPrompts)
            {
                var promptItem = new MenuItem
                {
                    Header = BuildAiMenuLabel(prompt, vm.SelectedAiModel),
                    IsEnabled = !vm.IsAiBusy
                };
                promptItem.Click += async (_, _) =>
                {
                    DismissEditorContextFlyout();
                    await ApplyAiPromptAsync(prompt);
                };
                _editorContextFlyout.Items.Add(promptItem);
            }
        }

        var reloadItem = new MenuItem
        {
            Header = "Reload Prompts",
            IsEnabled = !vm.IsAiBusy
        };
        reloadItem.Click += async (_, _) => await vm.ReloadAiPromptsCommand.ExecuteAsync(null);
        _editorContextFlyout.Items.Add(reloadItem);
    }

    private void AddSettingsMenuItem()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var settingsItem = new MenuItem
        {
            Header = "Settings..."
        };
        settingsItem.Click += async (_, _) => await vm.OpenSettingsCommand.ExecuteAsync(null);
        _editorContextFlyout.Items.Add(settingsItem);
    }

    private static string BuildAiMenuLabel(AiPromptDefinition prompt, string defaultModel)
    {
        var model = string.IsNullOrWhiteSpace(prompt.Model) ? defaultModel : prompt.Model;
        return string.IsNullOrWhiteSpace(model) ? prompt.Name : $"{prompt.Name} ({model})";
    }

    private async Task ApplyAiPromptAsync(AiPromptDefinition prompt)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var selectedText = EditorTextEditor.SelectedText;
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            await vm.RunAiPromptAsync(prompt, string.Empty);
            return;
        }

        var selectionStart = EditorTextEditor.SelectionStart;
        var selectionLength = EditorTextEditor.SelectionLength;
        try
        {
            var result = await vm.RunAiPromptAsync(prompt, selectedText);
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            var document = EditorTextEditor.Document;
            if (document is null)
            {
                return;
            }

            var start = Math.Clamp(selectionStart, 0, document.TextLength);
            var length = Math.Clamp(selectionLength, 0, document.TextLength - start);
            document.Replace(start, length, result);

            var caretPosition = start + result.Length;
            EditorTextEditor.Select(caretPosition, 0);
            EditorTextEditor.CaretOffset = caretPosition;

            vm.StatusMessage = $"{prompt.Name} applied.";
            EditorTextEditor.Focus();
        }
        finally
        {
            EditorTextEditor.Focus();
        }
    }

    private void DismissEditorContextFlyout()
    {
        _editorContextFlyout.Hide();
    }

    private void OnEditorContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        e.Handled = true;
        RebuildEditorContextFlyout();

        if (e.TryGetPosition(control, out _))
        {
            _editorContextFlyout.ShowAt(control, true);
            return;
        }

        _editorContextFlyout.ShowAt(control);
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var hadCurrentNote = vm.CurrentNote is not null;
        var updated = _editorHost.SyncToViewModel(() => vm.EditorBody, text => vm.EditorBody = text);
        _isUpdatingViewModelFromEditor = _editorHost.IsUpdatingViewModelFromEditor;
        if (!updated)
        {
            return;
        }

        if (!hadCurrentNote && vm.CurrentNote is not null)
        {
            Dispatcher.UIThread.Post(
                () => _slashCommandPopup.ScheduleRefresh(),
                DispatcherPriority.Background);
            return;
        }

        _slashCommandPopup.ScheduleRefresh();
    }

    private void SyncEditorText(string text)
    {
        var changed = _editorHost.SyncFromViewModel(text, appendSuffixWhenPossible: false, out var appendedOnly);
        _isUpdatingEditorFromViewModel = _editorHost.IsUpdatingEditorFromViewModel;
        if (!changed)
        {
            return;
        }

        if (!appendedOnly)
        {
            _editorHost.RefreshLayoutAfterDocumentReplace();
        }

        _slashCommandPopup.ScheduleRefresh();
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (IsOpenSettingsGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await vm.OpenSettingsCommand.ExecuteAsync(null);
            return;
        }

        if (IsShowShortcutsHelpGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await vm.ShowKeyboardShortcutsHelpCommand.ExecuteAsync(null);
            return;
        }

        if (IsToggleYamlEditorShortcut(e.Key, e.KeyModifiers) && vm.ToggleYamlFrontMatterVisibilityCommand.CanExecute(null))
        {
            e.Handled = true;
            await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
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

    internal static bool IsOpenSettingsGesture(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsOpenSettingsGesture(key, modifiers);

    internal static bool IsShowShortcutsHelpGesture(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsShowShortcutsHelpGesture(key, modifiers);

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

    private async Task CopyEditorSelectionAsync()
    {
        var selectedText = EditorTextEditor.SelectedText;
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
    }

    private async Task CutEditorSelectionAsync()
    {
        var selectedText = EditorTextEditor.SelectedText;
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
        EditorTextEditor.SelectedText = string.Empty;
    }

    private async void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (sender is not TextEditor textEditor)
        {
            return;
        }

        var vm = DataContext as MainViewModel;

        if (_slashCommandPopup.HandleKeyDown(e, ApplyEditorEdit))
        {
            return;
        }

        if (vm is not null && IsShowShortcutsHelpGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await vm.ShowKeyboardShortcutsHelpCommand.ExecuteAsync(null);
            return;
        }

        if (vm is not null && IsToggleYamlEditorShortcut(e.Key, e.KeyModifiers) && vm.ToggleYamlFrontMatterVisibilityCommand.CanExecute(null))
        {
            e.Handled = true;
            await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
            return;
        }

        if (IsUndoShortcut(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            textEditor.Undo();
            return;
        }

        if (IsRedoShortcut(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            textEditor.Redo();
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (IsToggleTaskShortcut(e.Key, e.KeyModifiers))
            {
                var toggleTaskEdit = MarkdownEditingCommands.ToggleTaskState(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength);
                if (toggleTaskEdit.Length != 0 || toggleTaskEdit.Replacement.Length != 0)
                {
                    e.Handled = true;
                    ApplyEditorEdit(toggleTaskEdit);
                    return;
                }
            }

            if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.D && vm is not null && !vm.IsNotePickerOpen)
            {
                e.Handled = true;
                await vm.DeleteCurrentNoteCommand.ExecuteAsync(null);
                return;
            }

            if (e.Key == Key.C)
            {
                e.Handled = true;
                await CopyEditorSelectionAsync();
                return;
            }

            if (e.Key == Key.X)
            {
                e.Handled = true;
                await CutEditorSelectionAsync();
                return;
            }

            if (e.Key == Key.B)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleWrap(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength, "**"));
                return;
            }

            if (e.Key == Key.I)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleWrap(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength, "*"));
                return;
            }

            if (e.Key == Key.K)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleWrap(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength, "`"));
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.InsertLineBelow(GetEditorText(), textEditor.CaretOffset));
                return;
            }
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (IsMoveLineShortcut(e.Key, e.KeyModifiers, out var moveDown))
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.MoveLines(
                    GetEditorText(),
                    textEditor.SelectionStart,
                    textEditor.SelectionLength,
                    moveDown));
                return;
            }

            if (e.Key == Key.D)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.DeleteCurrentLine(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength));
                return;
            }

            if (e.Key == Key.D7)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleTaskList(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength));
                return;
            }

            if (e.Key == Key.D8)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleBulletList(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength));
                return;
            }
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (e.Key == Key.D1)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleHeading(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength, 1));
                return;
            }

            if (e.Key == Key.D2)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleHeading(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength, 2));
                return;
            }

            if (e.Key == Key.D3)
            {
                e.Handled = true;
                ApplyEditorEdit(MarkdownEditingCommands.ToggleHeading(GetEditorText(), textEditor.SelectionStart, textEditor.SelectionLength, 3));
                return;
            }
        }

        if (e.Key != Key.Tab)
        {
            return;
        }

        e.Handled = true;

        var document = textEditor.Document;
        if (document is null)
        {
            return;
        }

        var text = document.Text;
        var selStart = textEditor.SelectionStart;
        var isUnindent = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        ApplyEditorEdit(MarkdownListEditingCommands.ChangeIndentation(
            text,
            selStart,
            textEditor.SelectionLength,
            Math.Max(1, textEditor.Options.IndentationSize),
            isUnindent));
    }

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e) => _windowChrome.OnWindowPointerMoved(e);

    private void OnWindowPointerExited(object? sender, PointerEventArgs e) => _windowChrome.OnWindowPointerExited();

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e) => _windowChrome.OnWindowPointerPressed(e);

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
            || visual.FindAncestorOfType<TextEditor>() is not null
            || visual.FindAncestorOfType<TextArea>() is not null
            || visual.FindAncestorOfType<ListBox>() is not null;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _windowChrome.OnTitleBarPointerPressed(e);

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        _windowChrome.OnTitleBarDoubleTapped(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => _windowChrome.OnMinimizeClick();

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e) => _windowChrome.OnMaximizeRestoreClick();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _windowChrome.OnCloseClick();

    private async void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox || textBox.DataContext is not NoteListItemViewModel noteItem)
        {
            return;
        }

        if (IsRenameTextBoxSubmitKey(e.Key))
        {
            e.Handled = true;
            await vm.CommitRenameAsync(noteItem);
            FocusNotesListBox();
        }
        else if (IsRenameTextBoxCancelKey(e.Key))
        {
            e.Handled = true;
            vm.CancelRename(noteItem);
            FocusNotesListBox();
        }
    }

    private async void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox || textBox.DataContext is not NoteListItemViewModel noteItem || !noteItem.IsRenaming)
        {
            return;
        }

        await vm.CommitRenameAsync(noteItem);
    }

    private async void OnTitleSuggestionsContextTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!AiSendShortcut.IsSendGesture(e.Key, e.KeyModifiers))
        {
            return;
        }

        e.Handled = true;
        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);
    }

    private string GetEditorText() => _editorHost.GetText();

    private void ApplyEditorEdit(MarkdownEditResult edit)
    {
        var document = EditorTextEditor.Document;
        if (document is null)
        {
            return;
        }

        var start = Math.Clamp(edit.Start, 0, document.TextLength);
        var length = Math.Clamp(edit.Length, 0, document.TextLength - start);

        document.Replace(start, length, edit.Replacement);

        var selectionStart = Math.Clamp(edit.SelectionStart, 0, document.TextLength);
        var selectionLength = Math.Clamp(edit.SelectionLength, 0, document.TextLength - selectionStart);
        EditorTextEditor.Select(selectionStart, selectionLength);
        EditorTextEditor.CaretOffset = selectionStart + selectionLength;
        EditorTextEditor.Focus();
        _slashCommandPopup.ScheduleRefresh();
    }

    private void OnSlashCommandListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        _slashCommandPopup.ApplySelectedCommand(ApplyEditorEdit);
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _slashCommandPopup.ScheduleRefresh(DispatcherPriority.Input);
    }

    private void OnEditorLayoutSettingsChanged(object? sender, EditorLayoutSettings settings)
    {
        if (!_hasAppliedInitialEditorLayout)
        {
            return;
        }

        _editorHost.ApplyRuntimeLayout(settings);
    }

    internal static bool IsRenameTextBoxSubmitKey(Key key) =>
        InputGestureHelper.IsRenameTextBoxSubmitKey(key);

    internal static bool IsRenameTextBoxCancelKey(Key key) =>
        InputGestureHelper.IsRenameTextBoxCancelKey(key);

    internal static bool IsUndoShortcut(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsUndoShortcut(key, modifiers);

    internal static bool IsRedoShortcut(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsRedoShortcut(key, modifiers);

    internal static bool IsToggleYamlEditorShortcut(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsToggleYamlEditorShortcut(key, modifiers);

    internal static bool IsToggleTaskShortcut(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsToggleTaskShortcut(key, modifiers);

    internal static bool IsMoveLineShortcut(Key key, KeyModifiers modifiers, out bool moveDown) =>
        InputGestureHelper.IsMoveLineShortcut(key, modifiers, out moveDown);

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        _slashCommandPopup.SchedulePositionUpdate();
    }

    private void OnEditorTextViewScrollOffsetChanged(object? sender, EventArgs e)
    {
        _slashCommandPopup.SchedulePositionUpdate();
    }

    private void OnEditorTextViewVisualLinesChanged(object? sender, EventArgs e)
    {
        _slashCommandPopup.SchedulePositionUpdate();
    }
}
