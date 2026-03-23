using System.Collections.ObjectModel;
using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ChooseFolderCommand_UsesDialogServiceAndLoadsFolder()
    {
        Directory.CreateDirectory(_tempRoot);
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);

        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogService.PickFolderCallCount);
        Assert.Equal(_tempRoot, vm.NotesFolder);
        Assert.Equal("Ready.", vm.StatusMessage);
    }

    [Fact]
    public async Task OpenChatCommand_UsesChatFactoryAndDialogService()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };
        var chatFactory = new FakeChatViewModelFactory();

        using var vm = await CreateViewModelAsync(dialogService: dialogService, chatViewModelFactory: chatFactory);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var selectedNote = Assert.Single(vm.VisibleNotes);
        vm.SelectedVisibleNote = selectedNote;

        await vm.OpenChatCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogService.ShowChatCallCount);
        Assert.Equal(_tempRoot, chatFactory.LastNotesFolder);
        Assert.Equal(vm.SelectedAiModel, chatFactory.LastDefaultModel);
        Assert.Equal(vm.SelectedNoteSummary?.FilePath, chatFactory.LastOriginNote?.FilePath);
        Assert.NotNull(dialogService.LastChatViewModel);
    }

    [Fact]
    public async Task DeleteNoteCommand_DoesNotDelete_WhenDialogRejects()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            ConfirmDeleteResult = false
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var note = Assert.Single(vm.VisibleNotes);

        await vm.DeleteNoteCommand.ExecuteAsync(note);

        Assert.Equal(1, dialogService.ConfirmDeleteCallCount);
        Assert.True(File.Exists(notePath));
        Assert.Equal("Delete canceled.", vm.StatusMessage);
    }

    [Fact]
    public async Task CommitRenameAsync_DoesNotMarkConflictForLocalRename()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        var note = Assert.Single(vm.VisibleNotes);
        vm.SelectedVisibleNote = note;
        await WaitForConditionAsync(() => vm.CurrentNote is not null);

        vm.StartRenameNoteCommand.Execute(note);
        note.RenameText = "renamed";

        await vm.CommitRenameAsync(note);

        Assert.False(vm.HasConflict);
        Assert.Equal("renamed", vm.CurrentNote?.Title);
        Assert.Contains(vm.VisibleNotes, summary => string.Equals(summary.DisplayName, "renamed", StringComparison.Ordinal));
        Assert.DoesNotContain(vm.VisibleNotes, summary => string.Equals(summary.DisplayName, "note", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplySettingsPreview_AppliesCodeFontImmediately()
    {
        var appearanceService = new FakeAppAppearanceService();
        var editorLayoutState = new FakeEditorLayoutState();
        using var vm = await CreateViewModelAsync(appearanceService: appearanceService, editorLayoutState: editorLayoutState);

        var model = new SettingsDialogModel(
            ["Default"],
            new FakeFontCatalogService().LoadBundledFonts(),
            "Default",
            "Iosevka Slab",
            FontCatalogService.DefaultVariantKey,
            "Iosevka Slab",
            FontCatalogService.DefaultVariantKey,
            "JetBrains Mono",
            FontCatalogService.DefaultVariantKey,
            12,
            12,
            2,
            1.3,
            true,
            string.Empty,
            "gpt-5.4-mini",
            string.Empty,
            string.Empty,
            string.Empty);

        vm.ApplySettingsPreview(model);

        Assert.Equal(1, appearanceService.ApplyCodeFontCallCount);
        Assert.Equal("JetBrains Mono", appearanceService.LastCodeFontFamilyName);
        Assert.Equal(FontCatalogService.DefaultVariantKey, appearanceService.LastCodeFontVariantName);
        Assert.Equal(2, editorLayoutState.CurrentSettings.IndentationSize);
        Assert.Equal(1.3, editorLayoutState.CurrentSettings.LineHeightFactor);
    }

    private async Task<MainViewModel> CreateViewModelAsync(
        FakeWorkspaceDialogService? dialogService = null,
        FakeChatViewModelFactory? chatViewModelFactory = null,
        FakeAppAppearanceService? appearanceService = null,
        FakeEditorLayoutState? editorLayoutState = null)
    {
        dialogService ??= new FakeWorkspaceDialogService();
        chatViewModelFactory ??= new FakeChatViewModelFactory();
        appearanceService ??= new FakeAppAppearanceService();
        editorLayoutState ??= new FakeEditorLayoutState();

        var repository = new NotesRepository();
        var settingsService = new FakeSettingsService();
        var fileWatcherService = new FakeFileWatcherService();
        var noteMutationService = new NoteMutationService(repository);
        var noteSearchServiceFactory = new NoteSearchServiceFactory(repository);
        var vm = new MainViewModel(
            repository,
            settingsService,
            fileWatcherService,
            new FakeThemeLoaderService(),
            new FakeFontCatalogService(),
            new FakeAiPromptCatalogService(),
            new FakeAiTextActionService(),
            noteMutationService,
            dialogService,
            appearanceService,
            editorLayoutState,
            chatViewModelFactory,
            noteSearchServiceFactory);

        await vm.InitializeAsync();
        return vm;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 1000)
    {
        var start = Environment.TickCount64;
        while (!condition())
        {
            if (Environment.TickCount64 - start > timeoutMs)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(20);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class FakeWorkspaceDialogService : IWorkspaceDialogService
    {
        public string? FolderToPick { get; set; }

        public bool ConfirmDeleteResult { get; set; } = true;

        public int PickFolderCallCount { get; private set; }

        public int ConfirmDeleteCallCount { get; private set; }

        public int ShowChatCallCount { get; private set; }

        public ChatViewModel? LastChatViewModel { get; private set; }

        public void Attach(Avalonia.Controls.Window window)
        {
        }

        public Task<string?> PickFolderAsync()
        {
            PickFolderCallCount++;
            return Task.FromResult(FolderToPick);
        }

        public Task<bool> ConfirmDeleteAsync(string noteName)
        {
            ConfirmDeleteCallCount++;
            return Task.FromResult(ConfirmDeleteResult);
        }

        public Task ShowChatAsync(ChatViewModel model)
        {
            ShowChatCallCount++;
            LastChatViewModel = model;
            return Task.CompletedTask;
        }

        public Task<SettingsDialogModel?> ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> previewSettingsAsync)
        {
            return Task.FromResult<SettingsDialogModel?>(null);
        }
    }

    private sealed class FakeChatViewModelFactory : IChatViewModelFactory
    {
        public string? LastNotesFolder { get; private set; }

        public string? LastDefaultModel { get; private set; }

        public NoteSummary? LastOriginNote { get; private set; }

        public ChatViewModel Create(
            string notesFolder,
            string defaultModel,
            Func<IEnumerable<NoteSummary>> searchNotesFunc,
            NoteSummary? originNote,
            IEnumerable<NoteSummary>? initialNotes = null)
        {
            LastNotesFolder = notesFolder;
            LastDefaultModel = defaultModel;
            LastOriginNote = originNote;

            return new ChatViewModel(
                new FakeAiChatService(),
                new NotesRepository(),
                new FakeSettingsService(),
                new NoteMutationService(new NotesRepository()),
                new NoteSearchService(new NotesRepository(), searchNotesFunc),
                notesFolder,
                defaultModel,
                ["gpt-5.4-mini"],
                originNote,
                initialNotes);
        }
    }

    private sealed class FakeAppAppearanceService : IAppAppearanceService
    {
        public int ApplyCodeFontCallCount { get; private set; }

        public string? LastCodeFontFamilyName { get; private set; }

        public string? LastCodeFontVariantName { get; private set; }

        public void ApplyTheme(GroundNotes.Styles.AppTheme theme)
        {
        }

        public void ApplyUiFontSize(double fontSize)
        {
        }

        public void ApplyTerminalFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant)
        {
        }

        public void ApplySidebarFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant)
        {
        }

        public void ApplyCodeFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant)
        {
            ApplyCodeFontCallCount++;
            LastCodeFontFamilyName = fontFamily.DisplayName;
            LastCodeFontVariantName = fontVariant.DisplayName;
        }
    }

    private sealed class FakeEditorLayoutState : IEditorLayoutState
    {
        public EditorLayoutSettings CurrentSettings { get; private set; } = new(
            EditorDisplaySettings.DefaultIndentSize,
            EditorDisplaySettings.DefaultLineHeightFactor);

        public event EventHandler<EditorLayoutSettings>? SettingsChanged;

        public void Set(EditorLayoutSettings settings)
        {
            CurrentSettings = EditorLayoutSettings.Normalize(settings);
            SettingsChanged?.Invoke(this, CurrentSettings);
        }
    }

    private sealed class FakeFileWatcherService : IFileWatcherService
    {
#pragma warning disable CS0067
        public event EventHandler<NoteFileChangedEventArgs>? NoteChanged;
#pragma warning restore CS0067

        public void Watch(string folderPath)
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeThemeLoaderService : IThemeLoaderService
    {
        public string ThemesDirectory => string.Empty;

        public Task<IReadOnlyList<GroundNotes.Styles.AppTheme>> LoadAllThemesAsync()
        {
            return Task.FromResult<IReadOnlyList<GroundNotes.Styles.AppTheme>>(GroundNotes.Styles.AppTheme.BuiltInThemes);
        }

        public Task ExportThemeAsync(GroundNotes.Styles.AppTheme theme)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFontCatalogService : IFontCatalogService
    {
        public IReadOnlyList<BundledFontFamilyOption> LoadBundledFonts()
        {
            return
            [
                new BundledFontFamilyOption(
                    FontCatalogService.DefaultFontKey,
                    "Iosevka Slab",
                    "avares://GroundNotes/Assets/Fonts/IosevkaSlab#Iosevka Slab",
                    [new BundledFontVariantOption(FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultVariantKey, Avalonia.Media.FontWeight.Normal, Avalonia.Media.FontStyle.Normal)]),
                new BundledFontFamilyOption(
                    FontCatalogService.DefaultCodeFontKey,
                    "JetBrains Mono",
                    "avares://GroundNotes/Assets/Fonts/JetBrainsMono#JetBrains Mono",
                    [new BundledFontVariantOption(FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultVariantKey, Avalonia.Media.FontWeight.Normal, Avalonia.Media.FontStyle.Normal)])
            ];
        }
    }

    private sealed class FakeAiPromptCatalogService : IAiPromptCatalogService
    {
        public string BuiltInPromptsDirectory => string.Empty;

        public string GetNotesFolderPromptsDirectory(string notesFolder)
        {
            return Path.Combine(notesFolder, ".quicknotestxt", "ai-prompts");
        }

        public Task<AiPromptCatalogLoadResult> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiPromptCatalogLoadResult([], []));
        }
    }

    private sealed class FakeAiTextActionService : IAiTextActionService
    {
        public Task<string> RunPromptAsync(AiPromptDefinition prompt, string selectedText, AiSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(selectedText);
        }
    }

    private sealed class FakeAiChatService : IAiChatService
    {
        public Task<string> GetResponseAsync(IEnumerable<AiChatMessage> history, AiSettings settings, string model, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("reply");
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private AppSettings _settings = new(null, 12, 12, 4, 1.15, FontCatalogService.DefaultFontKey, FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultFontKey, FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultCodeFontKey, FontCatalogService.DefaultVariantKey, GroundNotes.Styles.AppTheme.Dark.Name, null, AiSettings.Default);

        public AppSettings GetSettingsSync() => _settings;

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);

        public void SaveSettingsSync(AppSettings settings)
        {
            _settings = settings;
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }

        public Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
        {
            _settings = update(_settings);
            return Task.CompletedTask;
        }

        public Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings.AiSettings);

        public Task SetAiSettingsAsync(AiSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = _settings with { AiSettings = settings };
            return Task.CompletedTask;
        }
    }
}
