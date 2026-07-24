using Avalonia;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

internal sealed class WorkspaceWindowManager
{
    private readonly Func<MainWindow, MainViewModel> _viewModelFactory;
    private readonly IEditorLayoutState _editorLayoutState;
    private readonly IWindowLayoutService _mainWindowLayoutService;
    private readonly SettingsNoteWindowLayoutService _noteWindowLayoutService;
    private int _noteWindowCascadeIndex;

    public WorkspaceWindowManager(
        Func<MainWindow, MainViewModel> viewModelFactory,
        IEditorLayoutState editorLayoutState,
        IWindowLayoutService mainWindowLayoutService,
        SettingsNoteWindowLayoutService noteWindowLayoutService)
    {
        _viewModelFactory = viewModelFactory;
        _editorLayoutState = editorLayoutState;
        _mainWindowLayoutService = mainWindowLayoutService;
        _noteWindowLayoutService = noteWindowLayoutService;
    }

    public MainWindow CreateMainWindow()
    {
        return CreateWorkspaceWindow(persistMainWindowLayout: true);
    }

    private MainWindow CreateWorkspaceWindow(bool persistMainWindowLayout)
    {
        var window = new MainWindow
        {
            Opacity = 0
        };
        try
        {
            window.SetEditorLayoutState(_editorLayoutState);

            var viewModel = _viewModelFactory(window);
            window.DataContext = viewModel;
            window.OpenNoteInWindowAsync = (filePath, mode) => OpenNoteWindowAsync(window, filePath, mode);
            window.SaveNoteWindowLayout = _noteWindowLayoutService.SaveLayout;
            if (persistMainWindowLayout)
            {
                window.SetWindowLayoutService(_mainWindowLayoutService);
            }

            return window;
        }
        catch
        {
            window.DisposeBeforeShow();
            throw;
        }
    }

    private async Task OpenNoteWindowAsync(
        MainWindow sourceWindow,
        string filePath,
        NoteWindowMode mode)
    {
        var sourceViewModel = sourceWindow.DataContext as MainViewModel;
        var sourceFolder = sourceViewModel?.NotesFolder;
        if (string.IsNullOrWhiteSpace(sourceFolder)
            || !File.Exists(filePath)
            || !MainViewModel.IsNotePathInNotesFolder(sourceFolder, filePath))
        {
            SetOpenError(sourceViewModel, "The note is outside the current notes folder or no longer exists.");
            return;
        }

        MainWindow? noteWindow = null;
        MainViewModel? noteViewModel = null;
        var wasShown = false;
        try
        {
            noteWindow = CreateWorkspaceWindow(persistMainWindowLayout: false);
            noteViewModel = (MainViewModel)noteWindow.DataContext!;
            await noteViewModel.InitializeForFolderAsync(sourceFolder);
            await noteViewModel.OpenNoteAsync(filePath, focusEditorWhenReady: false);
            if (noteViewModel.CurrentNote is null)
            {
                throw new InvalidOperationException("The note could not be loaded.");
            }

            noteWindow.Title = Path.GetFileNameWithoutExtension(filePath);
            ApplyPlacement(sourceWindow, noteWindow, mode);
            noteWindow.ShowActivated = false;
            noteWindow.ConfigureAsStandaloneWindow(mode);
            noteWindow.Show();
            wasShown = true;
            ThemeService.SyncScrollBarClassFromMainWindow(noteWindow);

            await noteWindow.CompleteStartupInitializationAsync(revealWindow: false);
            noteWindow.Opacity = 1;
            noteWindow.ShowActivated = true;
            noteWindow.ActivateAndFocusActiveEditor();
        }
        catch (Exception ex)
        {
            if (wasShown)
            {
                noteWindow!.CloseAfterStartupFailure();
            }
            else if (noteWindow is not null)
            {
                noteWindow.DisposeBeforeShow();
            }
            else
            {
                noteViewModel?.Dispose();
            }

            SetOpenError(sourceViewModel, ex.Message);
        }
    }

    private void ApplyPlacement(
        MainWindow sourceWindow,
        MainWindow noteWindow,
        NoteWindowMode mode)
    {
        var requestedLayout = _noteWindowLayoutService.GetLayout(mode)
            ?? NoteWindowPlacementCalculator.CreateDefaultLayout(
                mode,
                sourceWindow.Bounds.Width,
                sourceWindow.Bounds.Height);
        var screen = sourceWindow.Screens.ScreenFromWindow(sourceWindow) ?? sourceWindow.Screens.Primary;
        if (screen is null)
        {
            noteWindow.Width = requestedLayout.Width;
            noteWindow.Height = requestedLayout.Height;
            noteWindow.Position = new PixelPoint(sourceWindow.Position.X + 32, sourceWindow.Position.Y + 32);
            return;
        }

        var placement = NoteWindowPlacementCalculator.Calculate(
            screen.WorkingArea,
            screen.Scaling,
            sourceWindow.Position,
            requestedLayout,
            _noteWindowCascadeIndex++);
        noteWindow.Width = placement.Width;
        noteWindow.Height = placement.Height;
        noteWindow.Position = placement.Position;
    }

    private static void SetOpenError(MainViewModel? sourceViewModel, string detail)
    {
        if (sourceViewModel is not null)
        {
            sourceViewModel.StatusMessage = $"Could not open note: {detail}";
        }
    }
}
