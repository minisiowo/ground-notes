using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
    private static readonly DataFormat<string> s_sidebarNotePathsDataFormat =
        DataFormat.CreateStringApplicationFormat("GroundNotes.NotePaths");
    private IWindowLayoutService? _windowLayoutService;
    private readonly MenuFlyout _editorContextFlyout = new();
    private readonly MenuFlyout _imageContextFlyout = new();
    private readonly MarkdownColorizingTransformer _markdownColorizer = new();
    private readonly NoteAssetService _noteAssetService = new();
    private readonly EditorHostController _editorHost;
    private readonly Dictionary<Guid, EditorHostController> _secondaryEditorHosts = [];
    private readonly Dictionary<Guid, TextEditor> _secondaryEditorControls = [];
    private readonly Dictionary<Guid, string?> _secondaryEditorSyncedFilePaths = [];
    private readonly Dictionary<Guid, Border> _secondaryEditorBorders = [];
    private readonly Dictionary<Guid, Control> _secondaryPaneRoots = [];
    private readonly Dictionary<Guid, Control> _secondaryTitleAnchors = [];
    private readonly Dictionary<Guid, TextBox> _secondaryTagsTextBoxes = [];
    private readonly WindowChromeController _windowChrome;
    private readonly TaskCompletionSource _openedTaskSource = new();
    private IEditorLayoutState? _editorLayoutState;
    private string? _primaryEditorSyncedFilePath;
    private bool _hasAppliedInitialEditorLayout;
    private bool _isUpdatingEditorFromViewModel;
    private bool _isUpdatingViewModelFromEditor;
    private SlashCommandPopupController _slashCommandPopup;
    private PointerPressedEventArgs? _sidebarDragPointerPressedEvent;
    private Point _sidebarDragStartPoint;
    private bool _isSidebarDragStarting;
    private SidebarTreeRowViewModel? _sidebarPendingSingleClickRow;
    private SidebarSelectionState? _sidebarSelectionBeforeContextMenu;
    private OverlayLayer? _sidebarDragOverlay;
    private Border? _sidebarDragGhost;
    private TextBlock? _sidebarDragGhostText;
    private TranslateTransform? _sidebarDragGhostTransform;
    private string _sidebarDragBaseLabel = string.Empty;
    private Control? _activeSidebarDropTarget;
    private readonly SidebarDragGhostPositionState _sidebarDragGhostPositionState = new();
    private int _sidebarDragGhostPositionGeneration;
    private IDataTransfer? _sidebarDragDataTransfer;
    private IReadOnlyList<string> _sidebarDragPaths = [];
    private IDataTransfer? _sidebarDragPathsCacheTransfer;
    private IReadOnlyList<string> _sidebarDragPathsCache = [];
    private readonly DispatcherTimer _sidebarDragTargetLabelTimer;
    private string? _pendingSidebarDragTargetLabel;
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
    private bool _isSidebarLayoutRefreshQueued;
    private Bitmap? _clipboardImageBitmap;
    private bool _windowResourcesDisposed;

    public MainWindow()
    {
        InitializeComponent();

        SidebarNotesContainer.AddHandler(
            DragDrop.DragEnterEvent,
            OnSidebarDragPositionChanged,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        SidebarNotesContainer.AddHandler(
            DragDrop.DragOverEvent,
            OnSidebarDragPositionChanged,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        _windowChrome = new WindowChromeController(
            this,
            new WindowChromeController.Options
            {
                IdleCursor = null,
                IsInteractiveControl = IsPointerOverInteractiveControl,
                ShouldSuppressTitleBarDoubleTap = e => e.Source is Control control && control.FindAncestorOfType<Button>() is not null
            });
        _editorHost = new EditorHostController(EditorTextEditor, _markdownColorizer, CopyCodeBlockAsync, _vimWorkspaceState);
        _editorHost.SetDocumentDisplayMode(EditorDocumentDisplayMode.Markdown);
        _slashCommandPopup = new SlashCommandPopupController(
            EditorTextEditor,
            EditorBorder,
            SlashCommandPopup,
            SlashCommandPopupContent,
            SlashCommandListBox,
            SlashCommandHintText);
        ConfigureVimHost(_editorHost, EditorTextEditor, PrimaryVimStatusText);
        _titleSuggestionsPopup = new ToolPopupController(TitleSuggestionsPopup, TitleSuggestionsPopupContent);
        _tagSuggestionsPopup = new ToolPopupController(TagSuggestionsPopup, TagSuggestionsPopupContent);
        _resizeHandleHoverTimer = new DispatcherTimer
        {
            Interval = ResizeHandleHoverDelay
        };
        _resizeHandleHoverTimer.Tick += OnResizeHandleHoverTimerTick;
        _sidebarDragTargetLabelTimer = new DispatcherTimer
        {
            Interval = SidebarDragTargetLabelDelay
        };
        _sidebarDragTargetLabelTimer.Tick += OnSidebarDragTargetLabelTimerTick;

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
        PaneWorkspaceScrollViewer.SizeChanged += OnPaneWorkspaceViewportSizeChanged;
        EditorPanel.SizeChanged += OnEditorPanelSizeChanged;
        SlashCommandPopup.PlacementTarget = EditorBorder;
        EditorTextEditor.TextChanged += OnEditorTextChanged;
        UpdateWorkspaceHostMargin();
        UpdateActiveEditorBindings();
        RebuildEditorContextFlyout();

        Opened += async (_, _) =>
        {
            try
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
                    _editorHost.SetBaseDirectoryPath(vm.NotesFolder);
                    ApplyVimSettings(vm);
                    ApplyEditorDisplayMode(vm.ShowYamlFrontMatterInEditor);
                    SyncEditorText(vm.EditorBody);
                    UpdateActiveEditorBindings();
                }

                await RestoreWindowLayoutAsync();
            }
            catch (Exception ex)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.StatusMessage = $"Layout restore failed: {ex.Message}";
                }
            }
            finally
            {
                _openedTaskSource.TrySetResult();
            }
        };

        Closing += async (_, e) =>
        {
            if (!_closeApproved)
            {
                e.Cancel = true;
                await RequestCloseAsync();
                return;
            }

            try
            {
                if (IsStandaloneWindow)
                {
                    SaveStandaloneNoteWindowLayout();
                }
                else
                {
                    SaveWindowLayout();
                }
            }
            catch (Exception ex)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.StatusMessage = $"Could not save window layout: {ex.Message}";
                }
            }
        };

        Closed += (_, _) => DisposeWindowResources();

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
            if (WindowState == WindowState.Normal && Bounds.Width > 0 && Bounds.Height > 0)
            {
                _lastNormalWidth = Bounds.Width;
                _lastNormalHeight = Bounds.Height;
            }

            _slashCommandPopup.SchedulePositionUpdate();
            _titleSuggestionsPopup.ScheduleRefresh();
            _tagSuggestionsPopup.ScheduleRefresh();
            UpdateWorkspacePresentation();
            UpdateSplitEditorAvailability();
        };

    }

    internal void DisposeBeforeShow()
    {
        _closeApproved = true;
        try
        {
            Close();
        }
        finally
        {
            DisposeWindowResources();
        }
    }

    private void DisposeWindowResources()
    {
        if (_windowResourcesDisposed)
        {
            return;
        }

        _windowResourcesDisposed = true;
        _slashCommandPopup.Dispose();
        HideSidebarDragGhost();
        SidebarNotesContainer.RemoveHandler(DragDrop.DragEnterEvent, OnSidebarDragPositionChanged);
        SidebarNotesContainer.RemoveHandler(DragDrop.DragOverEvent, OnSidebarDragPositionChanged);
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.FocusEditorRequested -= OnFocusEditorRequested;
            viewModel.SecondaryPanes.CollectionChanged -= OnSecondaryPanesCollectionChanged;
            foreach (var pane in viewModel.SecondaryPanes)
            {
                pane.PropertyChanged -= OnSecondaryPaneViewModelPropertyChanged;
            }

            viewModel.Dispose();
        }

        if (_editorLayoutState is not null)
        {
            _editorLayoutState.SettingsChanged -= OnEditorLayoutSettingsChanged;
            _editorLayoutState = null;
        }

        EditorTagsTextBox.PropertyChanged -= OnEditorTagsTextBoxPropertyChanged;
        EditorTagsTextBox.GotFocus -= OnEditorTagsTextBoxGotFocus;
        EditorTagsTextBox.LostFocus -= OnEditorTagsTextBoxLostFocus;
        DetachWorkspaceBringIntoViewSuppression(EditorTextEditor);
        DetachWorkspaceBringIntoViewSuppression(EditorTextEditor.TextArea);
        DetachWorkspaceBringIntoViewSuppression(EditorTitleTextBox);
        DetachWorkspaceBringIntoViewSuppression(EditorTagsTextBox);
        EditorTextEditor.GotFocus -= OnPrimaryEditorGotFocus;
        PaneWorkspaceScrollViewer.SizeChanged -= OnPaneWorkspaceViewportSizeChanged;
        EditorPanel.SizeChanged -= OnEditorPanelSizeChanged;
        _resizeHandleHoverTimer.Stop();
        _resizeHandleHoverTimer.Tick -= OnResizeHandleHoverTimerTick;
        _sidebarDragTargetLabelTimer.Stop();
        _sidebarDragTargetLabelTimer.Tick -= OnSidebarDragTargetLabelTimerTick;
        _clipboardImageBitmap?.Dispose();
        _clipboardImageBitmap = null;
        _editorHost.Dispose();
        DisposeSecondaryEditorHosts();
    }

    private void SetTitleBarVisible(bool isVisible)
    {
        TitleBarBorder.IsVisible = isVisible;
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

    public async Task CompleteStartupInitializationAsync(bool revealWindow = true)
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

        if (revealWindow)
        {
            Opacity = 1;
        }
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
            viewModel.RestoreSidebarTreeExpansion(layout.SidebarExpandedTagPaths);
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
        var sidebarWidth = GetSidebarWidthForLayout(vm);

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
            _multiPaneEqualizedPaneWidth,
            vm?.ExpandedSidebarTagPaths);
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
    private static readonly TimeSpan SidebarDragTargetLabelDelay = TimeSpan.FromMilliseconds(350);
    private const double EditorCanvasMinWidth = 520;
    private const double EditorCanvasResetThreshold = 12;
    private const double EditorResizeStripeHeight = 32;
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
        UpdateEditorResizeStripePosition(sender, e);
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
        ScheduleSidebarLayoutRefresh();
        e.Handled = true;
    }

    private void OnResizeHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isResizingSidebar)
            return;

        _isResizingSidebar = false;
        ScheduleSidebarLayoutRefresh();
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

        if (DataContext is MainViewModel vm && vm.HasSecondaryPane && !_isZenMode)
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
        UpdateEditorResizeStripePosition(sender, e);
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

        if (_isZenMode)
        {
            _zenEditorCanvasPreferredWidth = NormalizeDraggedEditorCanvasWidth(requestedWidth, availableWidth);
        }
        else
        {
            _editorCanvasPreferredWidth = NormalizeDraggedEditorCanvasWidth(requestedWidth, availableWidth);
            _multiPaneEqualizedPaneWidth = null;
        }

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

    private static void UpdateEditorResizeStripePosition(object? sender, PointerEventArgs e)
    {
        if (sender is not Border { Child: Border stripe } handle
            || !handle.Classes.Contains("editorResizeHandle"))
        {
            return;
        }

        var top = CalculateEditorResizeStripeTop(
            e.GetPosition(handle).Y,
            handle.Bounds.Height,
            EditorResizeStripeHeight);
        stripe.Margin = new Thickness(stripe.Margin.Left, top, stripe.Margin.Right, stripe.Margin.Bottom);
    }

    internal static double CalculateEditorResizeStripeTop(
        double pointerY,
        double handleHeight,
        double stripeHeight)
    {
        var maxTop = Math.Max(0, handleHeight - stripeHeight);
        return Math.Clamp(pointerY - (stripeHeight / 2), 0, maxTop);
    }

    private void OnPaneWorkspaceViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateWorkspacePresentation();
    }

    private void OnEditorPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateEditorCanvasHostWidth();
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
        UpdateEditorCanvasHostWidth();
    }

    private void UpdateEditorCanvasHostWidth()
    {
        if (TryUpdateZenEditorCanvasWidth())
        {
            return;
        }

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

        if (TryUpdateZenWorkspacePresentation(vm))
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
            PrimaryPaneRoot.Width = GetSinglePaneFitWidth(viewportWidth);
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

    private static double GetSinglePaneFitWidth(double viewportWidth)
    {
        return Math.Max(0, Math.Floor(viewportWidth - EqualFitSafetyGap));
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
            _editorCanvasPreferredWidth = null;
            _multiPaneEqualizedPaneWidth = null;
            UpdateEditorCanvasWidth();
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
        _slashCommandPopup.Dispose();
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

        if (_isZenMode)
        {
            return CalculateZenEditorWidth(availableWidth, _zenEditorCanvasPreferredWidth);
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
        if (_isZenMode)
        {
            return CalculateZenPaneWidth(Bounds.Width, WorkspaceHost.Margin);
        }

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
            }
            else
            {
                FocusEditorAfterNotePickerClosed(vm);
            }

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
            foreach (var host in _secondaryEditorHosts.Values)
            {
                host.RefreshVisualResources();
            }
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.NotesFolder))
        {
            _editorHost.SetBaseDirectoryPath(vm.NotesFolder);
            foreach (var host in _secondaryEditorHosts.Values)
            {
                host.SetBaseDirectoryPath(vm.NotesFolder);
            }

            return;
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedCodeFontFamilyName)
            or nameof(MainViewModel.SelectedCodeFontVariantName))
        {
            _editorHost.RefreshTypographyResources();
            foreach (var host in _secondaryEditorHosts.Values)
            {
                host.RefreshTypographyResources();
            }
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.ShowYamlFrontMatterInEditor))
        {
            ApplyEditorDisplayMode(vm.ShowYamlFrontMatterInEditor);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.VimModeSettings))
        {
            ApplyVimSettings(vm);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentNote))
        {
            var currentPath = vm.CurrentNote?.FilePath;
            if (!string.Equals(_primaryEditorSyncedFilePath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                SyncEditorText(vm.EditorBody);
            }
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.EditorBody))
        {
            SyncEditorText(vm.EditorBody);
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.HasSecondaryPane))
        {
            UpdateEditorCanvasWidth();
            UpdateSplitEditorAvailability();
            UpdateActiveEditorBindings();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.ActiveSecondaryPane) or nameof(MainViewModel.IsPrimaryPaneActive))
        {
            SyncSidebarSelectionFromActivePane(vm);
            UpdateActiveEditorBindings();
            _editorHost.ResetVimState();
            foreach (var host in _secondaryEditorHosts.Values)
            {
                host.ResetVimState();
            }
            UpdateEditorCanvasWidth();
            return;
        }

        if (e.PropertyName != nameof(MainViewModel.SidebarCollapsed))
            return;

        if (_isZenMode)
        {
            return;
        }

        AnimateSidebar(vm.SidebarCollapsed);
    }

    private void OnSecondaryPaneViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not EditorPaneViewModel pane)
        {
            return;
        }

        if (e.PropertyName is nameof(EditorPaneViewModel.CurrentNote)
            or nameof(EditorPaneViewModel.EditorBody))
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
        var animationCts = new CancellationTokenSource();
        _sidebarAnimationCts = animationCts;
        var cancellationToken = animationCts.Token;
        var editorResizeScopes = new List<IDisposable>
        {
            _editorHost.BeginContinuousResize()
        };
        editorResizeScopes.AddRange(_secondaryEditorHosts.Values.Select(host => host.BeginContinuousResize()));

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

                if (progress >= 1)
                {
                    break;
                }

                await WaitForNextAnimationFrameAsync(cancellationToken);
            }

            SidebarCol.Width = new GridLength(targetWidth, GridUnitType.Pixel);
            SidebarBorder.Opacity = targetOpacity;
            UpdateWorkspaceHostMargin();
            UpdateEditorCanvasWidth();
            SidebarCol.MinWidth = collapse ? 0 : SidebarMinWidth;
            SplitterCol.Width = new GridLength(collapse ? 0 : SidebarSplitterWidth, GridUnitType.Pixel);
            SidebarBorder.IsVisible = !collapse;
            UpdateWorkspaceHostMargin();
            ScheduleSidebarLayoutRefresh();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            for (var index = editorResizeScopes.Count - 1; index >= 0; index--)
            {
                editorResizeScopes[index].Dispose();
            }

            if (ReferenceEquals(_sidebarAnimationCts, animationCts))
            {
                _sidebarAnimationCts = null;
                animationCts.Dispose();
            }
        }
    }

    private void UpdateWorkspaceHostMargin()
    {
        var leftGutter = Math.Max(0, EditorOuterGutter - SplitterCol.Width.Value);
        WorkspaceHost.Margin = new Thickness(leftGutter, EditorOuterGutter, EditorOuterGutter, EditorOuterGutter);
    }

    private void ScheduleSidebarLayoutRefresh()
    {
        if (_isSidebarLayoutRefreshQueued)
        {
            return;
        }

        _isSidebarLayoutRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _isSidebarLayoutRefreshQueued = false;
                UpdateWorkspaceHostMargin();
                UpdateEditorCanvasWidth();
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Render);
    }

    private Task WaitForNextAnimationFrameAsync(CancellationToken cancellationToken)
    {
        var frameCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RequestAnimationFrame(_ => frameCompletion.TrySetResult());
        return frameCompletion.Task.WaitAsync(cancellationToken);
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
            : vm.VisibleSidebarRows.FirstOrDefault(row =>
                row.Note is not null
                && string.Equals(row.Note.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
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

    private void OnNoteListItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: SidebarTreeRowViewModel { Note: { } noteItem } } border
            || DataContext is not MainViewModel vm)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsRightButtonPressed)
        {
            _sidebarSelectionBeforeContextMenu = noteItem.IsSelected
                ? null
                : vm.CaptureSidebarSelection();
            vm.EnsureSidebarNoteSelected((SidebarTreeRowViewModel)border.DataContext!);
            e.Handled = true;
            if (border.ContextMenu is { } contextMenu)
            {
                contextMenu.Open(border);
            }
            else if (_sidebarSelectionBeforeContextMenu is { } previousSelection)
            {
                _sidebarSelectionBeforeContextMenu = null;
                vm.RestoreSidebarSelection(previousSelection);
            }
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var row = (SidebarTreeRowViewModel)border.DataContext!;
        _sidebarPendingSingleClickRow = null;
        _sidebarDragPointerPressedEvent = e;
        _sidebarDragStartPoint = e.GetPosition(NotesListBox);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            vm.SelectSidebarNoteRange(row);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            vm.ToggleSidebarNoteSelection(row);
            e.Handled = true;
            return;
        }

        if (noteItem.IsSelected && vm.SelectedSidebarNotes.Count > 1)
        {
            _sidebarPendingSingleClickRow = row;
            e.Handled = true;
            return;
        }

        vm.SelectOnlySidebarNote(row);
        _sidebarPendingSingleClickRow = row;
        e.Handled = true;
    }

    private async void OnNoteListItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_sidebarDragPointerPressedEvent is null
            || _isSidebarDragStarting
            || DataContext is not MainViewModel vm
            || !e.GetCurrentPoint(NotesListBox).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var delta = e.GetPosition(NotesListBox) - _sidebarDragStartPoint;
        if (Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4)
        {
            return;
        }

        var paths = vm.SelectedSidebarNotes.Select(note => note.FilePath).ToArray();
        if (paths.Length == 0)
        {
            return;
        }

        _isSidebarDragStarting = true;
        _sidebarPendingSingleClickRow = null;
        var pointerPressedEvent = _sidebarDragPointerPressedEvent;
        _sidebarDragPointerPressedEvent = null;
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(s_sidebarNotePathsDataFormat, string.Join('\n', paths)));
            ShowSidebarDragGhost(vm.SelectedSidebarNotes, e);
            _sidebarDragDataTransfer = data;
            _sidebarDragPaths = paths;
            _sidebarDragPathsCacheTransfer = data;
            _sidebarDragPathsCache = paths;
            await DragDrop.DoDragDropAsync(pointerPressedEvent, data, DragDropEffects.Move);
        }
        finally
        {
            HideSidebarDragGhost();
            _isSidebarDragStarting = false;
        }
    }

    private async void OnNoteListItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _sidebarDragPointerPressedEvent = null;
        var pendingRow = _sidebarPendingSingleClickRow;
        _sidebarPendingSingleClickRow = null;
        if (pendingRow?.Note is not { } note || DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.SelectOnlySidebarNote(pendingRow);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(note);
    }

    private void OnSidebarNoteContextMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (_sidebarSelectionBeforeContextMenu is not { } previousSelection
            || DataContext is not MainViewModel vm)
        {
            return;
        }

        _sidebarSelectionBeforeContextMenu = null;
        vm.RestoreSidebarSelection(previousSelection);
    }

    private void OnSidebarDragPositionChanged(object? sender, DragEventArgs e)
    {
        if (!_isSidebarDragStarting
            || _sidebarDragGhost is null
            || _sidebarDragOverlay is null)
        {
            return;
        }

        QueueSidebarDragGhostPosition(e.GetPosition(_sidebarDragOverlay));
    }

    private void OnSidebarFolderDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Button { DataContext: SidebarTreeRowViewModel { TagPath: { } tagPath } } button
            && GetSidebarDragPaths(e.DataTransfer).Count > 0)
        {
            e.DragEffects = DragDropEffects.Move;
            SetActiveSidebarDropTarget(button, $"Add to {tagPath}");
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
    }

    private void OnSidebarFolderDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is not Button button
            || IsPointWithinSidebarDropTarget(e.GetPosition(button), button.Bounds.Size)
            || !ReferenceEquals(button, _activeSidebarDropTarget))
        {
            return;
        }

        SetActiveSidebarDropTarget(null, null);
    }

    private async void OnSidebarFolderDrop(object? sender, DragEventArgs e)
    {
        var paths = GetSidebarDragPaths(e.DataTransfer);
        if (sender is not Button { DataContext: SidebarTreeRowViewModel { TagPath: { } tagPath } }
            || DataContext is not MainViewModel vm
            || paths.Count == 0)
        {
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        SetActiveSidebarDropTarget(null, null);
        await vm.AddSidebarNotesToTagFolderAsync(paths, tagPath);
    }

    private void OnSidebarRootDragOver(object? sender, DragEventArgs e)
    {
        if (sender is Border border && GetSidebarDragPaths(e.DataTransfer).Count > 0)
        {
            e.DragEffects = DragDropEffects.Move;
            SetActiveSidebarDropTarget(border, "Move to root");
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.None;
    }

    private void OnSidebarRootDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is not Border border
            || IsPointWithinSidebarDropTarget(e.GetPosition(border), border.Bounds.Size)
            || !ReferenceEquals(border, _activeSidebarDropTarget))
        {
            return;
        }

        SetActiveSidebarDropTarget(null, null);
    }

    private async void OnSidebarRootDrop(object? sender, DragEventArgs e)
    {
        var paths = GetSidebarDragPaths(e.DataTransfer);
        if (sender is not Border
            || DataContext is not MainViewModel vm
            || paths.Count == 0)
        {
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        SetActiveSidebarDropTarget(null, null);
        await vm.MoveSidebarNotesToRootAsync(paths);
    }

    internal static bool IsPointWithinSidebarDropTarget(Point point, Size targetSize)
    {
        return point.X >= 0
            && point.X <= targetSize.Width
            && point.Y >= 0
            && point.Y <= targetSize.Height;
    }

    internal static string FormatSidebarDragLabel(IReadOnlyList<NoteListItemViewModel> notes)
    {
        if (notes.Count == 0)
        {
            return string.Empty;
        }

        return notes.Count == 1
            ? notes[0].DisplayName
            : $"{notes[0].DisplayName} +{notes.Count - 1}";
    }

    private IReadOnlyList<string> GetSidebarDragPaths(IDataTransfer dataTransfer)
    {
        if (ReferenceEquals(dataTransfer, _sidebarDragDataTransfer))
        {
            return _sidebarDragPaths;
        }

        if (ReferenceEquals(dataTransfer, _sidebarDragPathsCacheTransfer))
        {
            return _sidebarDragPathsCache;
        }

        var paths = ParseSidebarDragPaths(dataTransfer.TryGetValues(s_sidebarNotePathsDataFormat));
        _sidebarDragPathsCacheTransfer = dataTransfer;
        _sidebarDragPathsCache = paths;
        return paths;
    }

    internal static IReadOnlyList<string> ParseSidebarDragPaths(IEnumerable<string>? values)
    {
        return values?
            .SelectMany(value => value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private void ShowSidebarDragGhost(IReadOnlyList<NoteListItemViewModel> notes, PointerEventArgs e)
    {
        HideSidebarDragGhost();
        _sidebarDragOverlay = OverlayLayer.GetOverlayLayer(NotesListBox);
        if (_sidebarDragOverlay is null)
        {
            return;
        }

        _sidebarDragBaseLabel = FormatSidebarDragLabel(notes);
        _sidebarDragGhostText = new TextBlock { Text = _sidebarDragBaseLabel };
        _sidebarDragGhostText.Classes.Add("sidebarDragGhostText");
        _sidebarDragGhostTransform = new TranslateTransform();
        _sidebarDragGhost = new Border
        {
            Child = _sidebarDragGhostText,
            IsHitTestVisible = false,
            RenderTransform = _sidebarDragGhostTransform
        };
        _sidebarDragGhost.Classes.Add("sidebarDragGhost");
        _sidebarDragOverlay.Children.Add(_sidebarDragGhost);
        SidebarRootDropTarget.IsVisible = true;
        ApplySidebarDragGhostPosition(e.GetPosition(_sidebarDragOverlay));
    }

    private void QueueSidebarDragGhostPosition(Point point)
    {
        if (_sidebarDragGhostTransform is null
            || !_sidebarDragGhostPositionState.Queue(point))
        {
            return;
        }

        var generation = _sidebarDragGhostPositionGeneration;
        Dispatcher.UIThread.Post(
            () => ApplyQueuedSidebarDragGhostPosition(generation),
            DispatcherPriority.Render);
    }

    private void ApplyQueuedSidebarDragGhostPosition(int generation)
    {
        if (generation != _sidebarDragGhostPositionGeneration
            || !_sidebarDragGhostPositionState.TryConsume(out var point))
        {
            return;
        }

        ApplySidebarDragGhostPosition(point);
    }

    private void ApplySidebarDragGhostPosition(Point point)
    {
        if (_sidebarDragGhostTransform is not { } transform)
        {
            return;
        }

        transform.X = point.X + 14;
        transform.Y = point.Y + 14;
    }

    private void SetActiveSidebarDropTarget(Control? target, string? ghostTargetLabel)
    {
        if (ReferenceEquals(_activeSidebarDropTarget, target))
        {
            if (target is null)
            {
                CancelSidebarDragTargetLabelReveal();
            }
            return;
        }

        if (_activeSidebarDropTarget is { } previousTarget)
        {
            previousTarget.Classes.Set(GetSidebarDropTargetClass(previousTarget), false);
        }

        _activeSidebarDropTarget = target;
        if (target is not null)
        {
            target.Classes.Set(GetSidebarDropTargetClass(target), true);
        }

        CancelSidebarDragTargetLabelReveal();
        if (target is not null && !string.IsNullOrWhiteSpace(ghostTargetLabel))
        {
            _pendingSidebarDragTargetLabel = ghostTargetLabel;
            _sidebarDragTargetLabelTimer.Start();
        }
    }

    private void OnSidebarDragTargetLabelTimerTick(object? sender, EventArgs e)
    {
        _sidebarDragTargetLabelTimer.Stop();
        if (_activeSidebarDropTarget is not null && _pendingSidebarDragTargetLabel is { } targetLabel)
        {
            SetSidebarDragGhostTarget(targetLabel);
        }
        _pendingSidebarDragTargetLabel = null;
    }

    private void CancelSidebarDragTargetLabelReveal()
    {
        _sidebarDragTargetLabelTimer.Stop();
        _pendingSidebarDragTargetLabel = null;
        SetSidebarDragGhostTarget(null);
    }

    private string GetSidebarDropTargetClass(Control target)
    {
        return ReferenceEquals(target, SidebarNotesContainer)
            ? "rootDragTarget"
            : "dragTarget";
    }

    private void SetSidebarDragGhostTarget(string? target)
    {
        if (_sidebarDragGhostText is not null)
        {
            _sidebarDragGhostText.Text = string.IsNullOrWhiteSpace(target)
                ? _sidebarDragBaseLabel
                : $"{_sidebarDragBaseLabel} -> {target}";
        }
    }

    private void HideSidebarDragGhost()
    {
        SetActiveSidebarDropTarget(null, null);

        if (_sidebarDragOverlay is not null && _sidebarDragGhost is not null)
        {
            _sidebarDragOverlay.Children.Remove(_sidebarDragGhost);
        }

        foreach (var button in NotesListBox.GetVisualDescendants().OfType<Button>())
        {
            button.Classes.Set("dragTarget", false);
        }

        SidebarNotesContainer.Classes.Set("rootDragTarget", false);
        SidebarRootDropTarget.Classes.Set("dragTarget", false);
        SidebarRootDropTarget.IsVisible = false;
        _sidebarDragGhostPositionGeneration++;
        _sidebarDragGhostPositionState.Reset();
        _sidebarDragOverlay = null;
        _sidebarDragGhost = null;
        _sidebarDragGhostTransform = null;
        _sidebarDragGhostText = null;
        _sidebarDragBaseLabel = string.Empty;
        _sidebarDragDataTransfer = null;
        _sidebarDragPaths = [];
        _sidebarDragPathsCacheTransfer = null;
        _sidebarDragPathsCache = [];
    }

    private async void OnSidebarTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedSidebarRow is not { } row)
        {
            return;
        }

        if (row.IsNote
            && e.Key == Key.Space
            && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            vm.ToggleSidebarNoteSelection(row);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            var currentIndex = vm.VisibleSidebarRows.IndexOf(row);
            var nextIndex = Math.Clamp(currentIndex + (e.Key == Key.Up ? -1 : 1), 0, vm.VisibleSidebarRows.Count - 1);
            var nextRow = vm.VisibleSidebarRows[nextIndex];
            vm.SelectedSidebarRow = nextRow;
            NotesListBox.SelectedItem = nextRow;
            NotesListBox.ScrollIntoView(nextRow);

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && nextRow.IsNote)
            {
                vm.SelectSidebarNoteRange(nextRow);
            }
            else if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                if (nextRow.IsNote)
                {
                    vm.SelectOnlySidebarNote(nextRow);
                }
                else
                {
                    vm.ClearSidebarSelection();
                }
            }

            e.Handled = true;
            return;
        }

        if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.DeleteNote, e.Key, e.KeyModifiers)
            && vm.SelectedSidebarNotes.Count > 0)
        {
            e.Handled = true;
            await vm.DeleteSelectedSidebarNotesCommand.ExecuteAsync(null);
            return;
        }

        if (row.IsFolder && e.Key is Key.Enter or Key.Right or Key.Left)
        {
            if (e.Key == Key.Enter
                || (e.Key == Key.Right && !row.IsExpanded)
                || (e.Key == Key.Left && row.IsExpanded))
            {
                row.ToggleExpandedCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                SelectSidebarParentRow(vm, row);
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Left && row.IsNote)
        {
            SelectSidebarParentRow(vm, row);
            e.Handled = true;
            return;
        }

        if (!row.IsNote || row.Note is null || e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            await vm.OpenNoteInSplitCommand.ExecuteAsync(row.Note);
            return;
        }

        vm.SelectOnlySidebarNote(row);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(row.Note);
    }

    private void SelectSidebarParentRow(MainViewModel vm, SidebarTreeRowViewModel row)
    {
        var index = vm.VisibleSidebarRows.IndexOf(row);
        for (var candidateIndex = index - 1; candidateIndex >= 0; candidateIndex--)
        {
            var candidate = vm.VisibleSidebarRows[candidateIndex];
            if (candidate.IsFolder && candidate.Depth == row.Depth - 1)
            {
                vm.SelectedSidebarRow = candidate;
                NotesListBox.SelectedItem = candidate;
                NotesListBox.ScrollIntoView(candidate);
                return;
            }
        }
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
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Paste", targetEditor.CanPaste, async (_, _) => await PasteIntoEditorAsync(targetEditor)));
        if (GetEditorHost(targetEditor).SupportsMarkdownTables)
        {
            _editorContextFlyout.Items.Add(new Separator());
            AddMarkdownTableMenuSection(targetEditor);
        }

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

    private void AddMarkdownTableMenuSection(TextEditor editor)
    {
        var host = GetEditorHost(editor);
        if (!host.IsCaretInMarkdownTable)
        {
            _editorContextFlyout.Items.Add(CreateEditorMenuItem("Insert table", true, (_, _) =>
            {
                DismissEditorContextFlyout();
                ApplyEditorEdit(editor, MarkdownTableEditingCommands.InsertTable(GetEditorText(editor), editor.SelectionStart, editor.SelectionLength));
            }));
            return;
        }

        var tableMenu = new MenuItem { Header = "Table" };
        tableMenu.Items.Add(CreateTableActionItem("Format table", host.FormatMarkdownTable));

        var rowsMenu = new MenuItem { Header = "Rows" };
        rowsMenu.Items.Add(CreateTableActionItem("Insert above", () => host.InsertMarkdownTableRow(above: true)));
        rowsMenu.Items.Add(CreateTableActionItem("Insert below", () => host.InsertMarkdownTableRow(above: false)));
        rowsMenu.Items.Add(CreateTableActionItem("Move up", () => host.MoveMarkdownTableRow(down: false)));
        rowsMenu.Items.Add(CreateTableActionItem("Move down", () => host.MoveMarkdownTableRow(down: true)));
        rowsMenu.Items.Add(CreateTableActionItem("Delete row", host.DeleteMarkdownTableRow));
        tableMenu.Items.Add(rowsMenu);

        var columnsMenu = new MenuItem { Header = "Columns" };
        columnsMenu.Items.Add(CreateTableActionItem("Insert left", () => host.InsertMarkdownTableColumn(before: true)));
        columnsMenu.Items.Add(CreateTableActionItem("Insert right", () => host.InsertMarkdownTableColumn(before: false)));
        columnsMenu.Items.Add(CreateTableActionItem("Move left", () => host.MoveMarkdownTableColumn(right: false)));
        columnsMenu.Items.Add(CreateTableActionItem("Move right", () => host.MoveMarkdownTableColumn(right: true)));
        columnsMenu.Items.Add(CreateTableActionItem("Delete column", host.DeleteMarkdownTableColumn));
        tableMenu.Items.Add(columnsMenu);

        _editorContextFlyout.Items.Add(tableMenu);
    }

    private MenuItem CreateTableActionItem(string header, Func<bool> action)
    {
        return CreateEditorMenuItem(header, true, (_, _) =>
        {
            action();
            DismissEditorContextFlyout();
        });
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

            ApplyEditorEdit(
                targetEditor,
                BuildAiResultEdit(document.Text, selectionStart, selectionLength, result));

            vm.StatusMessage = $"{prompt.Name} applied.";
        }
        finally
        {
            targetEditor.Focus();
        }
    }

    internal static MarkdownEditResult BuildAiResultEdit(
        string documentText,
        int selectionStart,
        int selectionLength,
        string result)
        => BuildTableFragmentEdit(documentText, selectionStart, selectionLength, result);

    internal static MarkdownEditResult BuildTableFragmentEdit(
        string documentText,
        int selectionStart,
        int selectionLength,
        string fragment)
        => BuildTableFragmentEdit(
            documentText,
            selectionStart,
            selectionLength,
            MarkdownTableFormatter.FormatAllWithMetadata(fragment));

    private static MarkdownEditResult BuildTableFragmentEdit(
        string documentText,
        int selectionStart,
        int selectionLength,
        MarkdownTableFormatResult fragment)
    {
        var start = Math.Clamp(selectionStart, 0, documentText.Length);
        var length = Math.Clamp(selectionLength, 0, documentText.Length - start);
        if (!fragment.ContainsTables)
        {
            return new MarkdownEditResult(start, length, fragment.Text, start + fragment.Text.Length, 0);
        }

        var newLine = documentText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var needsLeadingBreak = fragment.SourceTables[0].Start == 0
                                && start > 0
                                && documentText[start - 1] is not ('\r' or '\n');
        var followingOffset = start + length;
        var lastTable = fragment.SourceTables[^1];
        var needsTrailingBreak = lastTable.Start + lastTable.Length == fragment.SourceLength
                                 && followingOffset < documentText.Length
                                 && documentText[followingOffset] is not ('\r' or '\n');
        var replacement = (needsLeadingBreak ? newLine : string.Empty)
                          + fragment.Text
                          + (needsTrailingBreak ? newLine : string.Empty);
        return new MarkdownEditResult(start, length, replacement, start + replacement.Length, 0);
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
            if (TryShowImageContextFlyout(editor, e))
            {
                e.Handled = true;
                return;
            }

            SetEditorContextCaret(editor, e);
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

    private static void SetEditorContextCaret(TextEditor editor, ContextRequestedEventArgs e)
    {
        var document = editor.Document;
        if (document is null
            || !e.TryGetPosition(editor.TextArea.TextView, out var point)
            || editor.TextArea.TextView.GetPosition(point) is not TextViewPosition position)
        {
            return;
        }

        var offset = document.GetOffset(position.Location);
        var selectionStart = editor.SelectionStart;
        var selectionEnd = selectionStart + editor.SelectionLength;
        if (editor.SelectionLength != 0 && offset >= selectionStart && offset <= selectionEnd)
        {
            return;
        }

        if (MarkdownTableParser.TryFindTableAtOffset(document.Text, offset, out var table))
        {
            var delimiter = table.Rows.FirstOrDefault(row => row.IsDelimiter
                && offset >= row.Start
                && offset <= row.Start + row.Length);
            if (delimiter is not null)
            {
                var column = delimiter.Cells.Count - 1;
                for (var index = 0; index < delimiter.Cells.Count; index++)
                {
                    var cell = delimiter.Cells[index];
                    if (offset <= cell.SegmentStart + cell.SegmentLength)
                    {
                        column = index;
                        break;
                    }
                }

                offset = table.Header.Cells[Math.Clamp(column, 0, table.ColumnCount - 1)].EditableStart;
            }
        }

        editor.Select(offset, 0);
        editor.CaretOffset = offset;
    }

    private bool TryShowImageContextFlyout(TextEditor editor, ContextRequestedEventArgs e)
    {
        if (!e.TryGetPosition(editor.TextArea.TextView, out var point))
        {
            return false;
        }

        var hit = GetEditorHost(editor).TryHitTestImagePreview(point);
        if (hit is null)
        {
            return false;
        }

        RebuildImageContextFlyout(editor, hit.Value);
        _imageContextFlyout.ShowAt(editor, true);
        return true;
    }

    private void RebuildImageContextFlyout(TextEditor editor, MarkdownImagePreviewHitTestResult hit)
    {
        _imageContextFlyout.Items.Clear();

        var openItem = new MenuItem
        {
            Header = "Open image"
        };
        openItem.Click += (_, _) => OpenImageViewer(editor, hit);
        _imageContextFlyout.Items.Add(openItem);

        var copyItem = new MenuItem
        {
            Header = "Copy"
        };
        copyItem.Click += async (_, _) => await CopyImageToClipboardAsync(hit.ResolvedPath);
        _imageContextFlyout.Items.Add(copyItem);

        _imageContextFlyout.Items.Add(new Separator());

        var canManageAsset = DataContext is MainViewModel vm
            && _noteAssetService.IsManagedAssetPath(vm.NotesFolder, hit.ResolvedPath);

        var renameItem = new MenuItem
        {
            Header = "Rename image...",
            IsEnabled = canManageAsset
        };
        renameItem.Click += async (_, _) => await RenameImageAssetAsync(editor, hit);
        _imageContextFlyout.Items.Add(renameItem);

        var deleteItem = new MenuItem
        {
            Header = "Delete image",
            IsEnabled = canManageAsset
        };
        deleteItem.Click += async (_, _) => await DeleteImageAssetAsync(editor, hit);
        _imageContextFlyout.Items.Add(deleteItem);
    }

    private async Task RenameImageAssetAsync(TextEditor editor, MarkdownImagePreviewHitTestResult hit)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var dialog = new RenameImageWindow(Path.GetFileName(hit.ResolvedPath));
        var requestedFileName = await dialog.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(requestedFileName))
        {
            return;
        }

        if (!_noteAssetService.TryBuildRenameAssetPath(vm.NotesFolder, hit.ResolvedPath, requestedFileName, out var newAssetPath, out var newMarkdownPath, out var errorMessage))
        {
            vm.StatusMessage = errorMessage;
            return;
        }

        try
        {
            File.Move(hit.ResolvedPath, newAssetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            vm.StatusMessage = $"Could not rename image: {ex.Message}";
            return;
        }

        ApplyEditorEdit(editor, MarkdownImageEditingCommands.RenameImageUrl(GetEditorText(editor), hit.UrlStart, hit.UrlLength, newMarkdownPath));

        var updatedCount = await vm.RenameImageReferenceInAllNotesAsync(
            hit.ResolvedPath,
            newMarkdownPath,
            _noteAssetService);

        vm.StatusMessage = updatedCount > 0
            ? $"Renamed image to {Path.GetFileName(newAssetPath)} (updated in {updatedCount} other note(s))"
            : $"Renamed image to {Path.GetFileName(newAssetPath)}";
    }

    private async Task DeleteImageAssetAsync(TextEditor editor, MarkdownImagePreviewHitTestResult hit)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!_noteAssetService.IsManagedAssetPath(vm.NotesFolder, hit.ResolvedPath))
        {
            vm.StatusMessage = "Only images from this note folder's assets directory can be deleted.";
            return;
        }

        var fileName = Path.GetFileName(hit.ResolvedPath);
        if (!File.Exists(hit.ResolvedPath))
        {
            vm.StatusMessage = $"Image file was not found: {fileName}";
            return;
        }

        var dialog = new ConfirmDeleteWindow(
            "Delete image",
            "Delete image?",
            $"Delete '{fileName}' from disk and remove it from this note?",
            "Delete");
        var shouldDelete = await dialog.ShowDialog<bool>(this);
        if (!shouldDelete)
        {
            vm.StatusMessage = "Delete canceled.";
            return;
        }

        try
        {
            File.Delete(hit.ResolvedPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            vm.StatusMessage = $"Could not delete image: {ex.Message}";
            return;
        }

        ApplyEditorEdit(editor, MarkdownImageEditingCommands.DeleteImageReference(GetEditorText(editor), hit.ReferenceStart, hit.ReferenceLength));
        vm.StatusMessage = $"Deleted image {fileName}";
    }

    private async Task CopyImageToClipboardAsync(string imagePath)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            vm.StatusMessage = "System clipboard is not available.";
            return;
        }

        if (!File.Exists(imagePath))
        {
            vm.StatusMessage = $"Image file was not found: {Path.GetFileName(imagePath)}";
            return;
        }

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(imagePath);
            await topLevel.Clipboard.SetBitmapAsync(bitmap);
            _clipboardImageBitmap?.Dispose();
            _clipboardImageBitmap = bitmap;
            bitmap = null;
        }
        catch (Exception ex) when (ex is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidOperationException or
            NotSupportedException)
        {
            bitmap?.Dispose();
            vm.StatusMessage = $"Could not copy image: {ex.Message}";
            return;
        }

        vm.StatusMessage = $"Copied image {Path.GetFileName(imagePath)}";
    }

    private async Task<bool> CopyAnnotatedImageToClipboardAsync(RenderTargetBitmap annotatedBitmap)
    {
        if (DataContext is not MainViewModel vm)
        {
            return false;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            vm.StatusMessage = "System clipboard is not available.";
            return false;
        }

        try
        {
            await topLevel.Clipboard.SetBitmapAsync(annotatedBitmap);
            _clipboardImageBitmap?.Dispose();
            _clipboardImageBitmap = annotatedBitmap;
        }
        catch (Exception ex) when (ex is
            IOException or
            UnauthorizedAccessException or
            ArgumentException or
            InvalidOperationException or
            NotSupportedException)
        {
            vm.StatusMessage = $"Could not copy annotated image: {ex.Message}";
            return false;
        }

        vm.StatusMessage = "Copied annotated image";
        return true;
    }

    private async Task<ImageViewerSaveResult> SaveAnnotatedImageAsync(
        TextEditor editor,
        MarkdownImagePreviewHitTestResult hit,
        string currentImagePath,
        RenderTargetBitmap annotatedBitmap,
        bool overwrite)
    {
        if (DataContext is not MainViewModel vm)
        {
            return new ImageViewerSaveResult(false, null);
        }

        try
        {
            if (overwrite)
            {
                await SaveBitmapToFileAsync(annotatedBitmap, currentImagePath);
                RefreshEditorImagePreviews(editor, currentImagePath);
                vm.StatusMessage = $"Saved annotations to {Path.GetFileName(currentImagePath)}";
                return new ImageViewerSaveResult(true, currentImagePath);
            }

            if (string.IsNullOrWhiteSpace(vm.NotesFolder))
            {
                vm.StatusMessage = "Choose a notes folder before saving an annotated image copy.";
                return new ImageViewerSaveResult(false, null);
            }

            var currentText = GetEditorText(editor);
            var currentUrlLength = TryGetImageUrlLengthAtOffset(currentText, hit.UrlStart);
            if (currentUrlLength is null)
            {
                vm.StatusMessage = "Could not update the image reference in this note.";
                return new ImageViewerSaveResult(false, null);
            }

            var assetFileName = await _noteAssetService.SaveBitmapAsync(vm.NotesFolder, annotatedBitmap);
            var newMarkdownPath = _noteAssetService.BuildAssetMarkdownPath(assetFileName);
            ApplyEditorEdit(
                editor,
                MarkdownImageEditingCommands.RenameImageUrl(currentText, hit.UrlStart, currentUrlLength.Value, newMarkdownPath));
            var newImagePath = Path.Combine(vm.NotesFolder, "assets", assetFileName);
            vm.StatusMessage = $"Saved annotated copy {assetFileName}";
            return new ImageViewerSaveResult(true, newImagePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            vm.StatusMessage = $"Could not save annotated image: {ex.Message}";
            return new ImageViewerSaveResult(false, null);
        }
    }

    private static async Task SaveBitmapToFileAsync(Bitmap bitmap, string filePath)
    {
        await using var stream = File.Create(filePath);
        bitmap.Save(stream, quality: null);
        await stream.FlushAsync();
    }

    private void RefreshEditorImagePreviews(TextEditor editor, string imagePath)
    {
        var host = GetEditorHost(editor);
        host.RefreshImagePreviews(imagePath);
    }

    private static int? TryGetImageUrlLengthAtOffset(string text, int urlStart)
    {
        if (urlStart < 0 || urlStart >= text.Length)
        {
            return null;
        }

        var urlEnd = text.IndexOf(')', urlStart);
        return urlEnd <= urlStart
            ? null
            : urlEnd - urlStart;
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
        var syncedFilePath = (DataContext as MainViewModel)?.CurrentNote?.FilePath;
        var shouldResetViewport = HasSyncedFilePathChanged(_primaryEditorSyncedFilePath, syncedFilePath);
        var changed = _editorHost.SyncFromViewModel(text, appendSuffixWhenPossible: false, out var appendedOnly);
        var isEditorOriginatedUpdate = _editorHost.IsUpdatingViewModelFromEditor;
        if (!isEditorOriginatedUpdate
            && DataContext is MainViewModel vm
            && !string.Equals(text, _editorHost.GetText(), StringComparison.Ordinal))
        {
            _editorHost.SyncToViewModel(() => vm.EditorBody, normalized => vm.EditorBody = normalized);
        }

        if (shouldResetViewport && !isEditorOriginatedUpdate)
        {
            _editorHost.ResetVimState();
        }
        _primaryEditorSyncedFilePath = syncedFilePath;
        _isUpdatingEditorFromViewModel = _editorHost.IsUpdatingEditorFromViewModel;
        if (!changed)
        {
            if (shouldResetViewport && !isEditorOriginatedUpdate)
            {
                _editorHost.ResetViewportToDocumentStart();
            }

            return;
        }

        if (!appendedOnly)
        {
            _editorHost.RefreshLayoutAfterDocumentReplace();
            if (shouldResetViewport)
            {
                _editorHost.ResetViewportToDocumentStart();
            }
        }

        _slashCommandPopup.ScheduleRefresh();
    }

    private void SyncSecondaryEditorText(EditorPaneViewModel pane)
    {
        if (!_secondaryEditorHosts.TryGetValue(pane.Id, out var host))
        {
            return;
        }

        var syncedFilePath = pane.CurrentNote?.FilePath;
        var previousFilePath = _secondaryEditorSyncedFilePaths.GetValueOrDefault(pane.Id);
        var shouldResetViewport = HasSyncedFilePathChanged(previousFilePath, syncedFilePath);
        var changed = host.SyncFromViewModel(pane.EditorBody, appendSuffixWhenPossible: false, out var appendedOnly);
        var isEditorOriginatedUpdate = host.IsUpdatingViewModelFromEditor;
        if (!isEditorOriginatedUpdate
            && !string.Equals(pane.EditorBody, host.GetText(), StringComparison.Ordinal))
        {
            host.SyncToViewModel(() => pane.EditorBody, normalized => pane.EditorBody = normalized);
        }

        if (shouldResetViewport && !isEditorOriginatedUpdate)
        {
            host.ResetVimState();
        }
        _secondaryEditorSyncedFilePaths[pane.Id] = syncedFilePath;
        if (!changed)
        {
            if (shouldResetViewport && !isEditorOriginatedUpdate)
            {
                host.ResetViewportToDocumentStart();
            }

            return;
        }

        if (!appendedOnly)
        {
            host.RefreshLayoutAfterDocumentReplace();
            if (shouldResetViewport)
            {
                host.ResetViewportToDocumentStart();
            }
        }
    }

    private static bool HasSyncedFilePathChanged(string? previousFilePath, string? syncedFilePath)
    {
        return !string.Equals(previousFilePath, syncedFilePath, StringComparison.OrdinalIgnoreCase);
    }

    private void OnSecondaryEditorAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextEditor editor || editor.DataContext is not EditorPaneViewModel pane)
        {
            return;
        }

        var host = new EditorHostController(editor, new MarkdownColorizingTransformer(), CopyCodeBlockAsync, _vimWorkspaceState);
        _secondaryEditorHosts[pane.Id] = host;
        _secondaryEditorControls[pane.Id] = editor;
        ConfigureVimHost(host, editor, FindSecondaryVimStatus(pane.Id));
        if (DataContext is MainViewModel vm)
        {
            host.SetBaseDirectoryPath(vm.NotesFolder);
            host.SetDocumentDisplayMode(vm.ShowYamlFrontMatterInEditor ? EditorDocumentDisplayMode.PlainText : EditorDocumentDisplayMode.Markdown);
        }

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
        if (_isZenMode && DataContext is MainViewModel { ActiveSecondaryPane: { } activePane }
            && ReferenceEquals(activePane, pane))
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isZenMode
                    && DataContext is MainViewModel { ActiveSecondaryPane: { } currentActivePane }
                    && ReferenceEquals(currentActivePane, pane)
                    && _secondaryEditorControls.TryGetValue(pane.Id, out var currentEditor)
                    && ReferenceEquals(currentEditor, editor))
                {
                    editor.Focus();
                }
            }, DispatcherPriority.Render);
        }
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
        _secondaryEditorSyncedFilePaths.Remove(pane.Id);
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

    private void ApplyEditorDisplayMode(bool showYamlFrontMatterInEditor)
    {
        var mode = showYamlFrontMatterInEditor
            ? EditorDocumentDisplayMode.PlainText
            : EditorDocumentDisplayMode.Markdown;

        _editorHost.SetDocumentDisplayMode(mode);
        foreach (var host in _secondaryEditorHosts.Values)
        {
            host.SetDocumentDisplayMode(mode);
        }
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

        if (!e.Handled
            && e.Key == Key.Escape
            && e.KeyModifiers == KeyModifiers.None
            && !vm.IsNotePickerOpen
            && !vm.IsTitleSuggestionsOpen
            && !vm.IsTagSuggestionsOpen
            && vm.ClearAdditionalSidebarSelection())
        {
            NotesListBox.SelectedItems?.Clear();
            NotesListBox.SelectedItem = vm.SelectedSidebarRow;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F6
            && (e.KeyModifiers == KeyModifiers.None || e.KeyModifiers == KeyModifiers.Shift))
        {
            e.Handled = true;
            CycleMainFocus(reverse: e.KeyModifiers == KeyModifiers.Shift);
            return;
        }

        var suppressUnmodifiedGlobalShortcut = e.KeyModifiers == KeyModifiers.None && IsTextInputSource(e.Source);
        if (!suppressUnmodifiedGlobalShortcut)
        {
            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.OpenSettings, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                await vm.OpenSettingsCommand.ExecuteAsync(null);
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ShowShortcuts, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                await vm.ShowKeyboardShortcutsHelpCommand.ExecuteAsync(null);
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleYaml, e.Key, e.KeyModifiers)
                && vm.ToggleYamlFrontMatterVisibilityCommand.CanExecute(null))
            {
                e.Handled = true;
                await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
                return;
            }

            if (TryHandleWorkspacePresentationShortcut(vm, e))
            {
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.EqualizePanes, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                EqualizePaneWidthsToActivePane(vm);
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ReloadNotes, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                await vm.ReloadCommand.ExecuteAsync(null);
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.NewNote, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                await vm.NewNoteCommand.ExecuteAsync(null);
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.NewNoteWindow, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                await InvokeOpenNewNoteInWindowAsync();
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.OpenNotePicker, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                vm.OpenNotePickerCommand.Execute(null);
                return;
            }

            if (!vm.IsNotePickerOpen
                && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.DeleteNote, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                await vm.DeleteCurrentNoteCommand.ExecuteAsync(null);
                return;
            }

            if (vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ClosePane, e.Key, e.KeyModifiers))
            {
                e.Handled = true;
                if (IsStandaloneWindow)
                {
                    await RequestCloseAsync();
                }
                else
                {
                    await vm.CloseActivePaneAsync();
                }
                return;
            }
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
    }

    private static bool IsTextInputSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return visual is TextBox or TextEditor
               || visual.GetVisualAncestors().Any(ancestor => ancestor is TextBox or TextEditor);
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

    private async void OnNotePickerSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (IsOpenNoteInZenWindowGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await OpenSelectedPickerNoteInWindowAsync(vm, NoteWindowMode.Zen);
        }
        else if (IsOpenNoteInNewWindowGesture(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await OpenSelectedPickerNoteInWindowAsync(vm, NoteWindowMode.Standard);
        }
        else if (e.Key == Key.Down)
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

    private async Task CopyCodeBlockAsync(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            vm.StatusMessage = "System clipboard is not available.";
            return;
        }

        try
        {
            await ClipboardTextService.SetTextAsync(topLevel.Clipboard, code);
            vm.StatusMessage = "Copied code block";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Could not copy code block: {ex.Message}";
        }
    }

    private async Task CutEditorSelectionAsync(TextEditor editor)
    {
        var selectedText = editor.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
        {
            return;
        }

        var host = GetEditorHost(editor);
        if (host.DoesSelectionTouchMarkdownTable && !host.CanDeleteMarkdownTableSelection)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            return;
        }

        await ClipboardTextService.SetTextAsync(topLevel.Clipboard, selectedText);
        if (host.DoesSelectionTouchMarkdownTable)
        {
            host.DeleteMarkdownTableSelection();
            return;
        }

        editor.SelectedText = string.Empty;
    }

    private async Task PasteIntoEditorAsync(TextEditor editor)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            editor.Paste();
            return;
        }

        var host = GetEditorHost(editor);
