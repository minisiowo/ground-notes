using System.Collections.ObjectModel;
using Avalonia.Controls;
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
    public async Task ShowKeyboardShortcutsHelpCommand_UsesDialogService()
    {
        Directory.CreateDirectory(_tempRoot);
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.ShowKeyboardShortcutsHelpCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogService.ShowKeyboardShortcutsHelpCallCount);
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
    public async Task GenerateTitleSuggestionsCommand_LoadsSuggestionsForCurrentNote()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline", "meeting-summary", "deployment-checklist"]
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService, aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = Assert.Single(vm.VisibleNotes);
        await WaitForConditionAsync(() => vm.CurrentNote is not null);

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Equal(["project-outline", "meeting-summary", "deployment-checklist"], vm.TitleSuggestions);
        Assert.True(vm.IsTitleSuggestionsOpen);
        Assert.NotNull(aiTitleSuggestionService.LastDocument);
        Assert.Equal("note", aiTitleSuggestionService.LastDocument!.Title);
        Assert.Equal("body", aiTitleSuggestionService.LastDocument.Body);
        Assert.Equal(string.Empty, aiTitleSuggestionService.LastAdditionalContext);
    }

    [Fact]
    public async Task GenerateTitleSuggestionsCommand_PassesAdditionalContextAndKeepsItForNextRound()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline", "meeting-summary", "deployment-checklist"]
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService, aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = Assert.Single(vm.VisibleNotes);
        await WaitForConditionAsync(() => vm.CurrentNote is not null);
        vm.TitleSuggestionsContext = "Focus on release planning and make it shorter.";

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Equal("Focus on release planning and make it shorter.", aiTitleSuggestionService.LastAdditionalContext);
        Assert.Equal("Focus on release planning and make it shorter.", vm.TitleSuggestionsContext);
    }

    [Fact]
    public async Task ApplyTitleSuggestionCommand_RenamesCurrentNote()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline", "meeting-summary", "deployment-checklist"]
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService, aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = Assert.Single(vm.VisibleNotes);
        await WaitForConditionAsync(() => vm.CurrentNote is not null);
        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        await vm.ApplyTitleSuggestionCommand.ExecuteAsync("project-outline");

        Assert.Equal("project-outline", vm.CurrentNote?.Title);
        Assert.Equal("project-outline", vm.EditorTitle);
        Assert.False(vm.IsTitleSuggestionsOpen);
        Assert.Empty(vm.TitleSuggestions);
        Assert.Equal(string.Empty, vm.TitleSuggestionsContext);
        Assert.Contains(vm.VisibleNotes, note => string.Equals(note.DisplayName, "project-outline", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangingSelectedNote_ClearsTitleSuggestionContext()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "first.md"), "first body");
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "second.md"), "second body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        var first = Assert.Single(vm.VisibleNotes.Where(note => string.Equals(note.DisplayName, "first", StringComparison.Ordinal)));
        var second = Assert.Single(vm.VisibleNotes.Where(note => string.Equals(note.DisplayName, "second", StringComparison.Ordinal)));

        vm.SelectedVisibleNote = first;
        await WaitForConditionAsync(() => vm.CurrentNote is not null);
        vm.TitleSuggestionsContext = "Prefer concise release note naming.";

        vm.SelectedVisibleNote = second;
        await WaitForConditionAsync(() => string.Equals(vm.CurrentNote?.FilePath, second.FilePath, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(string.Empty, vm.TitleSuggestionsContext);
    }

    [Fact]
    public async Task GenerateTitleSuggestionsCommand_DoesNotRunWhenAiDisabled()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "note.md");
        await File.WriteAllTextAsync(notePath, "body");

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };
        var settingsService = new FakeSettingsService();
        await settingsService.UpdateSettingsAsync(s => s with { AiSettings = new AiSettings("secret", "gpt-5.4-mini", false) });
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline", "meeting-summary", "deployment-checklist"]
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService, settingsService: settingsService, aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = Assert.Single(vm.VisibleNotes);
        await WaitForConditionAsync(() => vm.CurrentNote is not null);

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Empty(vm.TitleSuggestions);
        Assert.Equal("AI is disabled in settings.", vm.StatusMessage);
        Assert.Null(aiTitleSuggestionService.LastDocument);
    }

    [Fact]
    public async Task SelectCalendarDayCommand_FiltersByCreatedDate_AndSecondClickClearsFilter()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("march-9.md", "march-9", "release planning", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("march-10.md", "march-10", "postmortem", createdAt: new DateTime(2026, 3, 10, 9, 15, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.DisplayedCalendarMonth = new DateTime(2026, 3, 1);

        var march9 = Assert.Single(vm.VisibleCalendarDays.Where(day => day.Date == new DateTime(2026, 3, 9)));

        vm.SelectCalendarDayCommand.Execute(march9);

        Assert.Equal(new DateTime(2026, 3, 9), vm.SelectedCalendarDate);
        Assert.Equal("march-9", Assert.Single(vm.VisibleNotes).DisplayName);

        vm.SelectCalendarDayCommand.Execute(march9);

        Assert.Null(vm.SelectedCalendarDate);
        Assert.Equal(2, vm.VisibleNotes.Count);
    }

    [Fact]
    public async Task DateFilter_CombinesWithSearch()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("release-ship-plan.md", "release-ship-plan", "ship checklist", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("incident-ship-log.md", "incident-ship-log", "ship checklist", createdAt: new DateTime(2026, 3, 10, 9, 15, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.DisplayedCalendarMonth = new DateTime(2026, 3, 1);
        vm.SearchText = "ship";

        var march10 = Assert.Single(vm.VisibleCalendarDays.Where(day => day.Date == new DateTime(2026, 3, 10)));

        vm.SelectCalendarDayCommand.Execute(march10);

        var match = Assert.Single(vm.VisibleNotes);
        Assert.Equal("incident-ship-log", match.DisplayName);
    }

    [Fact]
    public async Task VisibleCalendarDays_UseAllNotesInsteadOfFilteredVisibleNotes()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("unique-focus.md", "unique-focus", "unique search token", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("hidden.md", "hidden", "something else", createdAt: new DateTime(2026, 3, 10, 9, 15, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.DisplayedCalendarMonth = new DateTime(2026, 3, 1);
        vm.SearchText = "unique";

        var march10 = Assert.Single(vm.VisibleCalendarDays.Where(day => day.Date == new DateTime(2026, 3, 10)));

        Assert.True(march10.HasNotes);
        Assert.Single(vm.VisibleNotes);
    }

    [Fact]
    public async Task SearchTextChange_DoesNotRebuildCalendarDays_WhenNotesAreUnchanged()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("release-alpha.md", "release-alpha", "release notes", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "incident report", createdAt: new DateTime(2026, 3, 10, 9, 15, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.DisplayedCalendarMonth = new DateTime(2026, 3, 1);

        var before = vm.VisibleCalendarDays;

        vm.SearchText = "release";

        Assert.Same(before, vm.VisibleCalendarDays);
        Assert.Single(vm.VisibleNotes);
    }

    [Fact]
    public async Task DisplayedCalendarMonth_UsesActualWeekCountForVisibleDays()
    {
        using var vm = await CreateViewModelAsync();

        vm.DisplayedCalendarMonth = new DateTime(2021, 2, 1);
        Assert.Equal(28, vm.VisibleCalendarDays.Count);

        vm.DisplayedCalendarMonth = new DateTime(2026, 3, 1);
        Assert.Equal(42, vm.VisibleCalendarDays.Count);

        vm.DisplayedCalendarMonth = new DateTime(2026, 4, 1);
        Assert.Equal(35, vm.VisibleCalendarDays.Count);
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

    [Fact]
    public async Task ToggleYamlFrontMatterVisibilityCommand_ShowsFullDocument_AndPersistsSetting()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("note.md", "note", "body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0), tags: ["alpha"]);

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };
        var settingsService = new FakeSettingsService();

        using var vm = await CreateViewModelAsync(dialogService: dialogService, settingsService: settingsService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = Assert.Single(vm.VisibleNotes);
        await WaitForConditionAsync(() => vm.CurrentNote is not null);

        await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);

        Assert.True(vm.ShowYamlFrontMatterInEditor);
        Assert.Contains("---", vm.EditorBody, StringComparison.Ordinal);
        Assert.Contains("title: note", vm.EditorBody, StringComparison.Ordinal);
        Assert.Contains("tags: [\"alpha\"]", vm.EditorBody, StringComparison.Ordinal);
        Assert.True(settingsService.GetSettingsSync().ShowYamlFrontMatterInEditor);
    }

    [Fact]
    public async Task ToggleYamlFrontMatterVisibilityCommand_KeepsYamlMode_WhenFrontMatterIsInvalid()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("note.md", "note", "body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0), tags: ["alpha"]);

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = Assert.Single(vm.VisibleNotes);
        await WaitForConditionAsync(() => vm.CurrentNote is not null);

        await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
        vm.EditorBody = "---\ntitle note\n---\nbody";

        await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);

        Assert.True(vm.ShowYamlFrontMatterInEditor);
        Assert.Contains("Invalid YAML frontmatter", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidYamlDraft_CanBeDiscarded_WhenSwitchingToAnotherNote()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("first.md", "first", "first body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("second.md", "second", "second body", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            ConfirmDiscardInvalidDraftResult = true
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.NewNoteCommand.ExecuteAsync(null);
        await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
        vm.EditorBody = "---\ntitle broken\n---\nbody";

        var second = Assert.Single(vm.VisibleNotes.Where(note => string.Equals(note.DisplayName, "second", StringComparison.Ordinal)));
        vm.SelectedVisibleNote = second;
        await WaitForConditionAsync(() => string.Equals(vm.CurrentNote?.FilePath, second.FilePath, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(1, dialogService.ConfirmDiscardInvalidDraftCallCount);
        Assert.Equal("second", vm.CurrentNote?.Title);
        Assert.False(vm.ShowStructuredMetadataEditors);
    }

    [Fact]
    public async Task InvalidYamlDraft_StaysOpen_WhenDiscardIsCancelled()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("first.md", "first", "first body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("second.md", "second", "second body", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            ConfirmDiscardInvalidDraftResult = false
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.NewNoteCommand.ExecuteAsync(null);
        await vm.ToggleYamlFrontMatterVisibilityCommand.ExecuteAsync(null);
        vm.EditorBody = "---\ntitle broken\n---\nbody";

        var second = Assert.Single(vm.VisibleNotes.Where(note => string.Equals(note.DisplayName, "second", StringComparison.Ordinal)));
        vm.SelectedVisibleNote = second;
        await Task.Delay(100);

        Assert.Equal(1, dialogService.ConfirmDiscardInvalidDraftCallCount);
        Assert.Null(vm.SelectedVisibleNote);
        Assert.NotNull(vm.CurrentNote);
        Assert.Contains("discard the draft", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<MainViewModel> CreateViewModelAsync(
        FakeWorkspaceDialogService? dialogService = null,
        FakeChatViewModelFactory? chatViewModelFactory = null,
        FakeAppAppearanceService? appearanceService = null,
        FakeEditorLayoutState? editorLayoutState = null,
        FakeSettingsService? settingsService = null,
        FakeAiTitleSuggestionService? aiTitleSuggestionService = null)
    {
        dialogService ??= new FakeWorkspaceDialogService();
        chatViewModelFactory ??= new FakeChatViewModelFactory();
        appearanceService ??= new FakeAppAppearanceService();
        editorLayoutState ??= new FakeEditorLayoutState();
        settingsService ??= new FakeSettingsService();
        aiTitleSuggestionService ??= new FakeAiTitleSuggestionService();

        var repository = new NotesRepository();
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
            aiTitleSuggestionService,
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

    private async Task WriteNoteAsync(string fileName, string title, string body, DateTime createdAt, string[]? tags = null)
    {
        tags ??= [];
        var content =
            $"""
            ---
            title: {title}
            tags: [{string.Join(", ", tags)}]
            createdAt: {createdAt:O}
            updatedAt: {createdAt.AddMinutes(1):O}
            ---
            {body}
            """;

        await File.WriteAllTextAsync(Path.Combine(_tempRoot, fileName), content);
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

        public int ConfirmDiscardInvalidDraftCallCount { get; private set; }

        public int ShowChatCallCount { get; private set; }

        public int ShowKeyboardShortcutsHelpCallCount { get; private set; }

        public ChatViewModel? LastChatViewModel { get; private set; }

        public bool ConfirmDiscardInvalidDraftResult { get; set; } = true;

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

        public Task<bool> ConfirmDiscardInvalidDraftAsync()
        {
            ConfirmDiscardInvalidDraftCallCount++;
            return Task.FromResult(ConfirmDiscardInvalidDraftResult);
        }

        public Task ShowChatAsync(ChatViewModel model)
        {
            ShowChatCallCount++;
            LastChatViewModel = model;
            return Task.CompletedTask;
        }

        public Task ShowKeyboardShortcutsHelpAsync(Window? owner = null)
        {
            ShowKeyboardShortcutsHelpCallCount++;
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

        public void ApplyScrollBars(bool show)
        {
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

    private sealed class FakeAiTitleSuggestionService : IAiTitleSuggestionService
    {
        public IReadOnlyList<string> Suggestions { get; set; } = [];

        public NoteDocument? LastDocument { get; private set; }

        public string? LastAdditionalContext { get; private set; }

        public Task<IReadOnlyList<string>> GetSuggestionsAsync(NoteDocument document, AiSettings settings, string? additionalContext = null, CancellationToken cancellationToken = default)
        {
            LastDocument = new NoteDocument
            {
                FilePath = document.FilePath,
                Title = document.Title,
                OriginalTitle = document.OriginalTitle,
                Body = document.Body,
                Tags = [.. document.Tags]
            };
            LastAdditionalContext = additionalContext;
            return Task.FromResult(Suggestions);
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
        private AppSettings _settings = new(null, 12, 12, 4, 1.15, FontCatalogService.DefaultFontKey, FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultFontKey, FontCatalogService.DefaultVariantKey, FontCatalogService.DefaultCodeFontKey, FontCatalogService.DefaultVariantKey, GroundNotes.Styles.AppTheme.Dark.Name, false, true, null, AiSettings.Default);

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
    }
}
