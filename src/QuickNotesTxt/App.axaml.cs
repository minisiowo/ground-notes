using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QuickNotesTxt.Services;
using QuickNotesTxt.Styles;
using QuickNotesTxt.ViewModels;
using QuickNotesTxt.Views;

namespace QuickNotesTxt;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ThemeService.Apply(AppTheme.Dark);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new FolderSettingsService();
            var repository = new NotesRepository();
            var fileWatcher = new FileWatcherService();
            var mainViewModel = new MainViewModel(repository, settingsService, fileWatcher);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };
            mainWindow.SetSettingsService(settingsService);

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
