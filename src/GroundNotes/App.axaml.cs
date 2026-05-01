using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.ViewModels;
using GroundNotes.Views;
using GroundNotes.Styles;

namespace GroundNotes;

public partial class App : Application
{
    private readonly IAppAppearanceService _appearanceService = new AppAppearanceService();
    private HttpClient? _openAiHttpClient;

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

        StartupStateSnapshot startup;
        try
        {
            startup = startupStateService.Load();
        }
        catch (Exception)
        {
            var fonts = fontCatalog.LoadBundledFonts();
            var defaultFont = FontResolutionHelper.FindByKey(fonts, FontCatalogService.DefaultFontKey) ?? fonts[0];
            var defaultVariant = FontResolutionHelper.GetDefaultVariant(defaultFont);
            var fallbackSettings = new AppSettings(
                null, null, null, null, null,
                null, null, null, null, null, null,
                null, false, true, null,
                GroundNotes.Models.AiSettings.Default);
            startup = new StartupStateSnapshot(
                fallbackSettings,
                null,
                AppTheme.Dark,
                fonts,
                defaultFont,
                defaultVariant,
                defaultFont,
                defaultVariant,
                defaultFont,
                defaultVariant,
                12);
        }

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
        _openAiHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        desktop.Exit += (_, _) => _openAiHttpClient?.Dispose();
        var aiCompletionsClient = new OpenAiCompletionsClient(_openAiHttpClient);
        var aiTextActionService = new OpenAiTextActionService(aiCompletionsClient);
        var aiTitleSuggestionService = new OpenAiTitleSuggestionService(aiCompletionsClient);
        var aiChatService = new OpenAiChatService(aiCompletionsClient);
        var noteSearchServiceFactory = new NoteSearchServiceFactory(repository);
        var chatViewModelFactory = new ChatViewModelFactory(aiChatService, repository, settingsService, noteMutationService, noteSearchServiceFactory);
        var mainViewModel = new MainViewModel(repository, settingsService, fileWatcher, themeLoader, fontCatalog, aiPromptCatalog, aiTextActionService, aiTitleSuggestionService, noteMutationService, dialogService, _appearanceService, editorLayoutState, chatViewModelFactory, noteSearchServiceFactory);
        mainWindow.DataContext = mainViewModel;
        mainWindow.SetWindowLayoutService(windowLayoutService);

        if (startup.Layout is not null)
        {
            mainWindow.Position = new PixelPoint((int)startup.Layout.X, (int)startup.Layout.Y);
            mainWindow.Width = startup.Layout.Width;
            mainWindow.Height = startup.Layout.Height;
        }

        desktop.MainWindow = mainWindow;
        _appearanceService.ApplyScrollBars(startup.Settings.ShowScrollBars);

        try
        {
            await mainWindow.CompleteStartupInitializationAsync();
            await mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            if (mainWindow.DataContext is MainViewModel vm)
            {
                vm.StatusMessage = $"Startup failed: {ex.Message}";
            }
        }

        if (mainWindow.Opacity == 0)
        {
            await mainWindow.CompleteStartupInitializationAsync();
        }
    }
}
