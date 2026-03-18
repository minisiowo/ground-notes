using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QuickNotesTxt.Services;
using QuickNotesTxt.ViewModels;
using QuickNotesTxt.Views;

namespace QuickNotesTxt;

public partial class App : Application
{
    private readonly IAppAppearanceService _appearanceService = new AppAppearanceService();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new FolderSettingsService();
            var fontCatalog = new FontCatalogService();
            var startupStateService = new StartupStateService(settingsService, fontCatalog);
            var startup = startupStateService.Load();
            ApplyStartupAppearance(startup);
            var windowLayoutService = new SettingsWindowLayoutService(settingsService);
            var mainWindow = new MainWindow
            {
                Opacity = 0
            };
            var dialogService = new WindowDialogService(mainWindow);
            var repository = new NotesRepository();
            var noteMutationService = new NoteMutationService(repository);
            var fileWatcher = new FileWatcherService();
            var themeLoader = new ThemeLoaderService();
            var aiPromptCatalog = new AiPromptCatalogService();
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            var aiCompletionsClient = new OpenAiCompletionsClient(httpClient);
            var aiTextActionService = new OpenAiTextActionService(aiCompletionsClient);
            var aiChatService = new OpenAiChatService(aiCompletionsClient);
            var chatViewModelFactory = new ChatViewModelFactory(aiChatService, repository, settingsService, noteMutationService);
            var mainViewModel = new MainViewModel(repository, settingsService, fileWatcher, themeLoader, fontCatalog, aiPromptCatalog, aiTextActionService, noteMutationService, dialogService, _appearanceService, chatViewModelFactory);
            mainWindow.DataContext = mainViewModel;
            mainWindow.SetWindowLayoutService(windowLayoutService);

            // Apply saved layout synchronously before the window is shown,
            // so it appears at the correct position and size immediately.
            if (startup.Layout is not null)
            {
                mainWindow.Position = new PixelPoint((int)startup.Layout.X, (int)startup.Layout.Y);
                mainWindow.Width = startup.Layout.Width;
                mainWindow.Height = startup.Layout.Height;
            }

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyStartupAppearance(StartupStateSnapshot startup)
    {
        _appearanceService.ApplyTheme(startup.Theme);
        _appearanceService.ApplyUiFontSize(startup.UiFontSize);
        _appearanceService.ApplyTerminalFont(startup.TerminalFontFamily, startup.TerminalFontVariant);
        _appearanceService.ApplySidebarFont(startup.SidebarFontFamily, startup.SidebarFontVariant);
        _appearanceService.ApplyCodeFont(startup.CodeFontFamily, startup.CodeFontVariant);
    }
}
