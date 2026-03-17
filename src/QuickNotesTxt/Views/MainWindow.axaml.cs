using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Document;
using QuickNotesTxt.Editors;
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
    private readonly MenuFlyout _editorContextFlyout = new();
    private readonly MarkdownColorizingTransformer _markdownColorizer = new();
    private MarkdownSlashTrigger? _activeSlashTrigger;
    private IReadOnlyList<MarkdownSlashCommand> _activeSlashCommands = [];
    private bool _isSlashPopupRefreshQueued;
    private bool _isSlashPopupPositionUpdateQueued;
    private bool _isUpdatingEditorFromViewModel;
    private bool _isUpdatingViewModelFromEditor;

    public MainWindow()
    {
        InitializeComponent();

        PointerMoved += OnWindowPointerMoved;
        PointerExited += OnWindowPointerExited;

        // Use Tunnel routing so corner resize takes priority over title-bar buttons.
        AddHandler(PointerPressedEvent, OnWindowPointerPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        EditorTextEditor.AddHandler(KeyDownEvent, OnEditorKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        EditorTextEditor.PointerPressed += OnEditorPointerPressed;
        EditorTextEditor.ContextRequested += OnEditorContextRequested;
        EditorTextEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
        EditorTextEditor.TextArea.TextView.ScrollOffsetChanged += OnEditorTextViewScrollOffsetChanged;
        EditorTextEditor.TextArea.TextView.VisualLinesChanged += OnEditorTextViewVisualLinesChanged;
        SlashCommandPopup.PlacementTarget = EditorBorder;
        SlashCommandPopup.Placement = PlacementMode.AnchorAndGravity;
        EditorTextEditor.Options.ConvertTabsToSpaces = false;
        EditorTextEditor.Options.EnableRectangularSelection = false;
        EditorTextEditor.Options.WordWrapIndentation = 0;
        EditorTextEditor.Options.InheritWordWrapIndentation = false;
        EditorTextEditor.TextArea.TextView.LineTransformers.Add(_markdownColorizer);
        ApplyEditorSelectionTheme();
        EditorTextEditor.TextChanged += OnEditorTextChanged;
        RebuildEditorContextFlyout();

        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PickFolderAsync = PickFolderAsync;
                vm.ConfirmDeleteAsync = ConfirmDeleteAsync;
                vm.ShowSettingsAsync = ShowSettingsAsync;
                vm.PropertyChanged += OnViewModelPropertyChanged;
                vm.FocusEditorRequested += OnFocusEditorRequested;
                SyncEditorText(vm.EditorBody);
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

            ScheduleSlashCommandPopupPositionUpdate();
        };

        SizeChanged += (_, _) => ScheduleSlashCommandPopupPositionUpdate();
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
        Avalonia.Threading.Dispatcher.UIThread.Post(() => EditorTextEditor.Focus(), Avalonia.Threading.DispatcherPriority.Input);
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

        if (e.PropertyName is nameof(MainViewModel.AiPrompts)
            or nameof(MainViewModel.IsAiBusy)
            or nameof(MainViewModel.SelectedAiModel))
        {
            RebuildEditorContextFlyout();
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.SelectedThemeName)
            or nameof(MainViewModel.SelectedCodeFontFamilyName)
            or nameof(MainViewModel.SelectedCodeFontVariantName))
        {
            _markdownColorizer.InvalidateResourceCache();
            ApplyEditorSelectionTheme();
            EditorTextEditor.TextArea.TextView.InvalidateVisual();
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.EditorBody))
        {
            SyncEditorText(vm.EditorBody);
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
                EditorTextEditor.Focus();
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

    private async Task<SettingsDialogModel?> ShowSettingsAsync(SettingsDialogModel model)
    {
        var dialog = new SettingsWindow(model);
        dialog.PreviewSettingsAsync = previewModel =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ApplySettingsPreview(previewModel);
            }

            return Task.CompletedTask;
        };

        return await dialog.ShowDialog<SettingsDialogModel?>(this);
    }

    private void RebuildEditorContextFlyout()
    {
        _editorContextFlyout.Items.Clear();
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Cut", EditorTextEditor.CanCut, async (_, _) => await CutEditorSelectionAsync()));
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Copy", EditorTextEditor.CanCopy, async (_, _) => await CopyEditorSelectionAsync()));
        _editorContextFlyout.Items.Add(CreateEditorMenuItem("Paste", EditorTextEditor.CanPaste, (_, _) => EditorTextEditor.Paste()));
        _editorContextFlyout.Items.Add(new Separator());
        AddAiMenuSection();
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

        _editorContextFlyout.Items.Add(new Separator());

        var reloadItem = new MenuItem
        {
            Header = "Reload Prompts",
            IsEnabled = !vm.IsAiBusy
        };
        reloadItem.Click += async (_, _) => await vm.ReloadAiPromptsCommand.ExecuteAsync(null);
        _editorContextFlyout.Items.Add(reloadItem);

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

    private void ApplyEditorSelectionTheme()
    {
        var resources = Application.Current?.Resources;
        EditorTextEditor.TextArea.SelectionBrush = resources?[QuickNotesTxt.Styles.ThemeKeys.EditorTextSelectionBrush] as IBrush;
        EditorTextEditor.TextArea.SelectionForeground = null;
        EditorTextEditor.TextArea.SelectionBorder = null;
        EditorTextEditor.TextArea.SelectionCornerRadius = 0;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditorFromViewModel || DataContext is not MainViewModel vm)
        {
            return;
        }

        var text = EditorTextEditor.Document?.Text ?? string.Empty;
        if (string.Equals(vm.EditorBody, text, StringComparison.Ordinal))
        {
            return;
        }

        var hadCurrentNote = vm.CurrentNote is not null;

        _isUpdatingViewModelFromEditor = true;
        try
        {
            vm.EditorBody = text;
        }
        finally
        {
            _isUpdatingViewModelFromEditor = false;
        }

        if (!hadCurrentNote && vm.CurrentNote is not null)
        {
            Dispatcher.UIThread.Post(
                ScheduleSlashCommandPopupRefresh,
                DispatcherPriority.Background);
            return;
        }

        ScheduleSlashCommandPopupRefresh();
    }

    private void SyncEditorText(string text)
    {
        if (_isUpdatingViewModelFromEditor)
        {
            return;
        }

        var document = EditorTextEditor.Document;
        if (document is null)
        {
            return;
        }

        text ??= string.Empty;
        if (string.Equals(document.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        var caretOffset = Math.Min(EditorTextEditor.CaretOffset, text.Length);

        _isUpdatingEditorFromViewModel = true;
        try
        {
            document.BeginUpdate();
            document.Replace(0, document.TextLength, text);
            document.EndUpdate();
            EditorTextEditor.CaretOffset = caretOffset;
            EditorTextEditor.Select(caretOffset, 0);
        }
        finally
        {
            _isUpdatingEditorFromViewModel = false;
        }

        ScheduleSlashCommandPopupRefresh();
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key is Key.OemPeriod or Key.Decimal)
        {
            e.Handled = true;
            await vm.OpenSettingsCommand.ExecuteAsync(null);
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
        if (sender is not TextEditor textEditor)
        {
            return;
        }

        var vm = DataContext as MainViewModel;

        if (HandleSlashCommandKeyDown(e))
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
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
        var selEnd = selStart + textEditor.SelectionLength;
        var hasSelection = selStart != selEnd;
        var isUnindent = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (!hasSelection && !isUnindent)
        {
            var caretOffset = textEditor.CaretOffset;
            document.Insert(caretOffset, "    ");
            textEditor.CaretOffset = caretOffset + 4;
            textEditor.Select(textEditor.CaretOffset, 0);
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

        document.Replace(lineStart, lineEnd - lineStart, newBlock);

        var newSelStart = Math.Max(lineStart, selStart + firstLineDelta);
        var newSelEnd = Math.Max(newSelStart, selEnd + totalDelta);
        Dispatcher.UIThread.Post(() =>
        {
            textEditor.Select(newSelStart, newSelEnd - newSelStart);
            textEditor.CaretOffset = newSelEnd;
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
            || visual.FindAncestorOfType<TextEditor>() is not null
            || visual.FindAncestorOfType<TextArea>() is not null
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

    private string GetEditorText() => EditorTextEditor.Document?.Text ?? string.Empty;

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
        ScheduleSlashCommandPopupRefresh();
    }

    private bool HandleSlashCommandKeyDown(KeyEventArgs e)
    {
        if (!SlashCommandPopup.IsOpen)
        {
            return false;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            MoveSlashCommandSelection(1);
            return true;
        }

        if (e.Key == Key.Up)
        {
            e.Handled = true;
            MoveSlashCommandSelection(-1);
            return true;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ApplySelectedSlashCommand();
            return true;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseSlashCommandPopup();
            return true;
        }

        return false;
    }

    private void UpdateSlashCommandPopup()
    {
        var trigger = MarkdownSlashCommandCatalog.TryGetTrigger(GetEditorText(), EditorTextEditor.CaretOffset);
        if (trigger is null)
        {
            CloseSlashCommandPopup();
            return;
        }

        var commands = MarkdownSlashCommandCatalog.Filter(trigger.Value.Query);
        if (commands.Count == 0)
        {
            CloseSlashCommandPopup();
            return;
        }

        var wasVisible = SlashCommandPopup.IsOpen;
        _activeSlashTrigger = trigger;
        _activeSlashCommands = commands;
        SlashCommandPopup.IsOpen = true;
        SlashCommandListBox.ItemsSource = commands;
        SlashCommandListBox.SelectedItem = commands[0];
        SlashCommandHintText.Text = string.IsNullOrWhiteSpace(trigger.Value.Query)
            ? "Formatting commands"
            : $"/{trigger.Value.Query}";
        EditorTextEditor.Focus();
        ScheduleSlashCommandPopupPositionUpdate(!wasVisible);
    }

    private void CloseSlashCommandPopup()
    {
        _activeSlashTrigger = null;
        _activeSlashCommands = [];
        SlashCommandPopup.IsOpen = false;
        SlashCommandListBox.ItemsSource = null;
        SlashCommandListBox.SelectedItem = null;
        EditorTextEditor.Focus();
    }

    private void MoveSlashCommandSelection(int delta)
    {
        if (_activeSlashCommands.Count == 0)
        {
            return;
        }

        var currentIndex = SlashCommandListBox.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        currentIndex = (currentIndex + delta + _activeSlashCommands.Count) % _activeSlashCommands.Count;
        SlashCommandListBox.SelectedIndex = currentIndex;
        if (SlashCommandListBox.SelectedItem is not null)
        {
            SlashCommandListBox.ScrollIntoView(SlashCommandListBox.SelectedItem);
        }
    }

    private void ApplySelectedSlashCommand()
    {
        if (_activeSlashTrigger is not { } trigger || SlashCommandListBox.SelectedItem is not MarkdownSlashCommand command)
        {
            CloseSlashCommandPopup();
            return;
        }

        var document = EditorTextEditor.Document;
        if (document is null)
        {
            CloseSlashCommandPopup();
            return;
        }

        document.Replace(trigger.Start, trigger.Length, string.Empty);
        EditorTextEditor.CaretOffset = trigger.Start;
        EditorTextEditor.Select(trigger.Start, 0);
        var commandOffset = trigger.Start;

        var edit = command.Action switch
        {
            SlashCommandAction.Bold => MarkdownEditingCommands.ToggleWrap(document.Text, commandOffset, 0, "**"),
            SlashCommandAction.Italic => MarkdownEditingCommands.ToggleWrap(document.Text, commandOffset, 0, "*"),
            SlashCommandAction.InlineCode => MarkdownEditingCommands.ToggleWrap(document.Text, commandOffset, 0, "`"),
            SlashCommandAction.CodeBlock => MarkdownEditingCommands.ToggleCodeBlock(document.Text, commandOffset, 0),
            SlashCommandAction.TaskList => MarkdownEditingCommands.ToggleTaskList(document.Text, commandOffset, 0),
            SlashCommandAction.BulletList => MarkdownEditingCommands.ToggleBulletList(document.Text, commandOffset, 0),
            SlashCommandAction.Heading1 => MarkdownEditingCommands.ToggleHeading(document.Text, commandOffset, 0, 1),
            SlashCommandAction.Heading2 => MarkdownEditingCommands.ToggleHeading(document.Text, commandOffset, 0, 2),
            SlashCommandAction.Heading3 => MarkdownEditingCommands.ToggleHeading(document.Text, commandOffset, 0, 3),
            _ => default
        };

        CloseSlashCommandPopup();
        ApplyEditorEdit(edit);
    }

    private void OnSlashCommandListBoxDoubleTapped(object? sender, RoutedEventArgs e)
    {
        ApplySelectedSlashCommand();
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ScheduleSlashCommandPopupRefresh(DispatcherPriority.Input);
    }

    private void ScheduleSlashCommandPopupRefresh()
    {
        ScheduleSlashCommandPopupRefresh(DispatcherPriority.Render);
    }

    private void ScheduleSlashCommandPopupRefresh(DispatcherPriority priority)
    {
        if (_isSlashPopupRefreshQueued)
        {
            return;
        }

        _isSlashPopupRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isSlashPopupRefreshQueued = false;
            UpdateSlashCommandPopup();
        }, priority);
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        ScheduleSlashCommandPopupPositionUpdate();
    }

    private void OnEditorTextViewScrollOffsetChanged(object? sender, EventArgs e)
    {
        ScheduleSlashCommandPopupPositionUpdate();
    }

    private void OnEditorTextViewVisualLinesChanged(object? sender, EventArgs e)
    {
        ScheduleSlashCommandPopupPositionUpdate();
    }

    private void ScheduleSlashCommandPopupPositionUpdate(bool resetPlacement = false)
    {
        if (!SlashCommandPopup.IsOpen || _isSlashPopupPositionUpdateQueued)
        {
            return;
        }

        _isSlashPopupPositionUpdateQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isSlashPopupPositionUpdateQueued = false;
            UpdateSlashCommandPopupPosition(resetPlacement);
        }, DispatcherPriority.Render);
    }

    private void UpdateSlashCommandPopupPosition(bool resetPlacement)
    {
        if (!SlashCommandPopup.IsOpen)
        {
            return;
        }

        try
        {
            var caretRect = EditorTextEditor.TextArea.Caret.CalculateCaretRectangle();
            var popupTopLeft = EditorTextEditor.TextArea.TextView.TranslatePoint(
                new Point(caretRect.X, caretRect.Y),
                EditorBorder);

            if (popupTopLeft is null)
            {
                return;
            }

            if (EditorBorder.Bounds.Width <= 0 || EditorBorder.Bounds.Height <= 0)
            {
                return;
            }

            const double edgePadding = 12;
            const double horizontalPadding = 4;
            const double verticalPadding = 8;
            const double preferredPopupWidth = 300;
            const double minPopupWidth = 220;
            const double preferredListHeight = 180;
            const double minListHeight = 72;
            const double popupChromeHeight = 44;

            var availableWidth = Math.Max(minPopupWidth, EditorBorder.Bounds.Width - (edgePadding * 2));
            var popupWidth = Math.Clamp(preferredPopupWidth, minPopupWidth, availableWidth);
            SlashCommandPopupContent.Width = popupWidth;
            SlashCommandPopupContent.MaxWidth = popupWidth;

            var anchorLeft = popupTopLeft.Value.X;
            var anchorRight = popupTopLeft.Value.X + Math.Max(1, caretRect.Width);
            var anchorTop = popupTopLeft.Value.Y;
            var anchorBottom = popupTopLeft.Value.Y + Math.Max(1, caretRect.Height);

            var availableBelow = Math.Max(0, EditorBorder.Bounds.Height - anchorBottom - verticalPadding - edgePadding);
            var availableAbove = Math.Max(0, anchorTop - verticalPadding - edgePadding);
            var maxViewportHeight = Math.Max(minListHeight, Math.Max(availableBelow, availableAbove) - popupChromeHeight);
            var listMaxHeight = Math.Min(preferredListHeight, maxViewportHeight);
            SlashCommandListBox.MaxHeight = listMaxHeight;

            SlashCommandPopupContent.Measure(new Size(popupWidth, Math.Max(60, EditorBorder.Bounds.Height - (edgePadding * 2))));
            var popupHeight = Math.Min(EditorBorder.Bounds.Height - (edgePadding * 2), SlashCommandPopupContent.DesiredSize.Height);

            var availableRight = Math.Max(0, EditorBorder.Bounds.Width - anchorRight - horizontalPadding - edgePadding);
            var availableLeft = Math.Max(0, anchorLeft - horizontalPadding - edgePadding);

            PopupAnchor anchor;
            PopupGravity gravity;
            var horizontalOffset = horizontalPadding;
            var verticalOffset = verticalPadding;

            if (availableBelow >= popupHeight && availableRight >= popupWidth)
            {
                anchor = PopupAnchor.BottomRight;
                gravity = PopupGravity.BottomRight;
            }
            else if (availableBelow >= popupHeight && availableLeft >= popupWidth)
            {
                anchor = PopupAnchor.BottomLeft;
                gravity = PopupGravity.BottomLeft;
                horizontalOffset = -horizontalPadding;
            }
            else if (availableAbove >= popupHeight && availableRight >= popupWidth)
            {
                anchor = PopupAnchor.TopRight;
                gravity = PopupGravity.TopRight;
                verticalOffset = -verticalPadding;
            }
            else if (availableAbove >= popupHeight && availableLeft >= popupWidth)
            {
                anchor = PopupAnchor.TopLeft;
                gravity = PopupGravity.TopLeft;
                horizontalOffset = -horizontalPadding;
                verticalOffset = -verticalPadding;
            }
            else if (availableBelow >= availableAbove)
            {
                if (availableRight >= availableLeft)
                {
                    anchor = PopupAnchor.BottomRight;
                    gravity = PopupGravity.BottomRight;
                }
                else
                {
                    anchor = PopupAnchor.BottomLeft;
                    gravity = PopupGravity.BottomLeft;
                    horizontalOffset = -horizontalPadding;
                }
            }
            else if (availableRight >= availableLeft)
            {
                anchor = PopupAnchor.TopRight;
                gravity = PopupGravity.TopRight;
                verticalOffset = -verticalPadding;
            }
            else
            {
                anchor = PopupAnchor.TopLeft;
                gravity = PopupGravity.TopLeft;
                horizontalOffset = -horizontalPadding;
                verticalOffset = -verticalPadding;
            }

            SlashCommandPopup.PlacementAnchor = anchor;
            SlashCommandPopup.PlacementGravity = gravity;
            SlashCommandPopup.HorizontalOffset = horizontalOffset;
            SlashCommandPopup.VerticalOffset = verticalOffset;
            SlashCommandPopup.PlacementRect = new Rect(anchorLeft, anchorTop, Math.Max(1, caretRect.Width), Math.Max(1, caretRect.Height));
        }
        catch (InvalidOperationException)
        {
        }
    }
}
