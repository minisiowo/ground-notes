using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using QuickNotesTxt.Models;
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
            InitializeDesktop(desktop);
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

    private async void InitializeDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var settingsService = new FolderSettingsService();
        var fontCatalog = new FontCatalogService();
        var startupStateService = new StartupStateService(settingsService, fontCatalog);
        var startup = startupStateService.Load();
        ApplyStartupAppearance(startup);
        var editorLayoutState = new EditorLayoutState(new EditorLayoutSettings(
            EditorDisplaySettings.NormalizeIndentSize(startup.Settings.EditorIndentSize),
            EditorDisplaySettings.NormalizeLineHeightFactor(startup.Settings.EditorLineHeightFactor)));
        var windowLayoutService = new SettingsWindowLayoutService(settingsService);
        var mainWindow = new MainWindow
        {
            Opacity = 0
        };
        mainWindow.SetEditorLayoutState(editorLayoutState);
        var dialogService = new WindowDialogService(mainWindow, editorLayoutState);
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
        var mainViewModel = new MainViewModel(repository, settingsService, fileWatcher, themeLoader, fontCatalog, aiPromptCatalog, aiTextActionService, noteMutationService, dialogService, _appearanceService, editorLayoutState, chatViewModelFactory);
        mainWindow.DataContext = mainViewModel;
        mainWindow.SetWindowLayoutService(windowLayoutService);

        if (startup.Layout is not null)
        {
            mainWindow.Position = new PixelPoint((int)startup.Layout.X, (int)startup.Layout.Y);
            mainWindow.Width = startup.Layout.Width;
            mainWindow.Height = startup.Layout.Height;
        }

        desktop.MainWindow = mainWindow;

        try
        {
            await mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (mainWindow.DataContext is MainViewModel vm)
            {
                vm.StatusMessage = $"Startup failed: {ex.Message}";
            }
        }
        finally
        {
            await mainWindow.CompleteStartupInitializationAsync();
        }
    }
}