#pragma warning disable CS0618 // Compatibility fallback for Avalonia clipboard text API.
        var clipboardText = await topLevel.Clipboard.GetTextAsync();
#pragma warning restore CS0618
        if (clipboardText is not null)
        {
            if (host.ShouldHandleMarkdownTablePaste)
            {
                host.TryInsertMarkdownTableText(clipboardText);
                return;
            }

            var formattedClipboard = MarkdownTableFormatter.FormatAllWithMetadata(clipboardText);
            if (formattedClipboard.ContainsTables)
            {
                ApplyEditorEdit(
                    editor,
                    BuildTableFragmentEdit(
                        editor.Document?.Text ?? string.Empty,
                        editor.SelectionStart,
                        editor.SelectionLength,
                        formattedClipboard));
                return;
            }
        }

        using var data = await topLevel.Clipboard.TryGetDataAsync();
        var bitmap = data is null ? null : await data.TryGetBitmapAsync();
        if (bitmap is null || DataContext is not MainViewModel vm || string.IsNullOrWhiteSpace(vm.NotesFolder))
        {
            editor.Paste();
            return;
        }

        var assetFileName = await _noteAssetService.SaveBitmapAsync(vm.NotesFolder, bitmap);
        var imageReference = _noteAssetService.BuildMarkdownImageReference(assetFileName);
        if (host.ShouldHandleMarkdownTablePaste)
        {
            host.TryInsertMarkdownTableText(imageReference);
            return;
        }

        InsertTextAtSelection(editor, imageReference);
    }

    private void InsertTextAtSelection(TextEditor editor, string text, int? caretOffsetWithinInsertedText = null)
    {
        var document = editor.Document;
        if (document is null)
        {
            return;
        }

        var selectionStart = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;
        document.Replace(selectionStart, selectionLength, text);
        var relativeCaretOffset = Math.Clamp(caretOffsetWithinInsertedText ?? text.Length, 0, text.Length);
        editor.Select(selectionStart + relativeCaretOffset, 0);
        editor.CaretOffset = selectionStart + relativeCaretOffset;
        editor.Focus();
        _slashCommandPopup.ScheduleRefresh();
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

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ShowShortcuts, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await vm.ShowKeyboardShortcutsHelpCommand.ExecuteAsync(null);
            return;
        }

        if (vm is not null
            && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleYaml, e.Key, e.KeyModifiers)
            && vm.ToggleYamlFrontMatterVisibilityCommand.CanExecute(null))
        {
            e.Handled = true;
            await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
            return;
        }

        if (vm is not null && TryHandleWorkspacePresentationShortcut(vm, e))
        {
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleTaskState, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var toggleTaskEdit = MarkdownEditingCommands.ToggleTaskState(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength);
            ApplyEditorEdit(
                textEditor,
                toggleTaskEdit.Length != 0 || toggleTaskEdit.Replacement.Length != 0
                    ? toggleTaskEdit
                    : MarkdownEditingCommands.InsertLineBelow(GetEditorText(textEditor), textEditor.CaretOffset));
            return;
        }

        if (_slashCommandPopup.HandleKeyDown(e, edit => ApplyEditorEdit(textEditor, edit)))
        {
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

        if (vm is not null
            && !vm.IsNotePickerOpen
            && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.DeleteNote, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            await vm.DeleteCurrentNoteCommand.ExecuteAsync(null);
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.C)
        {
            e.Handled = true;
            await CopyEditorSelectionAsync(textEditor);
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.X)
        {
            e.Handled = true;
            await CutEditorSelectionAsync(textEditor);
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.V)
        {
            e.Handled = true;
            await PasteIntoEditorAsync(textEditor);
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.Bold, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            ApplyEditorEdit(textEditor, BuildInlineMarkdownEdit(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, "**"));
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.Italic, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            ApplyEditorEdit(textEditor, BuildInlineMarkdownEdit(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, "*"));
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.InlineCode, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            ApplyEditorEdit(textEditor, BuildInlineMarkdownEdit(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, "`"));
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleCodeBlock, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (!host.DoesSelectionTouchMarkdownTable)
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleCodeBlock(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
            }
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.MoveLineUp, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (host.IsCaretInMarkdownTable)
            {
                host.MoveMarkdownTableRow(down: false);
            }
            else
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.MoveLines(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, moveDown: false));
            }
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.MoveLineDown, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (host.IsCaretInMarkdownTable)
            {
                host.MoveMarkdownTableRow(down: true);
            }
            else
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.MoveLines(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, moveDown: true));
            }
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.DeleteLine, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (host.IsCaretInMarkdownTable)
            {
                host.DeleteMarkdownTableRow();
            }
            else
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.DeleteCurrentLine(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
            }
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleTaskList, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (!host.DoesSelectionTouchMarkdownTable)
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleTaskList(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
            }
            return;
        }

        if (vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleBulletList, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (!host.DoesSelectionTouchMarkdownTable)
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleBulletList(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength));
            }
            return;
        }

        var headingLevel = vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.Heading1, e.Key, e.KeyModifiers) ? 1
            : vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.Heading2, e.Key, e.KeyModifiers) ? 2
            : vm is not null && vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.Heading3, e.Key, e.KeyModifiers) ? 3
            : 0;
        if (headingLevel != 0)
        {
            e.Handled = true;
            var host = GetEditorHost(textEditor);
            if (!host.DoesSelectionTouchMarkdownTable)
            {
                ApplyEditorEdit(textEditor, MarkdownEditingCommands.ToggleHeading(GetEditorText(textEditor), textEditor.SelectionStart, textEditor.SelectionLength, headingLevel));
            }
            return;
        }

        if (e.Key != Key.Tab)
        {
            return;
        }

        if (GetEditorHost(textEditor).IsCaretInMarkdownTable)
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

    private void OnWindowPointerMoved(object? sender, PointerEventArgs e)
    {
        _windowChrome.OnWindowPointerMoved(e);
        UpdateZenWindowMoveCursor(e);
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e) => _windowChrome.OnWindowPointerExited();

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _windowChrome.OnWindowPointerPressed(e);
        if (e.Handled || !_isZenMode)
        {
            return;
        }

        if (IsZenWindowDragGesture(Bounds.Size, e.GetPosition(this), e.KeyModifiers))
        {
            _windowChrome.OnWindowDragPointerPressed(e);
        }
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
        if (DataContext is not MainViewModel vm
            || sender is not TextBox textBox
            || GetSidebarNoteItem(textBox.DataContext) is not { } noteItem)
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
        if (DataContext is not MainViewModel vm
            || sender is not TextBox textBox
            || GetSidebarNoteItem(textBox.DataContext) is not { IsRenaming: true } noteItem)
        {
            return;
        }

        await vm.CommitRenameAsync(noteItem);
    }

    private static NoteListItemViewModel? GetSidebarNoteItem(object? dataContext)
    {
        return dataContext switch
        {
            NoteListItemViewModel note => note,
            SidebarTreeRowViewModel row => row.Note,
            _ => null
        };
    }

    private async void OnTitleSuggestionsContextTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!vm.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.GenerateTitleSuggestions, e.Key, e.KeyModifiers))
        {
            return;
        }

        e.Handled = true;
        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);
    }

    internal static MarkdownEditResult BuildInlineMarkdownEdit(string text, int selectionStart, int selectionLength, string marker)
    {
        var rawEdit = MarkdownEditingCommands.ToggleWrap(text, selectionStart, selectionLength, marker);
        if (MarkdownTableEditingCommands.TryAdaptCellEdit(text, rawEdit, out var tableEdit))
        {
            return tableEdit;
        }

        return MarkdownTableEditingCommands.DoesRangeTouchTable(text, rawEdit.Start, rawEdit.Length)
            ? new MarkdownEditResult(rawEdit.Start, 0, string.Empty, rawEdit.Start, 0)
            : rawEdit;
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

        EditorHostController? host = null;
        var textEditor = sender as TextEditor;
        if (sender is StyledElement { DataContext: EditorPaneViewModel pane })
        {
            SetSecondaryPaneActive(pane);
            _secondaryEditorHosts.TryGetValue(pane.Id, out host);
        }
        else
        {
            ActivatePrimaryPane();
            host = _editorHost;
        }

        if (textEditor is not null
            && host is not null
            && TryOpenImageViewerFromEditorClick(textEditor, host, e))
        {
            e.Handled = true;
            return;
        }

        _slashCommandPopup.ScheduleRefresh(DispatcherPriority.Input);
    }

    private bool TryOpenImageViewerFromEditorClick(TextEditor editor, EditorHostController host, PointerPressedEventArgs e)
    {
        var hit = host.TryHitTestImagePreview(e.GetPosition(editor.TextArea.TextView));
        if (hit is null)
        {
            return false;
        }

        return OpenImageViewer(editor, hit.Value);
    }

    private bool OpenImageViewer(TextEditor editor, MarkdownImagePreviewHitTestResult hit)
    {
        return ImageViewerWindow.TryOpen(
            this,
            hit.ResolvedPath,
            (currentImagePath, annotatedBitmap, overwrite) =>
                SaveAnnotatedImageAsync(editor, hit, currentImagePath, annotatedBitmap, overwrite),
            CopyAnnotatedImageToClipboardAsync);
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
