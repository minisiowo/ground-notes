using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class MainWindow
{
    private static readonly Cursor s_zenWindowMoveCursor = new(StandardCursorType.SizeAll);
    private bool _isZenMode;
    private bool _closeApproved;
    private bool _closeInProgress;
    private double _sidebarWidthBeforeZenMode = 300;
    private double? _zenEditorCanvasPreferredWidth;

    internal bool IsZenMode => _isZenMode;

    public void ConfigureAsStandaloneWindow(NoteWindowMode launchMode)
    {
        _standaloneLaunchMode = launchMode;
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        _sidebarWidthBeforeCollapse = Math.Max(SidebarCol.Width.Value, SidebarMinWidth);
        viewModel.SidebarCollapsed = true;
        SidebarCol.MinWidth = 0;
        SidebarCol.Width = new GridLength(0, GridUnitType.Pixel);
        SplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
        SidebarBorder.Opacity = 0;
        SidebarBorder.IsVisible = false;
        UpdateWorkspaceHostMargin();
        ScheduleSidebarLayoutRefresh();

        if (launchMode == NoteWindowMode.Zen && !_isZenMode)
        {
            EnterZenMode();
        }
    }

    private async Task RequestCloseAsync()
    {
        if (_closeApproved || _closeInProgress)
        {
            return;
        }

        _closeInProgress = true;
        try
        {
            if (DataContext is not MainViewModel viewModel || await viewModel.PrepareToCloseAsync())
            {
                _closeApproved = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StatusMessage = $"Could not save note before closing: {ex.Message}";
            }
        }
        finally
        {
            _closeInProgress = false;
        }
    }

    internal void CloseAfterStartupFailure()
    {
        _closeApproved = true;
        Close();
    }

    public void ActivateAndFocusActiveEditor()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible)
            {
                return;
            }

            Activate();
            GetActiveTextEditor().Focus();
        }, DispatcherPriority.Input);
    }

    private bool TryHandleWorkspacePresentationShortcut(MainViewModel viewModel, KeyEventArgs e)
    {
        if (viewModel.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleZenMode, e.Key, e.KeyModifiers))
        {
            e.Handled = true;
            ToggleZenMode();
            return true;
        }

        if (!viewModel.KeyboardShortcuts.Matches(KeyboardShortcutActionIds.ToggleSidebar, e.Key, e.KeyModifiers)
            || !viewModel.ToggleSidebarCommand.CanExecute(null))
        {
            return false;
        }

        e.Handled = true;
        if (!_isZenMode)
        {
            viewModel.ToggleSidebarCommand.Execute(null);
        }
        return true;
    }

    private void ToggleZenMode()
    {
        if (_standaloneLaunchMode == NoteWindowMode.Zen)
        {
            return;
        }

        if (_isZenMode)
        {
            ExitZenMode();
            return;
        }

        EnterZenMode();
    }

    private void EnterZenMode()
    {
        if (_isZenMode || DataContext is not MainViewModel vm)
        {
            return;
        }

        var sidebarAnimationInProgress = _sidebarAnimationCts is not null;
        _sidebarAnimationCts?.Cancel();
        _sidebarAnimationCts?.Dispose();
        _sidebarAnimationCts = null;

        _sidebarWidthBeforeZenMode = vm.SidebarCollapsed || sidebarAnimationInProgress
            ? Math.Max(_sidebarWidthBeforeCollapse, SidebarMinWidth)
            : Math.Max(SidebarMinWidth, SidebarCol.Width.Value);
        if (!vm.SidebarCollapsed)
        {
            _sidebarWidthBeforeCollapse = _sidebarWidthBeforeZenMode;
        }

        CloseTransientZenOverlays(vm);

        _zenEditorCanvasPreferredWidth = null;
        _isZenMode = true;
        Classes.Set("zenMode", true);
        SetTitleBarVisible(isVisible: false);

        SidebarCol.MinWidth = 0;
        SidebarCol.Width = new GridLength(0, GridUnitType.Pixel);
        SplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
        SidebarBorder.Opacity = 0;
        SidebarBorder.IsVisible = false;

        EditorCanvasHost.Width = double.NaN;
        EditorCanvasHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        _lastAppliedEditorCanvasWidth = null;
        UpdateWorkspaceHostMargin();
        UpdateEditorCanvasWidth();
        ScheduleSidebarLayoutRefresh();
        FocusActiveEditorAfterLayout(expectedZenMode: true);
    }

    private void ExitZenMode()
    {
        if (!_isZenMode || DataContext is not MainViewModel vm)
        {
            return;
        }

        _isZenMode = false;
        _zenEditorCanvasPreferredWidth = null;
        Classes.Set("zenMode", false);
        SetTitleBarVisible(isVisible: true);

        RestoreSidebarAfterZenMode(vm);
        PrimaryPaneRoot.IsVisible = true;
        foreach (var paneRoot in _secondaryPaneRoots.Values)
        {
            paneRoot.IsVisible = true;
        }

        EditorCanvasHost.Width = double.NaN;
        EditorCanvasHost.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        _lastAppliedEditorCanvasWidth = null;
        UpdateWorkspaceHostMargin();
        UpdateWorkspacePresentation();
        ScheduleSidebarLayoutRefresh();
        FocusActiveEditorAfterLayout(expectedZenMode: false);
    }

    private void CloseTransientZenOverlays(MainViewModel vm)
    {
        if (vm.CloseTitleSuggestionsCommand.CanExecute(null))
        {
            vm.CloseTitleSuggestionsCommand.Execute(null);
        }

        if (vm.CloseNotePickerCommand.CanExecute(null))
        {
            vm.CloseNotePickerCommand.Execute(null);
        }

        vm.DismissTagSuggestions();
        _slashCommandPopup.Close();
    }

    private void RestoreSidebarAfterZenMode(MainViewModel vm)
    {
        if (vm.SidebarCollapsed)
        {
            SidebarCol.MinWidth = 0;
            SidebarCol.Width = new GridLength(0, GridUnitType.Pixel);
            SplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
            SidebarBorder.Opacity = 0;
            SidebarBorder.IsVisible = false;
            return;
        }

        _sidebarWidthBeforeCollapse = Math.Max(_sidebarWidthBeforeZenMode, SidebarMinWidth);
        SidebarCol.MinWidth = SidebarMinWidth;
        SidebarCol.Width = new GridLength(_sidebarWidthBeforeCollapse, GridUnitType.Pixel);
        SplitterCol.Width = new GridLength(SidebarSplitterWidth, GridUnitType.Pixel);
        SidebarBorder.Opacity = 1;
        SidebarBorder.IsVisible = true;
    }

    private bool TryUpdateZenWorkspacePresentation(MainViewModel vm)
    {
        if (!_isZenMode)
        {
            return false;
        }

        UpdateWorkspaceHostMargin();
        PaneWorkspaceContent.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        PaneWorkspaceScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

        var activeSecondaryPane = vm.ActiveSecondaryPane;
        var activeSecondaryPaneId = activeSecondaryPane?.Id;
        PrimaryPaneRoot.IsVisible = IsPaneVisibleInZenMode(activeSecondaryPaneId, paneId: null);
        foreach (var pair in _secondaryPaneRoots)
        {
            pair.Value.IsVisible = IsPaneVisibleInZenMode(activeSecondaryPaneId, pair.Key);
        }

        var availableWidth = CalculateZenPaneWidth(Bounds.Width, WorkspaceHost.Margin);
        var zenPaneWidth = CalculateZenEditorWidth(availableWidth, _zenEditorCanvasPreferredWidth);
        if (zenPaneWidth <= 0)
        {
            return true;
        }
        if (activeSecondaryPane is not null
            && _secondaryPaneRoots.TryGetValue(activeSecondaryPane.Id, out var activePaneRoot))
        {
            activePaneRoot.Width = zenPaneWidth;
        }
        else
        {
            PrimaryPaneRoot.Width = zenPaneWidth;
        }

        return true;
    }

    private bool TryUpdateZenEditorCanvasWidth()
    {
        if (!_isZenMode)
        {
            return false;
        }

        EditorCanvasHost.Width = double.NaN;
        _lastAppliedEditorCanvasWidth = null;
        return true;
    }

    private double GetSidebarWidthForLayout(MainViewModel? vm)
    {
        return ResolveSidebarWidthForLayout(
            _isZenMode,
            _sidebarWidthBeforeZenMode,
            vm?.SidebarCollapsed == true,
            _sidebarWidthBeforeCollapse,
            SidebarCol.Width.Value);
    }

    internal static bool IsPaneVisibleInZenMode(Guid? activeSecondaryPaneId, Guid? paneId)
    {
        return activeSecondaryPaneId == paneId;
    }

    private void UpdateZenWindowMoveCursor(PointerEventArgs e)
    {
        if (!_isZenMode)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (WindowChromeController.TryGetResizeEdge(Bounds.Size, point) is null
            && IsZenWindowDragGesture(Bounds.Size, point, e.KeyModifiers))
        {
            Cursor = s_zenWindowMoveCursor;
        }
    }

    internal static double CalculateZenPaneWidth(double windowWidth, Thickness workspaceMargin)
    {
        var availableWidth = Math.Max(0, windowWidth - workspaceMargin.Left - workspaceMargin.Right);
        return GetSinglePaneFitWidth(availableWidth);
    }

    internal static double CalculateZenEditorWidth(double availableWidth, double? preferredWidth)
    {
        if (availableWidth <= 0)
        {
            return 0;
        }

        return preferredWidth is > 0
            ? Math.Min(preferredWidth.Value, availableWidth)
            : availableWidth;
    }

    internal static bool IsZenWindowDragGesture(Size bounds, Point point, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            return true;
        }

        if (point.X < 0 || point.Y < 0 || point.X > bounds.Width || point.Y > bounds.Height)
        {
            return false;
        }

        return point.X <= EditorOuterGutter
               || point.Y <= EditorOuterGutter
               || point.X >= bounds.Width - EditorOuterGutter
               || point.Y >= bounds.Height - EditorOuterGutter;
    }

    internal static double ResolveSidebarWidthForLayout(
        bool isZenMode,
        double sidebarWidthBeforeZenMode,
        bool sidebarCollapsed,
        double sidebarWidthBeforeCollapse,
        double currentSidebarWidth)
    {
        if (isZenMode)
        {
            return sidebarWidthBeforeZenMode;
        }

        return sidebarCollapsed ? sidebarWidthBeforeCollapse : currentSidebarWidth;
    }

    private void FocusActiveEditorAfterLayout(bool expectedZenMode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isZenMode == expectedZenMode)
            {
                GetActiveTextEditor().Focus();
            }
        }, DispatcherPriority.Render);
    }
}
