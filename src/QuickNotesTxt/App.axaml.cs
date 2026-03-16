using Avalonia;
using Avalonia.Controls;
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
            var savedLayout = settingsService.GetWindowLayoutSync();
            var repository = new NotesRepository();
            var fileWatcher = new FileWatcherService();
            var themeLoader = new ThemeLoaderService();
            var fontCatalog = new FontCatalogService();
            var aiPromptCatalog = new AiPromptCatalogService();
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            var aiTextActionService = new OpenAiTextActionService(httpClient);
            var mainViewModel = new MainViewModel(repository, settingsService, fileWatcher, themeLoader, fontCatalog, aiPromptCatalog, aiTextActionService);

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel,
                Opacity = 0
            };
            mainWindow.SetSettingsService(settingsService);

            // Apply saved layout synchronously before the window is shown,
            // so it appears at the correct position and size immediately.
            if (savedLayout is not null)
            {
                mainWindow.Position = new PixelPoint((int)savedLayout.X, (int)savedLayout.Y);
                mainWindow.Width = savedLayout.Width;
                mainWindow.Height = savedLayout.Height;
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
