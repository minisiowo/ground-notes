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
    private readonly Dictionary<Guid, EditorHostController> _secondaryEditorHosts = [];
    private readonly Dictionary<Guid, TextEditor> _secondaryEditorControls = [];
    private readonly Dictionary<Guid, Border> _secondaryEditorBorders = [];
    private readonly Dictionary<Guid, Control> _secondaryPaneRoots = [];
    private readonly Dictionary<Guid, Control> _secondaryTitleAnchors = [];
    private readonly Dictionary<Guid, TextBox> _secondaryTagsTextBoxes = [];
    private readonly WindowChromeController _windowChrome;
    private readonly TaskCompletionSource _openedTaskSource = new();
    private IEditorLayoutState? _editorLayoutState;
    private bool _hasAppliedInitialEditorLayout;
    private bool _isUpdatingEditorFromViewModel;
    private bool _isUpdatingViewModelFromEditor;
    private SlashCommandPopupController _slashCommandPopup;
    private readonly ToolPopupController _titleSuggestionsPopup;
    private readonly ToolPopupController _tagSuggestionsPopup;
    private readonly DispatcherTimer _resizeHandleHoverTimer;
    private CancellationTokenSource? _sidebarAnimationCts;
    private bool _isResizingEditorCanvas;
    private bool _isResizingMultiPane;
    private double? _editorCanvasPreferredWidth;
    private double? _multiPaneEqualizedPaneWidth;
    private double _editorCanvasResizeStartWidth;
    private Point _editorCanvasResizeStartPoint;
    private int _editorCanvasResizeDirection = 1;
    private Control? _pendingResizeHandleHoverControl;
    private double? _lastAppliedEditorCanvasWidth;
    private TextEditor? _editorContextTarget;
    private Guid? _lastBoundSecondaryPaneId;
    private List<double> _paneSplitWeights = [];
    private List<double> _multiPaneResizeStartWeights = [];
    private int _multiPaneResizePaneIndex = -1;
    private double _multiPaneResizeDistributableWidth;
    private int _multiPaneResizeDirection = 1;
    private bool _isResizingSharedPaneWidth;

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
        EditorTextEditor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AttachWorkspaceBringIntoViewSuppression(EditorTextEditor);
        AttachWorkspaceBringIntoViewSuppression(EditorTextEditor.TextArea);
        AttachWorkspaceBringIntoViewSuppression(EditorTitleTextBox);
        AttachWorkspaceBringIntoViewSuppression(EditorTagsTextBox);
        EditorTagsTextBox.AddHandler(KeyDownEvent, OnEditorTagsTextBoxKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        EditorTextEditor.GotFocus += OnPrimaryEditorGotFocus;
        EditorTextEditor.ContextRequested += OnEditorContextRequested;
        EditorTagsTextBox.PropertyChanged += OnEditorTagsTextBoxPropertyChanged;
        EditorTagsTextBox.GotFocus += OnEditorTagsTextBoxGotFocus;
        EditorTagsTextBox.LostFocus += OnEditorTagsTextBoxLostFocus;
        EditorTextEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        EditorTextEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorTextViewScrollOffsetChanged;
        EditorTextEditor.TextArea.TextView.VisualLinesChanged += OnEditorTextViewVisualLinesChanged;
        ConfigureEditorFocusScrollSuppression(EditorTextEditor);
        EditorPanel.SizeChanged += OnEditorPanelSizeChanged;
        SlashCommandPopup.PlacementTarget = EditorBorder;
        EditorTextEditor.TextChanged += OnEditorTextChanged;
        UpdateWorkspaceHostMargin();
        UpdateActiveEditorBindings();
        RebuildEditorContextFlyout();

        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.FocusEditorRequested += OnFocusEditorRequested;
                vm.SecondaryPanes.CollectionChanged += OnSecondaryPanesCollectionChanged;
                foreach (var pane in vm.SecondaryPanes)
                {
                    pane.PropertyChanged += OnSecondaryPaneViewModelPropertyChanged;
                }
                SyncEditorText(vm.EditorBody);
                UpdateActiveEditorBindings();
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
                vm.SecondaryPanes.CollectionChanged -= OnSecondaryPanesCollectionChanged;
                foreach (var pane in vm.SecondaryPanes)
                {
                    pane.PropertyChanged -= OnSecondaryPaneViewModelPropertyChanged;
                }
            }

            if (_editorLayoutState is not null)
            {
                _editorLayoutState.SettingsChanged -= OnEditorLayoutSettingsChanged;
            }

            EditorTagsTextBox.PropertyChanged -= OnEditorTagsTextBoxPropertyChanged;
            EditorTagsTextBox.GotFocus -= OnEditorTagsTextBoxGotFocus;
            EditorTagsTextBox.LostFocus -= OnEditorTagsTextBoxLostFocus;
            DetachWorkspaceBringIntoViewSuppression(EditorTextEditor);
            DetachWorkspaceBringIntoViewSuppression(EditorTextEditor.TextArea);
            DetachWorkspaceBringIntoViewSuppression(EditorTitleTextBox);
            DetachWorkspaceBringIntoViewSuppression(EditorTagsTextBox);
            EditorTextEditor.GotFocus -= OnPrimaryEditorGotFocus;
            _resizeHandleHoverTimer.Stop();
            _resizeHandleHoverTimer.Tick -= OnResizeHandleHoverTimerTick;
            _editorHost.Dispose();
            DisposeSecondaryEditorHosts();
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
            UpdateWorkspacePresentation();
            UpdateSplitEditorAvailability();
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

        UpdateWorkspaceHostMargin();

        _editorCanvasPreferredWidth = NormalizeEditorCanvasPreferredWidth(layout.EditorCanvasWidth);
        _paneSplitWeights = NormalizePaneSplitWeights(layout.PaneSplitWeights);
        _multiPaneEqualizedPaneWidth = NormalizeMultiPaneSharedWidth(layout.MultiPaneSharedWidth);

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
        return new WindowLayout(
            width,
            height,
            x,
            y,
            isMaximized,
            sidebarWidth,
            sidebarCollapsed,
            isCalendarExpanded,
            _editorCanvasPreferredWidth,
            _paneSplitWeights.Count == 2 ? _paneSplitWeights.ToList() : null,
            _multiPaneEqualizedPaneWidth);
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
    private const double SidebarSplitterWidth = 6;
    private const double EditorOuterGutter = 10;
    private const int SidebarAnimationDurationMs = 140;
    private static readonly TimeSpan ResizeHandleHoverDelay = TimeSpan.FromMilliseconds(150);
    private const double EditorCanvasMinWidth = 520;
    private const double EditorCanvasResetThreshold = 12;
    private const double TwoPaneMinWidth = 440;
    private const double MultiPaneMinWidth = 360;
    private const double EqualFitSafetyGap = 1;
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

        if (DataContext is MainViewModel vm && vm.HasSecondaryPane)
        {
            var paneIndex = GetPaneResizeIndex(control, vm);
            var resizeDirection = GetResizeDirection(control);
            var paneCount = Math.Max(1, vm.OpenPaneCount);
            var distributableWidth = paneCount == 2
                ? GetTwoPaneDistributableWidth(PaneWorkspaceScrollViewer.Bounds.Width)
                : GetSharedMultiPaneWidth(PaneWorkspaceScrollViewer.Bounds.Width, paneCount);
            if (paneIndex < 0 || distributableWidth <= 0)
            {
                CleanupResizeHandleInteraction(control, OnEditorResizeHandleCaptureLost);
                e.Pointer.Capture(null);
                return;
            }

            if (paneCount == 2)
            {
                EnsurePaneSplitWeights(paneCount, distributableWidth);
            }

            _isResizingMultiPane = true;
            _isResizingSharedPaneWidth = paneCount >= 3 || IsTwoPaneSharedResizeHandle(control, paneCount);
            if (paneCount == 2 && !_isResizingSharedPaneWidth && control.DataContext is EditorPaneViewModel && string.Equals(control.Tag as string, "Left", StringComparison.Ordinal))
            {
                paneIndex = 0;
                resizeDirection = 1;
            }

            _multiPaneResizePaneIndex = paneIndex;
            _multiPaneResizeDirection = resizeDirection;
            _multiPaneResizeDistributableWidth = distributableWidth;
            _multiPaneResizeStartWeights = paneCount == 2 && !_isResizingSharedPaneWidth ? _paneSplitWeights.ToList() : [];
            _editorCanvasResizeStartPoint = e.GetPosition(this);
            _editorCanvasResizeStartWidth = paneCount == 2 && !_isResizingSharedPaneWidth
                ? GetPaneWidthFromWeights(_multiPaneResizeStartWeights, paneIndex, distributableWidth)
                : GetResizeStartWidthForSharedPaneResize(control, vm, paneCount);
            return;
        }

        _isResizingEditorCanvas = true;
        _editorCanvasResizeStartPoint = e.GetPosition(this);
        _editorCanvasResizeStartWidth = GetEffectiveEditorCanvasWidth();
        _editorCanvasResizeDirection = GetResizeDirection(control);
    }

    private void OnEditorResizeHandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isResizingMultiPane)
        {
            if (_multiPaneResizePaneIndex < 0 || _multiPaneResizeDistributableWidth <= 0)
            {
                return;
            }

            var multiPaneCurrentPosition = e.GetPosition(this);
            var multiPaneDelta = multiPaneCurrentPosition.X - _editorCanvasResizeStartPoint.X;
            var multiPaneRequestedWidth = _editorCanvasResizeStartWidth + (multiPaneDelta * _multiPaneResizeDirection);
            if (_isResizingSharedPaneWidth)
            {
                _multiPaneEqualizedPaneWidth = NormalizeMultiPaneSharedWidth(multiPaneRequestedWidth);
            }
            else if (_multiPaneResizeStartWeights.Count == 2)
            {
                _paneSplitWeights = BuildWeightsForResizedPane(_multiPaneResizeStartWeights, _multiPaneResizePaneIndex, multiPaneRequestedWidth, _multiPaneResizeDistributableWidth);
            }

            UpdateWorkspacePresentation();
            e.Handled = true;
            return;
        }

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
        var delta = currentPosition.X - _editorCanvasResizeStartPoint.X;
        var requestedWidth = _editorCanvasResizeStartWidth + ((delta * _editorCanvasResizeDirection) * 2);

        _editorCanvasPreferredWidth = NormalizeDraggedEditorCanvasWidth(requestedWidth, availableWidth);
        _multiPaneEqualizedPaneWidth = null;
        UpdateEditorCanvasWidth();
        e.Handled = true;
    }

    private void OnEditorResizeHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isResizingMultiPane)
        {
            _isResizingMultiPane = false;
            _isResizingSharedPaneWidth = false;
            _multiPaneResizePaneIndex = -1;
            _multiPaneResizeDirection = 1;
            _multiPaneResizeStartWeights.Clear();
            EndResizeHandleInteraction(sender, e);
            return;
        }

        if (!_isResizingEditorCanvas)
        {
            return;
        }

        _isResizingEditorCanvas = false;
        EndResizeHandleInteraction(sender, e);
    }

    private void OnEditorResizeHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isResizingMultiPane = false;
        _isResizingSharedPaneWidth = false;
        _multiPaneResizePaneIndex = -1;
        _multiPaneResizeDirection = 1;
        _multiPaneResizeStartWeights.Clear();
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

    private static int GetResizeDirection(Control control)
    {
        return string.Equals(control.Tag as string, "Left", StringComparison.Ordinal)
            ? -1
            : 1;
    }

    private static bool IsTwoPaneSharedResizeHandle(Control control, int paneCount)
    {
        if (paneCount != 2)
        {
            return false;
        }

        var isLeftHandle = string.Equals(control.Tag as string, "Left", StringComparison.Ordinal);
        return (control.DataContext is EditorPaneViewModel) != isLeftHandle;
    }

    private double GetResizeStartWidthForSharedPaneResize(Control control, MainViewModel vm, int paneCount)
    {
        if (paneCount >= 3)
        {
            return GetSharedMultiPaneWidth(PaneWorkspaceScrollViewer.Bounds.Width, paneCount);
        }

        if (control.DataContext is EditorPaneViewModel pane
            && _secondaryPaneRoots.TryGetValue(pane.Id, out var secondaryRoot)
            && secondaryRoot.Width > 0)
        {
            return secondaryRoot.Width;
        }

        return PrimaryPaneRoot.Width > 0 ? PrimaryPaneRoot.Width : GetTwoPaneDistributableWidth(PaneWorkspaceScrollViewer.Bounds.Width) / 2;
    }

    private static void SetResizeHandleHoverIntent(Control control, bool isActive)
    {
        control.Classes.Set("hover-intent", isActive);
    }

    private void OnEditorPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateEditorCanvasWidth();
        UpdateWorkspacePresentation();
        UpdateSplitEditorAvailability();
    }

    private void UpdateSplitEditorAvailability()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.SetSplitEditorAvailability(true);
    }

    private void UpdateEditorCanvasWidth()
    {
        UpdateWorkspacePresentation();
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

    private void UpdateWorkspacePresentation()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        UpdateWorkspaceHostMargin();
        var paneCount = Math.Max(1, vm.OpenPaneCount);
        var hasSecondaryPane = paneCount > 1;
        PaneWorkspaceContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        PaneWorkspaceScrollViewer.HorizontalScrollBarVisibility = hasSecondaryPane
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;

        var viewportWidth = PaneWorkspaceScrollViewer.Bounds.Width;
        if (viewportWidth <= 0)
        {
            return;
        }

        if (!hasSecondaryPane)
        {
            _paneSplitWeights.Clear();
            _multiPaneEqualizedPaneWidth = null;
            PrimaryPaneRoot.Width = viewportWidth;
            foreach (var paneRoot in _secondaryPaneRoots.Values)
            {
                paneRoot.Width = double.NaN;
            }

            return;
        }

        List<double> paneWidths;
        if (paneCount == 2)
        {
            _multiPaneEqualizedPaneWidth = null;
            var distributableWidth = GetTwoPaneDistributableWidth(viewportWidth);
            EnsurePaneSplitWeights(paneCount, distributableWidth);
            paneWidths = GetPaneWidths(distributableWidth, paneCount);
        }
        else
        {
            _paneSplitWeights.Clear();
            var sharedWidth = GetSharedMultiPaneWidth(viewportWidth, paneCount);
            paneWidths = Enumerable.Repeat(sharedWidth, paneCount).ToList();
        }

        PrimaryPaneRoot.Width = paneWidths[0];
        for (var index = 0; index < vm.SecondaryPanes.Count; index++)
        {
            var pane = vm.SecondaryPanes[index];
            if (_secondaryPaneRoots.TryGetValue(pane.Id, out var paneRoot))
            {
                paneRoot.Width = paneWidths[index + 1];
            }
        }
    }

    private double GetTwoPaneDistributableWidth(double viewportWidth)
    {
        var totalSpacing = PaneWorkspaceContent.Spacing;
        var viewportContentWidth = Math.Max(0, viewportWidth - totalSpacing - EqualFitSafetyGap);
        var minimumReadableWidth = TwoPaneMinWidth * 2;
        var preferredTotalWidth = _editorCanvasPreferredWidth is { } preferredWidth
            ? preferredWidth + Math.Max(TwoPaneMinWidth, viewportContentWidth - preferredWidth)
            : 0;
        return Math.Max(viewportContentWidth, Math.Max(minimumReadableWidth, preferredTotalWidth));
    }

    private double GetSharedMultiPaneWidth(double viewportWidth, int paneCount)
    {
        var totalSpacing = PaneWorkspaceContent.Spacing * Math.Max(0, paneCount - 1);
        var availableWidth = Math.Max(0, viewportWidth - totalSpacing - EqualFitSafetyGap);
        var equalFitWidth = Math.Max(MultiPaneMinWidth, Math.Floor(availableWidth / paneCount));
        var sharedWidth = _multiPaneEqualizedPaneWidth ?? equalFitWidth;
        return Math.Max(MultiPaneMinWidth, sharedWidth);
    }

    private void EnsurePaneSplitWeights(int paneCount, double distributableWidth)
    {
        if (paneCount <= 1)
        {
            _paneSplitWeights.Clear();
            return;
        }

        if (_paneSplitWeights.Count == paneCount && Math.Abs(_paneSplitWeights.Sum() - 1d) < 0.001)
        {
            return;
        }

        if (_paneSplitWeights.Count == paneCount - 1 && paneCount == 2)
        {
            _paneSplitWeights = CreateTwoPaneWeightsFromSinglePane(distributableWidth);
            return;
        }

        if (_paneSplitWeights.Count != paneCount)
        {
            _paneSplitWeights = CreateEqualPaneSplitWeights(paneCount);
        }
    }

    private List<double> CreateTwoPaneWeightsFromSinglePane(double distributableWidth)
    {
        if (_editorCanvasPreferredWidth is not { } preferredWidth)
        {
            return CreateEqualPaneSplitWeights(2);
        }

        var minimumWidth = Math.Min(TwoPaneMinWidth, distributableWidth / 2);
        var primaryWidth = Math.Clamp(preferredWidth, minimumWidth, distributableWidth - minimumWidth);
        if (distributableWidth - primaryWidth < minimumWidth)
        {
            return CreateEqualPaneSplitWeights(2);
        }

        return [primaryWidth / distributableWidth, (distributableWidth - primaryWidth) / distributableWidth];
    }

    private static List<double> CreateEqualPaneSplitWeights(int paneCount)
    {
        var weight = 1d / paneCount;
        return Enumerable.Repeat(weight, paneCount).ToList();
    }

    private static List<double> NormalizePaneSplitWeights(IReadOnlyList<double>? weights)
    {
        if (weights is null || weights.Count == 0)
        {
            return [];
        }

        var normalized = weights.Where(weight => weight > 0).ToList();
        if (normalized.Count != weights.Count)
        {
            return [];
        }

        var sum = normalized.Sum();
        if (sum <= 0)
        {
            return [];
        }

        return normalized.Select(weight => weight / sum).ToList();
    }

    private List<double> GetPaneWidths(double distributableWidth, int paneCount)
    {
        var widths = new List<double>(paneCount);
        var remainingWidth = distributableWidth;
        var remainingWeight = 1d;

        for (var index = 0; index < paneCount; index++)
        {
            if (index == paneCount - 1)
            {
                widths.Add(remainingWidth);
                break;
            }

            var weight = _paneSplitWeights.ElementAtOrDefault(index);
            var width = remainingWeight <= 0
                ? distributableWidth / paneCount
                : distributableWidth * (weight / remainingWeight);
            width = Math.Max(TwoPaneMinWidth, width);
            widths.Add(width);
            remainingWidth -= width;
            remainingWeight -= weight;
        }

        return widths;
    }

    private double? GetActivePaneWidth(MainViewModel vm)
    {
        if (vm.ActiveSecondaryPane is { } activePane
            && _secondaryPaneRoots.TryGetValue(activePane.Id, out var secondaryRoot)
            && secondaryRoot.Width > 0)
        {
            return secondaryRoot.Width;
        }

        return PrimaryPaneRoot.Width > 0 ? PrimaryPaneRoot.Width : null;
    }

    private double? GetRepresentativePaneWidth(MainViewModel vm)
    {
        var activeWidth = GetActivePaneWidth(vm);
        if (activeWidth is > 0)
        {
            return activeWidth;
        }

        if (PrimaryPaneRoot.Width > 0)
        {
            return PrimaryPaneRoot.Width;
        }

        foreach (var pane in vm.SecondaryPanes)
        {
            if (_secondaryPaneRoots.TryGetValue(pane.Id, out var paneRoot) && paneRoot.Width > 0)
            {
                return paneRoot.Width;
            }
        }

        return null;
    }

    private void EqualizePaneWidthsToActivePane(MainViewModel vm)
    {
        var paneCount = Math.Max(1, vm.OpenPaneCount);
        if (paneCount <= 1)
        {
            return;
        }

        var viewportWidth = PaneWorkspaceScrollViewer.Bounds.Width;
        if (viewportWidth <= 0)
        {
            return;
        }

        if (paneCount == 2)
        {
            var activeWidth = GetActivePaneWidth(vm);
            if (activeWidth is null || activeWidth <= 0)
            {
                return;
            }

            _multiPaneEqualizedPaneWidth = null;
            _paneSplitWeights = CreateEqualPaneSplitWeights(2);
        }
        else
        {
            _paneSplitWeights.Clear();
            _multiPaneEqualizedPaneWidth = null;
        }

        UpdateWorkspacePresentation();
    }

    private int GetPaneResizeIndex(Control control, MainViewModel vm)
    {
        if (control.DataContext is EditorPaneViewModel pane)
        {
            return vm.SecondaryPanes.IndexOf(pane) + 1;
        }

        return 0;
    }

    private static double GetPaneWidthFromWeights(IReadOnlyList<double> weights, int paneIndex, double distributableWidth)
    {
        return weights[paneIndex] * distributableWidth;
    }

    private static List<double> BuildWeightsForResizedPane(IReadOnlyList<double> startWeights, int paneIndex, double requestedWidth, double distributableWidth)
    {
        var paneCount = startWeights.Count;
        var minimumWidth = Math.Min(TwoPaneMinWidth, distributableWidth / paneCount);
        var remainingPaneCount = paneCount - 1;
        var minRemainingWidth = minimumWidth * remainingPaneCount;
        var controlledWidth = Math.Clamp(requestedWidth, minimumWidth, distributableWidth - minRemainingWidth);
        var remainingWidth = distributableWidth - controlledWidth;

        var widths = Enumerable.Repeat(minimumWidth, paneCount).ToArray();
        widths[paneIndex] = controlledWidth;

        if (remainingPaneCount > 0)
        {
            var extraWidth = remainingWidth - (minimumWidth * remainingPaneCount);
            var basis = new List<double>(remainingPaneCount);
            for (var index = 0; index < paneCount; index++)
            {
                if (index == paneIndex)
                {
                    continue;
                }

                basis.Add(Math.Max(0, (startWeights[index] * distributableWidth) - minimumWidth));
            }

            var basisSum = basis.Sum();
            var basisIndex = 0;
            for (var index = 0; index < paneCount; index++)
            {
                if (index == paneIndex)
                {
                    continue;
                }

                widths[index] += basisSum <= 0
                    ? extraWidth / remainingPaneCount
                    : extraWidth * (basis[basisIndex] / basisSum);
                basisIndex++;
            }
        }

        return widths.Select(width => width / distributableWidth).ToList();
    }

    private TextEditor GetActiveTextEditor()
    {
        if (DataContext is MainViewModel { ActiveSecondaryPane: { } activePane }
            && _secondaryEditorControls.TryGetValue(activePane.Id, out var editor))
        {
            return editor;
        }

        return EditorTextEditor;
    }

    private EditorHostController GetEditorHost(TextEditor editor)
    {
        if (ReferenceEquals(editor, EditorTextEditor))
        {
            return _editorHost;
        }

        foreach (var pair in _secondaryEditorControls)
        {
            if (ReferenceEquals(pair.Value, editor)
                && _secondaryEditorHosts.TryGetValue(pair.Key, out var host))
            {
                return host;
            }
        }

        return _editorHost;
    }

    private Border GetActiveEditorBorder()
    {
        if (DataContext is MainViewModel { ActiveSecondaryPane: { } activePane }
            && _secondaryEditorBorders.TryGetValue(activePane.Id, out var border))
        {
            return border;
        }

        return EditorBorder;
    }

    private Control GetActiveTitleAnchor()
    {
        if (DataContext is MainViewModel { ActiveSecondaryPane: { } activePane }
            && _secondaryTitleAnchors.TryGetValue(activePane.Id, out var anchor))
        {
            return anchor;
        }

        return EditorTitleAnchor;
    }

    private TextBox GetActiveTagsTextBox()
    {
        if (DataContext is MainViewModel { ActiveSecondaryPane: { } activePane }
            && _secondaryTagsTextBoxes.TryGetValue(activePane.Id, out var textBox))
        {
            return textBox;
        }

        return EditorTagsTextBox;
    }

    private void UpdateActiveEditorBindings()
    {
        var activePaneId = (DataContext as MainViewModel)?.ActiveSecondaryPane?.Id;
        var targetEditor = GetActiveTextEditor();
        var targetBorder = GetActiveEditorBorder();
        var targetTitleAnchor = GetActiveTitleAnchor();
        var targetTagsTextBox = GetActiveTagsTextBox();

        _editorContextTarget = targetEditor;
        TitleSuggestionsPopup.PlacementTarget = targetTitleAnchor;
        TagSuggestionsPopup.PlacementTarget = targetTagsTextBox;
        SlashCommandPopup.PlacementTarget = targetBorder;

        if (_lastBoundSecondaryPaneId == activePaneId)
        {
            return;
        }

        _lastBoundSecondaryPaneId = activePaneId;
        _slashCommandPopup = new SlashCommandPopupController(
            targetEditor,
            targetBorder,
            SlashCommandPopup,
            SlashCommandPopupContent,
            SlashCommandListBox,
            SlashCommandHintText);
    }

    private double GetEffectiveEditorCanvasWidth()
    {
        var availableWidth = GetAvailableEditorCanvasWidth();
        if (availableWidth <= 0)
        {
            return 0;
        }

        if (DataContext is MainViewModel vm && vm.HasSecondaryPane)
        {
            return availableWidth;
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

    private static double? NormalizeMultiPaneSharedWidth(double? width)
    {
        if (width is null || width <= 0)
        {
            return null;
        }

        return Math.Max(MultiPaneMinWidth, width.Value);
    }

    private static bool IsPointerInsideEditorChrome(object? source)
    {
        return source is Visual visual
            && (visual.FindAncestorOfType<TextEditor>() is not null
                || visual.FindAncestorOfType<TextBox>() is not null
                || visual.FindAncestorOfType<Button>() is not null);
    }

    private static void ConfigureEditorFocusScrollSuppression(TextEditor editor)
    {
        ScrollViewer.SetBringIntoViewOnFocusChange(editor, false);
        ScrollViewer.SetBringIntoViewOnFocusChange(editor.TextArea, false);
    }

    private void AttachWorkspaceBringIntoViewSuppression(Control control)
    {
        control.AddHandler(Control.RequestBringIntoViewEvent, OnPaneWorkspaceDescendantRequestBringIntoView, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void DetachWorkspaceBringIntoViewSuppression(Control control)
    {
        control.RemoveHandler(Control.RequestBringIntoViewEvent, OnPaneWorkspaceDescendantRequestBringIntoView);
    }

    private void OnPaneWorkspaceDescendantRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
    {
        if (IsPaneWorkspaceDescendant(sender) || IsPaneWorkspaceDescendant(e.Source))
        {
            e.Handled = true;
        }
    }

    private bool IsPaneWorkspaceDescendant(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        Visual? current = visual;
        while (current is not null)
        {
            if (ReferenceEquals(current, PaneWorkspaceContent))
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    private void OnFocusEditorRequested(object? sender, EventArgs e)
    {
        var moveCaretToEnd = e is FocusEditorRequestEventArgs fe && fe.MoveCaretToEndOfBody;
        var paneId = e is FocusEditorRequestEventArgs request ? request.PaneId : null;
        TryFocusEditor(moveCaretToEnd, paneId);
    }

    /// <summary>
    /// Defers focus to after layout/render so sidebar ListBox / picker controls do not reclaim
    /// keyboard focus when pointer routing completes after a selection change.
    /// </summary>
    private void TryFocusEditor(bool moveCaretToEndOfBody, Guid? paneId)
    {
        void ApplyFocusAndCaret()
        {
            var editor = paneId is { } id && _secondaryEditorControls.TryGetValue(id, out var secondaryEditor)
                ? secondaryEditor
                : EditorTextEditor;
            if (moveCaretToEndOfBody && editor.Document is not null)
            {
                var end = editor.Document.TextLength;
                editor.CaretOffset = end;
                editor.Select(end, 0);
            }

            Activate();
            editor.Focus();
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

        if (e.PropertyName == nameof(MainViewModel.HasSecondaryPane))
        {
            UpdateWorkspacePresentation();
            UpdateEditorCanvasWidth();
            UpdateSplitEditorAvailability();
            UpdateActiveEditorBindings();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.ActiveSecondaryPane) or nameof(MainViewModel.IsPrimaryPaneActive))
        {
            SyncSidebarSelectionFromActivePane(vm);
            UpdateActiveEditorBindings();
            return;
        }

        if (e.PropertyName != nameof(MainViewModel.SidebarCollapsed))
            return;

        AnimateSidebar(vm.SidebarCollapsed);
        UpdateWorkspacePresentation();
    }

    private void OnSecondaryPaneViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not EditorPaneViewModel pane)
        {
            return;
        }

        if (e.PropertyName == nameof(EditorPaneViewModel.EditorBody))
        {
            SyncSecondaryEditorText(pane);
        }

    }

    private void OnSecondaryPanesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            UpdatePaneSplitWeightsForCollectionChange(vm, e);
        }

        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EditorPaneViewModel>())
            {
                item.PropertyChanged -= OnSecondaryPaneViewModelPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<EditorPaneViewModel>())
            {
                item.PropertyChanged += OnSecondaryPaneViewModelPropertyChanged;
            }
        }

        UpdateWorkspacePresentation();
        UpdateEditorCanvasWidth();
        }

    private void UpdatePaneSplitWeightsForCollectionChange(MainViewModel vm, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        var paneCount = Math.Max(1, vm.OpenPaneCount);
        var previousPaneCount = e.Action switch
        {
            System.Collections.Specialized.NotifyCollectionChangedAction.Add when e.NewItems is not null => Math.Max(1, paneCount - e.NewItems.Count),
            System.Collections.Specialized.NotifyCollectionChangedAction.Remove when e.OldItems is not null => paneCount + e.OldItems.Count,
            _ => paneCount
        };

        if (paneCount <= 1)
        {
            _paneSplitWeights.Clear();
            _multiPaneEqualizedPaneWidth = null;
            return;
        }

        if (paneCount >= 3)
        {
            _paneSplitWeights.Clear();

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                var preservedWidth = _multiPaneEqualizedPaneWidth;
                if (previousPaneCount == 2 || preservedWidth is null)
                {
                    preservedWidth = NormalizeMultiPaneSharedWidth(GetRepresentativePaneWidth(vm));
                }

                _multiPaneEqualizedPaneWidth = preservedWidth;
            }
            else if (_multiPaneEqualizedPaneWidth is null)
            {
                _multiPaneEqualizedPaneWidth = NormalizeMultiPaneSharedWidth(GetRepresentativePaneWidth(vm));
            }

            return;
        }

        var distributableWidth = GetTwoPaneDistributableWidth(PaneWorkspaceScrollViewer.Bounds.Width);

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
            && e.NewItems is not null
            && e.NewItems.Count > 0)
        {
            if (_paneSplitWeights.Count == 0 && paneCount == 2)
            {
                _paneSplitWeights = CreateTwoPaneWeightsFromSinglePane(distributableWidth);
                _multiPaneEqualizedPaneWidth = null;
                return;
            }

            EnsurePaneSplitWeights(Math.Max(1, paneCount - e.NewItems.Count), distributableWidth);
            var newWeight = 1d / paneCount;
            _paneSplitWeights = _paneSplitWeights.Select(weight => weight * (1 - newWeight)).ToList();
            var insertIndex = Math.Clamp((e.NewStartingIndex >= 0 ? e.NewStartingIndex : _paneSplitWeights.Count) + 1, 0, _paneSplitWeights.Count);
            for (var index = 0; index < e.NewItems.Count; index++)
            {
                _paneSplitWeights.Insert(insertIndex + index, newWeight);
            }

            _paneSplitWeights = NormalizePaneSplitWeights(_paneSplitWeights);
            _multiPaneEqualizedPaneWidth = null;
            return;
        }

        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove
            && e.OldItems is not null
            && e.OldItems.Count > 0
            && _paneSplitWeights.Count > 0)
        {
            var removeIndex = Math.Clamp((e.OldStartingIndex >= 0 ? e.OldStartingIndex : _paneSplitWeights.Count - 1) + 1, 0, _paneSplitWeights.Count - 1);
            for (var index = 0; index < e.OldItems.Count && removeIndex < _paneSplitWeights.Count; index++)
            {
                _paneSplitWeights.RemoveAt(removeIndex);
            }

            _paneSplitWeights = paneCount <= 1
                ? []
                : NormalizePaneSplitWeights(_paneSplitWeights);
            if (_paneSplitWeights.Count != paneCount)
            {
                _paneSplitWeights = CreateEqualPaneSplitWeights(paneCount);
            }

            _multiPaneEqualizedPaneWidth = null;

            return;
        }

        if (_paneSplitWeights.Count != paneCount)
        {
            _paneSplitWeights = CreateEqualPaneSplitWeights(paneCount);
        }

        _multiPaneEqualizedPaneWidth = null;
    }

    private void DisposeSecondaryEditorHosts()
    {
        foreach (var host in _secondaryEditorHosts.Values)
        {
            host.Dispose();
        }

        _secondaryEditorHosts.Clear();
        _secondaryEditorControls.Clear();
        _secondaryPaneRoots.Clear();
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
        SplitterCol.Width = new GridLength(SidebarSplitterWidth, GridUnitType.Pixel);
        SidebarCol.MinWidth = 0;
        UpdateWorkspaceHostMargin();

        var startOpacity = SidebarBorder.Opacity;
        var targetOpacity = collapse ? 0 : 1;

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
                UpdateWorkspaceHostMargin();
                UpdateWorkspacePresentation();
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
        UpdateWorkspaceHostMargin();
        UpdateWorkspacePresentation();
        UpdateEditorCanvasWidth();
        SidebarCol.MinWidth = collapse ? 0 : SidebarMinWidth;
        SplitterCol.Width = new GridLength(collapse ? 0 : SidebarSplitterWidth, GridUnitType.Pixel);
        SidebarBorder.IsVisible = !collapse;
        UpdateWorkspaceHostMargin();
        ScheduleSidebarLayoutRefresh();
    }

    private void UpdateWorkspaceHostMargin()
    {
        var leftGutter = Math.Max(0, EditorOuterGutter - SplitterCol.Width.Value);
        WorkspaceHost.Margin = new Thickness(leftGutter, EditorOuterGutter, EditorOuterGutter, EditorOuterGutter);
    }

    private void ScheduleSidebarLayoutRefresh()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateWorkspaceHostMargin();
                UpdateWorkspacePresentation();
                UpdateEditorCanvasWidth();
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Render);
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

        TagSuggestionsPopup.PlacementTarget = textBox;
        vm.UpdateTagSuggestions(textBox.CaretIndex);
        _tagSuggestionsPopup.ScheduleRefresh();
    }

    private void OnEditorTagsTextBoxGotFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox)
        {
            return;
        }

        TagSuggestionsPopup.PlacementTarget = textBox;
        vm.UpdateTagSuggestions(textBox.CaretIndex);
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

            var activeTextBox = GetActiveTagsTextBox();
            if (activeTextBox.IsFocused || TagSuggestionsListBox.IsPointerOver)
            {
                return;
            }

            vm.DismissTagSuggestions();
        }, DispatcherPriority.Background);
    }

    private async void OnEditorTagsTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextBox textBox)
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

        if (e.Key == Key.Tab && vm.TryApplySelectedTagSuggestion(textBox.CaretIndex, out var nextCaretIndex))
        {
            await ApplyTagSuggestionAsync(textBox, nextCaretIndex, commitAfterApply: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            await vm.CommitEditorTagsAsync();
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
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

        var activeTextBox = GetActiveTagsTextBox();
        if (!vm.TryApplySelectedTagSuggestion(activeTextBox.CaretIndex, out var nextCaretIndex))
        {
            return;
        }

        await ApplyTagSuggestionAsync(activeTextBox, nextCaretIndex, commitAfterApply: true);
        e.Handled = true;
    }

    private async Task ApplyTagSuggestionAsync(TextBox targetTextBox, int caretIndex, bool commitAfterApply)
    {
        var vm = DataContext as MainViewModel;
        targetTextBox.Text = vm?.GetActiveEditorTagsText();
        targetTextBox.CaretIndex = Math.Min(caretIndex, targetTextBox.Text?.Length ?? 0);

        if (commitAfterApply && vm is not null)
        {
            await vm.CommitEditorTagsAsync();
            targetTextBox.Text = vm.GetActiveEditorTagsText();
            targetTextBox.CaretIndex = targetTextBox.Text?.Length ?? 0;
        }

        targetTextBox.Focus();
        _tagSuggestionsPopup.ScheduleRefresh();
    }

    private void SetSecondaryPaneActive(EditorPaneViewModel? pane)
    {
        if (pane is null || DataContext is not MainViewModel vm)
        {
            return;
        }

        var needsBindingRefresh = !ReferenceEquals(vm.ActiveSecondaryPane, pane) || vm.IsPrimaryPaneActive;

        vm.ActivatePane(pane);
        SyncSidebarSelectionFromActivePane(vm);

        if (!needsBindingRefresh)
        {
            return;
        }

        Dispatcher.UIThread.Post(UpdateActiveEditorBindings, DispatcherPriority.Background);
    }

    private void OnPrimaryPaneRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ActivatePrimaryPane();
        }
    }

    private void ActivatePrimaryPane()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var needsBindingRefresh = !vm.IsPrimaryPaneActive;

        vm.ActivatePrimaryPane();
        SyncSidebarSelectionFromActivePane(vm);

        if (!needsBindingRefresh)
        {
            return;
        }

        Dispatcher.UIThread.Post(UpdateActiveEditorBindings, DispatcherPriority.Background);
    }

    private void SyncSidebarSelectionFromActivePane(MainViewModel vm)
    {
        var filePath = vm.ActiveSecondaryPane?.CurrentNote?.FilePath ?? vm.CurrentNote?.FilePath;
        var selectedItem = string.IsNullOrWhiteSpace(filePath)
            ? null
            : vm.VisibleNotes.FirstOrDefault(note => string.Equals(note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        NotesListBox.SelectedItem = selectedItem;
    }

    private void OnPrimaryPaneTitleGotFocus(object? sender, GotFocusEventArgs e)
    {
        ActivatePrimaryPane();
    }

    private void OnPrimaryPaneChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ActivatePrimaryPane();
    }

    private void OnPrimaryPaneTagsGotFocus(object? sender, GotFocusEventArgs e)
    {
        ActivatePrimaryPane();
    }

    private void OnPrimaryEditorGotFocus(object? sender, GotFocusEventArgs e)
    {
        ActivatePrimaryPane();
    }

    private void OnSecondaryPaneTitleGotFocus(object? sender, GotFocusEventArgs e)
    {
        SetSecondaryPaneActive((sender as StyledElement)?.DataContext as EditorPaneViewModel);
    }

    private void OnSecondaryPaneChromePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        SetSecondaryPaneActive((sender as StyledElement)?.DataContext as EditorPaneViewModel);
    }

    private void OnSecondaryPaneTagsGotFocus(object? sender, GotFocusEventArgs e)
    {
        SetSecondaryPaneActive((sender as StyledElement)?.DataContext as EditorPaneViewModel);
    }

    private void OnSecondaryPaneRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        SetSecondaryPaneActive((sender as StyledElement)?.DataContext as EditorPaneViewModel);
    }

    private void OnSecondaryEditorGotFocus(object? sender, GotFocusEventArgs e)
    {
        SetSecondaryPaneActive((sender as StyledElement)?.DataContext as EditorPaneViewModel);
    }

    private async void OnSecondaryGenerateTitleSuggestionsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if ((sender as StyledElement)?.DataContext is EditorPaneViewModel pane)
        {
            SetSecondaryPaneActive(pane);
            UpdateActiveEditorBindings();
        }

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);
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

    private async void OnNoteListItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: NoteListItemViewModel noteItem } border
            || DataContext is not MainViewModel vm)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            border.ContextMenu?.Open(border);
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            await vm.OpenSidebarNoteCommand.ExecuteAsync(noteItem);
            e.Handled = true;
            return;
        }

        await vm.OpenNoteInSplitCommand.ExecuteAsync(noteItem);
        e.Handled = true;
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
        var targetEditor = _editorContextTarget ?? GetActiveTextEditor();
        _editorContextFlyout.Items.Clear();
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Cut", targetEditor.CanCut, async (_, _) => await CutEditorSelectionAsync(targetEditor)));
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Copy", targetEditor.CanCopy, async (_, _) => await CopyEditorSelectionAsync(targetEditor)));
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Paste", targetEditor.CanPaste, (_, _) => targetEditor.Paste()));

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

        var targetEditor = _editorContextTarget ?? GetActiveTextEditor();
        var selectedText = targetEditor.SelectedText;
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            await vm.RunAiPromptAsync(prompt, string.Empty);
            return;
        }

        var selectionStart = targetEditor.SelectionStart;
        var selectionLength = targetEditor.SelectionLength;
        try
        {
            var result = await vm.RunAiPromptAsync(prompt, selectedText);
            if (string.IsNullOrWhiteSpace(result))
            {
                return;
            }

            var document = targetEditor.Document;
            if (document is null)
            {
                return;
            }

            var start = Math.Clamp(selectionStart, 0, document.TextLength);
            var length = Math.Clamp(selectionLength, 0, document.TextLength - start);
            document.Replace(start, length, result);

            var caretPosition = start + result.Length;
            targetEditor.Select(caretPosition, 0);
            targetEditor.CaretOffset = caretPosition;

            vm.StatusMessage = $"{prompt.Name} applied.";
            targetEditor.Focus();
        }
        finally
        {
            targetEditor.Focus();
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

        if (control is TextEditor editor)
        {
            _editorContextTarget = editor;
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

    private void OnSecondaryEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm || sender is not TextEditor editor || editor.DataContext is not EditorPaneViewModel pane)
        {
            return;
        }

        if (!_secondaryEditorHosts.TryGetValue(pane.Id, out var host))
        {
            return;
        }

        var updated = host.SyncToViewModel(() => pane.EditorBody, text => pane.EditorBody = text);
        if (!updated)
        {
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

    private void SyncSecondaryEditorText(EditorPaneViewModel pane)
    {
        if (!_secondaryEditorHosts.TryGetValue(pane.Id, out var host))
        {
            return;
        }

        var changed = host.SyncFromViewModel(pane.EditorBody, appendSuffixWhenPossible: false, out var appendedOnly);
        if (!changed)
        {
            return;
        }

        if (!appendedOnly)
        {
            host.RefreshLayoutAfterDocumentReplace();
        }
    }

    private void OnSecondaryEditorAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextEditor editor || editor.DataContext is not EditorPaneViewModel pane)
        {
            return;
        }

        var host = new EditorHostController(editor, new MarkdownColorizingTransformer());
        _secondaryEditorHosts[pane.Id] = host;
        _secondaryEditorControls[pane.Id] = editor;
        editor.AddHandler(KeyDownEvent, OnEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AttachWorkspaceBringIntoViewSuppression(editor);
        AttachWorkspaceBringIntoViewSuppression(editor.TextArea);
        editor.ContextRequested += OnEditorContextRequested;
        editor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        editor.TextArea.TextView.ScrollOffsetChanged += OnEditorTextViewScrollOffsetChanged;
        editor.TextArea.TextView.VisualLinesChanged += OnEditorTextViewVisualLinesChanged;
        ConfigureEditorFocusScrollSuppression(editor);

        if (_editorLayoutState is not null)
        {
            if (_hasAppliedInitialEditorLayout)
            {
                host.ApplyRuntimeLayout(_editorLayoutState.CurrentSettings);
            }
            else
            {
                host.ApplyInitialLayout(_editorLayoutState.CurrentSettings);
            }
        }

        SyncSecondaryEditorText(pane);
        UpdateActiveEditorBindings();
    }

    private void OnSecondaryEditorDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextEditor editor || editor.DataContext is not EditorPaneViewModel pane)
        {
            return;
        }

        if (_secondaryEditorHosts.Remove(pane.Id, out var host))
        {
            host.Dispose();
        }

        editor.ContextRequested -= OnEditorContextRequested;
        DetachWorkspaceBringIntoViewSuppression(editor);
        DetachWorkspaceBringIntoViewSuppression(editor.TextArea);
        editor.TextArea.Caret.PositionChanged -= OnEditorCaretPositionChanged;
        editor.TextArea.TextView.ScrollOffsetChanged -= OnEditorTextViewScrollOffsetChanged;
        editor.TextArea.TextView.VisualLinesChanged -= OnEditorTextViewVisualLinesChanged;
        _secondaryEditorControls.Remove(pane.Id);
        _secondaryEditorBorders.Remove(pane.Id);
        _secondaryTitleAnchors.Remove(pane.Id);
        if (_secondaryTagsTextBoxes.Remove(pane.Id, out var tagsTextBox))
        {
            tagsTextBox.PropertyChanged -= OnEditorTagsTextBoxPropertyChanged;
            tagsTextBox.GotFocus -= OnEditorTagsTextBoxGotFocus;
            tagsTextBox.LostFocus -= OnEditorTagsTextBoxLostFocus;
        }
        UpdateActiveEditorBindings();
    }

    private void OnSecondaryPaneRootAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control && control.DataContext is EditorPaneViewModel pane)
        {
            _secondaryPaneRoots[pane.Id] = control;
            if (control.FindControl<Border>("SecondaryEditorBorder") is { } editorBorder)
            {
                _secondaryEditorBorders[pane.Id] = editorBorder;
            }

            if (control.FindControl<Control>("SecondaryTitleAnchor") is { } titleAnchor)
            {
                _secondaryTitleAnchors[pane.Id] = titleAnchor;
            }

            if (control.FindControl<TextBox>("SecondaryTagsTextBox") is { } tagsTextBox)
            {
                _secondaryTagsTextBoxes[pane.Id] = tagsTextBox;
                AttachWorkspaceBringIntoViewSuppression(tagsTextBox);
                tagsTextBox.PropertyChanged += OnEditorTagsTextBoxPropertyChanged;
                tagsTextBox.GotFocus += OnEditorTagsTextBoxGotFocus;
                tagsTextBox.LostFocus += OnEditorTagsTextBoxLostFocus;
            }

            UpdateWorkspacePresentation();
            UpdateActiveEditorBindings();
        }
    }

    private void OnSecondaryPaneRootDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control && control.DataContext is EditorPaneViewModel pane)
        {
            if (_secondaryTagsTextBoxes.Remove(pane.Id, out var tagsTextBox))
            {
                DetachWorkspaceBringIntoViewSuppression(tagsTextBox);
                tagsTextBox.PropertyChanged -= OnEditorTagsTextBoxPropertyChanged;
                tagsTextBox.GotFocus -= OnEditorTagsTextBoxGotFocus;
                tagsTextBox.LostFocus -= OnEditorTagsTextBoxLostFocus;
            }

            _secondaryPaneRoots.Remove(pane.Id);
            UpdateWorkspacePresentation();
        }
    }

    private void OnPaneWorkspacePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        var nextOffset = PaneWorkspaceScrollViewer.Offset.X - (e.Delta.Y * 64);
        PaneWorkspaceScrollViewer.Offset = new Vector(Math.Max(0, nextOffset), PaneWorkspaceScrollViewer.Offset.Y);
        e.Handled = true;
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

        if (IsEqualizePaneWidthsGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            EqualizePaneWidthsToActivePane(vm);
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
        else if (IsClosePaneGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await vm.CloseActivePaneAsync();
        }
    }

    internal static bool IsOpenSettingsGesture(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsOpenSettingsGesture(key, modifiers);

    internal static bool IsShowShortcutsHelpGesture(Key key, KeyModifiers modifiers) =>
        InputGestureHelper.IsShowShortcutsHelpGesture(key, modifiers);

    internal static bool IsClosePaneGesture(Key key, KeyModifiers modifiers) =>
        (modifiers == KeyModifiers.Control || modifiers == KeyModifiers.Meta) && key == Key.W;

    internal static bool IsEqualizePaneWidthsGesture(Key key, KeyModifiers modifiers) =>
        (modifiers == KeyModifiers.Control || modifiers == KeyModifiers.Meta)
        && (key == Key.D0 || key == Key.NumPad0);

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

    private async Task CopyEditorSelectionAsync(TextEditor editor)
    {
        var selectedText = editor.SelectedText;
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

    private async Task CutEditorSelectionAsync(TextEditor editor)
    {
        var selectedText = editor.SelectedText;
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
        editor.SelectedText = string.Empty;
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

        if (_slashCommandPopup.HandleKeyDown(e, edit => ApplyEditorEdit(textEditor, edit)))
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
                var toggleTaskEdit = MarkdownEditingCommands.ToggleTaskState(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength);
                if (toggleTaskEdit.Length != 0 || toggleTaskEdit.Replacement.Length != 0)
                {
                    e.Handled = true;
                    ApplyEditorEdit(textEditor, toggleTaskEdit);
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
                await CopyEditorSelectionAsync(textEditor);
                return;
            }

            if (e.Key == Key.X)
            {
                e.Handled = true;
                await CutEditorSelectionAsync(textEditor);
                return;
            }

            if (e.Key == Key.B)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleWrap(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, "**"));
                return;
            }

            if (e.Key == Key.I)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleWrap(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, "*"));
                return;
            }

            if (e.Key == Key.K)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleWrap(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, "`"));
                return;
            }

            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.InsertLineBelow(GetEditorText(textEditor), textEditor.CaretOffset));
                return;
            }
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (IsMoveLineShortcut(e.Key, e.KeyModifiers, out var moveDown))
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.MoveLines(
                    GetEditorText(textEditor),
                    textEditor.SelectionStart,
                    textEditor.SelectionLength,
                    moveDown));
                return;
            }

            if (e.Key == Key.D)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.DeleteCurrentLine(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
                return;
            }

            if (e.Key == Key.D7)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleTaskList(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
                return;
            }

            if (e.Key == Key.D8)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleBulletList(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
                return;
            }
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (e.Key == Key.D1)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleHeading(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, 1));
                return;
            }

            if (e.Key == Key.D2)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleHeading(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, 2));
                return;
            }

            if (e.Key == Key.D3)
            {
                e.Handled = true;
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleHeading(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, 3));
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
        ApplyEditorEdit(textEditor, MarkdownListEditingCommands.ChangeIndentation(
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

    private string GetEditorText(TextEditor editor) => GetEditorHost(editor).GetText();

    private void ApplyEditorEdit(TextEditor editor, MarkdownEditResult edit)
    {
        var document = editor.Document;
        if (document is null)
        {
            return;
        }

        var start = Math.Clamp(edit.Start, 0, document.TextLength);
        var length = Math.Clamp(edit.Length, 0, document.TextLength - start);

        document.Replace(start, length, edit.Replacement);

        var selectionStart = Math.Clamp(edit.SelectionStart, 0, document.TextLength);
        var selectionLength = Math.Clamp(edit.SelectionLength, 0, document.TextLength - selectionStart);
        editor.Select(selectionStart, selectionLength);
        editor.CaretOffset = selectionStart + selectionLength;
        editor.Focus();
        _slashCommandPopup.ScheduleRefresh();
    }

    private void OnSlashCommandListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        _slashCommandPopup.ApplySelectedCommand(edit => ApplyEditorEdit(GetActiveTextEditor(), edit));
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (sender is StyledElement { DataContext: EditorPaneViewModel pane })
        {
            SetSecondaryPaneActive(pane);
        }
        else
        {
            ActivatePrimaryPane();
        }

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
