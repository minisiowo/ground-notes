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
    private ISettingsService? _settingsService;

    public MainWindow()
    {
        InitializeComponent();

        Opened += async (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PickFolderAsync = PickFolderAsync;
                vm.ConfirmDeleteAsync = ConfirmDeleteAsync;
            }

            await RestoreWindowLayoutAsync();
        };

        Closing += async (_, _) =>
        {
            await SaveWindowLayoutAsync();
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

    private async Task RestoreWindowLayoutAsync()
    {
        if (_settingsService is null) return;

        var layout = await _settingsService.GetWindowLayoutAsync();
        if (layout is null) return;

        // Validate that the saved position is on a visible screen area
        var screens = Screens;
        var savedBounds = new PixelRect(
            (int)layout.X, (int)layout.Y,
            (int)layout.Width, (int)layout.Height);

        bool isOnScreen = false;
        foreach (var screen in screens.All)
        {
            if (screen.WorkingArea.Intersects(savedBounds))
            {
                isOnScreen = true;
                break;
            }
        }

        if (isOnScreen)
        {
            Position = new PixelPoint((int)layout.X, (int)layout.Y);
        }

        Width = layout.Width;
        Height = layout.Height;

        if (layout.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async Task SaveWindowLayoutAsync()
    {
        if (_settingsService is null) return;

        var isMaximized = WindowState == WindowState.Maximized;

        // When maximized, save the restore bounds (normal size before maximizing)
        double width, height, x, y;

        if (isMaximized)
        {
            // Use the bounds the window had before being maximized
            // Avalonia doesn't expose RestoreBounds directly, so we save the current
            // size — but we track normal bounds via the last known normal state.
            // Fallback: save reasonable defaults from the XAML.
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

        var layout = new WindowLayout(width, height, x, y, isMaximized);
        await _settingsService.SetWindowLayoutAsync(layout);
    }

    private double? _lastNormalWidth;
    private double? _lastNormalHeight;
    private double? _lastNormalX;
    private double? _lastNormalY;

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

        if (e.Key is Key.Add or Key.OemPlus)
        {
            e.Handled = true;
            await vm.IncreaseEditorFontSizeCommand.ExecuteAsync(null);
        }
        else if (e.Key is Key.Subtract or Key.OemMinus)
        {
            e.Handled = true;
            await vm.DecreaseEditorFontSizeCommand.ExecuteAsync(null);
        }
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
