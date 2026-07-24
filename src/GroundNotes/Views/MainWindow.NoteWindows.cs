using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class MainWindow
{
    private NoteWindowMode? _standaloneLaunchMode;

    private bool IsStandaloneWindow => _standaloneLaunchMode.HasValue;

    public Func<string, NoteWindowMode, Task>? OpenNoteInWindowAsync { get; set; }

    public Func<Task>? OpenNewNoteInWindowAsync { get; set; }

    public Action<NoteWindowMode, NoteWindowLayout>? SaveNoteWindowLayout { get; set; }

    internal static bool IsOpenNoteInNewWindowGesture(Key key, KeyModifiers modifiers) =>
        key == Key.Enter && (modifiers == KeyModifiers.Control || modifiers == KeyModifiers.Meta);

    internal static bool IsOpenNoteInZenWindowGesture(Key key, KeyModifiers modifiers) =>
        key == Key.Enter
        && (modifiers == (KeyModifiers.Control | KeyModifiers.Shift)
            || modifiers == (KeyModifiers.Meta | KeyModifiers.Shift));

    private async void OnOpenSidebarNoteInNewWindowClick(object? sender, RoutedEventArgs e)
    {
        await OpenNoteFromMenuAsync(sender, e, NoteWindowMode.Standard, closePicker: false);
    }

    private async void OnOpenSidebarNoteInZenWindowClick(object? sender, RoutedEventArgs e)
    {
        await OpenNoteFromMenuAsync(sender, e, NoteWindowMode.Zen, closePicker: false);
    }

    private async void OnOpenPickerNoteInNewWindowClick(object? sender, RoutedEventArgs e)
    {
        await OpenNoteFromMenuAsync(sender, e, NoteWindowMode.Standard, closePicker: true);
    }

    private async void OnOpenPickerNoteInZenWindowClick(object? sender, RoutedEventArgs e)
    {
        await OpenNoteFromMenuAsync(sender, e, NoteWindowMode.Zen, closePicker: true);
    }

    private async Task OpenNoteFromMenuAsync(
        object? sender,
        RoutedEventArgs e,
        NoteWindowMode mode,
        bool closePicker)
    {
        if (!TryGetNoteFilePath(sender, out var filePath))
        {
            return;
        }

        e.Handled = true;
        if (closePicker)
        {
            CloseNotePicker();
        }

        await InvokeOpenNoteInWindowAsync(filePath, mode);
    }

    private async Task OpenSelectedPickerNoteInWindowAsync(MainViewModel viewModel, NoteWindowMode mode)
    {
        var filePath = (viewModel.SelectedNotePickerSummary ?? viewModel.NotePickerResults.FirstOrDefault())?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        CloseNotePicker();
        await InvokeOpenNoteInWindowAsync(filePath, mode);
    }

    private void CloseNotePicker()
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CloseNotePickerCommand.Execute(null);
        }
    }

    private void SaveStandaloneNoteWindowLayout()
    {
        if (_standaloneLaunchMode is not { } mode || SaveNoteWindowLayout is null)
        {
            return;
        }

        var width = WindowState == WindowState.Maximized
            ? _lastNormalWidth ?? Width
            : Bounds.Width;
        var height = WindowState == WindowState.Maximized
            ? _lastNormalHeight ?? Height
            : Bounds.Height;
        SaveNoteWindowLayout(mode, new NoteWindowLayout(width, height));
    }

    private async Task InvokeOpenNewNoteInWindowAsync()
    {
        var callback = OpenNewNoteInWindowAsync;
        if (callback is null)
        {
            return;
        }

        try
        {
            await callback();
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StatusMessage = $"Could not open new note window: {ex.Message}";
            }
        }
    }

    private async Task InvokeOpenNoteInWindowAsync(string filePath, NoteWindowMode mode)
    {
        var callback = OpenNoteInWindowAsync;
        if (callback is null)
        {
            return;
        }

        try
        {
            await callback(filePath, mode);
        }
        catch (Exception ex)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StatusMessage = $"Could not open note: {ex.Message}";
            }
        }
    }

    private static bool TryGetNoteFilePath(object? sender, out string filePath)
    {
        filePath = (sender as MenuItem)?.CommandParameter as string ?? string.Empty;
        return !string.IsNullOrWhiteSpace(filePath);
    }
}
