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
    public async Task OpenNotePickerCommand_WaitsForQueryOrDownArrowBeforeShowingResults()
    {
        Directory.CreateDirectory(_tempRoot);
        for (var index = 1; index <= 5; index++)
        {
            await WriteNoteAsync(
                $"note-{index}.md",
                $"note-{index}",
                $"body {index}",
                createdAt: new DateTime(2026, 3, index, 7, 33, 0));
        }

        using var vm = await CreateViewModelAsync(folderOverride: _tempRoot);

        vm.OpenNotePickerCommand.Execute(null);

        Assert.Empty(vm.NotePickerResults);
        Assert.True(vm.IsNotePickerIdle);
        Assert.Equal("Type to search or press ↓ for recent notes.", vm.NotePickerStatusText);

        vm.MoveNotePickerSelectionCommand.Execute(1);

        Assert.False(vm.IsNotePickerIdle);
        Assert.Equal(3, vm.NotePickerResults.Count);
        Assert.Equal(
            new[] { "note-5", "note-4", "note-3" },
            vm.NotePickerResults.Select(note => note.Title).ToArray());
        Assert.Equal("Showing 3 of 5 matches", vm.NotePickerStatusText);

        vm.NotePickerQuery = "note";

        Assert.Equal(5, vm.NotePickerResults.Count);
        Assert.Equal("5 matches", vm.NotePickerStatusText);

        vm.NotePickerQuery = string.Empty;

        Assert.Empty(vm.NotePickerResults);
        Assert.True(vm.IsNotePickerIdle);
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
        await vm.OpenSidebarNoteCommand.ExecuteAsync(Assert.Single(vm.VisibleNotes));

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Equal(["project-outline", "meeting-summary", "deployment-checklist"], vm.TitleSuggestions);
        Assert.True(vm.IsTitleSuggestionsOpen);
        Assert.NotNull(aiTitleSuggestionService.LastDocument);
        Assert.Equal("note", aiTitleSuggestionService.LastDocument!.Title);
        Assert.Equal("body", aiTitleSuggestionService.LastDocument.Body);
        Assert.Equal(string.Empty, aiTitleSuggestionService.LastAdditionalContext);
    }

    [Fact]
    public async Task GenerateTitleSuggestionsCommand_UsesActiveSecondaryPaneNote()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["beta-outline", "beta-summary", "beta-checklist"]
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService, aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectedVisibleNote = vm.VisibleNotes.First(note => note.DisplayName == "alpha");
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1 && vm.SecondaryPanes[0].CurrentNote is not null);

        vm.ActivatePane(vm.SecondaryPanes[0]);
        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Equal("beta", aiTitleSuggestionService.LastDocument?.Title);
        Assert.Equal("body beta", aiTitleSuggestionService.LastDocument?.Body);
        Assert.Equal(["beta-outline", "beta-summary", "beta-checklist"], vm.TitleSuggestions);
    }

    [Fact]
    public async Task OpenNoteInSplitCommand_LoadsSecondPaneWithoutReplacingPrimary()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        var alpha = vm.VisibleNotes.First(note => string.Equals(note.DisplayName, "alpha", StringComparison.Ordinal));
        var beta = vm.VisibleNotes.First(note => string.Equals(note.DisplayName, "beta", StringComparison.Ordinal));
        vm.SelectedVisibleNote = alpha;
        await WaitForConditionAsync(() => vm.CurrentNote is not null);

        await vm.OpenNoteInSplitCommand.ExecuteAsync(beta);

        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1 && vm.SecondaryPanes[0].CurrentNote is not null);
        Assert.True(vm.HasSecondaryPane);
        Assert.Equal("alpha", vm.CurrentNote?.Title);
        Assert.Equal("beta", vm.SecondaryPanes[0].CurrentNote?.Title);
        Assert.Equal("body beta", vm.SecondaryPanes[0].EditorBody);
    }

    [Fact]
    public async Task OpenNoteInSplitCommand_FocusesExistingPaneWhenNoteAlreadyOpen()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        var alpha = vm.VisibleNotes.First(note => string.Equals(note.DisplayName, "alpha", StringComparison.Ordinal));
        var beta = vm.VisibleNotes.First(note => string.Equals(note.DisplayName, "beta", StringComparison.Ordinal));
        vm.SelectedVisibleNote = alpha;
        await WaitForConditionAsync(() => vm.CurrentNote is not null);
        await vm.OpenNoteInSplitCommand.ExecuteAsync(beta);
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1 && vm.SecondaryPanes[0].CurrentNote is not null);

        await vm.OpenNoteInSplitCommand.ExecuteAsync(beta);

        Assert.True(vm.HasSecondaryPane);
        Assert.Single(vm.SecondaryPanes);
        Assert.Equal("beta", vm.SecondaryPanes[0].CurrentNote?.Title);
    }

    [Fact]
    public async Task OpenNoteInSplitCommand_FallsBackWhenSplitUnavailable()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SetSplitEditorAvailability(false);

        var beta = vm.VisibleNotes.First(note => string.Equals(note.DisplayName, "beta", StringComparison.Ordinal));

        await vm.OpenNoteInSplitCommand.ExecuteAsync(beta);

        await WaitForConditionAsync(() => vm.CurrentNote is not null);
        Assert.False(vm.HasSecondaryPane);
        Assert.Equal("beta", vm.CurrentNote?.Title);
        Assert.Equal("Expand the window to open a second editor.", vm.StatusMessage);
    }

    [Fact]
    public async Task OpenNoteInSplitCommand_InsertsPaneToRightOfActivePane()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));
        await WriteNoteAsync("gamma.md", "gamma", "body gamma", createdAt: new DateTime(2026, 3, 11, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        var alpha = vm.VisibleNotes.First(note => note.DisplayName == "alpha");
        var beta = vm.VisibleNotes.First(note => note.DisplayName == "beta");
        var gamma = vm.VisibleNotes.First(note => note.DisplayName == "gamma");

        vm.SelectedVisibleNote = alpha;
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(beta);
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1);

        vm.ActivatePane(vm.SecondaryPanes[0]);
        await vm.OpenNoteInSplitCommand.ExecuteAsync(gamma);

        Assert.Equal(new[] { "beta", "gamma" }, vm.SecondaryPanes.Select(pane => pane.CurrentNote?.Title).ToArray());
        Assert.Equal("beta", vm.ActiveSecondaryPane?.CurrentNote?.Title);
    }

    [Fact]
    public async Task SelectingNote_LoadsIntoActiveSecondaryPane()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));
        await WriteNoteAsync("gamma.md", "gamma", "body gamma", createdAt: new DateTime(2026, 3, 11, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SelectedVisibleNote = vm.VisibleNotes.First(note => note.DisplayName == "alpha");
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1);

        vm.ActivatePane(vm.SecondaryPanes[0]);
        vm.SelectedVisibleNote = vm.VisibleNotes.First(note => note.DisplayName == "gamma");

        await WaitForConditionAsync(() => vm.SecondaryPanes[0].CurrentNote?.Title == "gamma");
        Assert.Equal("alpha", vm.CurrentNote?.Title);
        Assert.Equal("gamma", vm.SecondaryPanes[0].CurrentNote?.Title);
    }

    [Fact]
    public async Task OpenSidebarNoteCommand_LoadsIntoActivePane()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));
        await WriteNoteAsync("gamma.md", "gamma", "body gamma", createdAt: new DateTime(2026, 3, 11, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.OpenSidebarNoteCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "alpha"));
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1 && vm.SecondaryPanes[0].CurrentNote?.Title == "beta");

        vm.ActivatePane(vm.SecondaryPanes[0]);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "gamma"));

        await WaitForConditionAsync(() => vm.SecondaryPanes[0].CurrentNote?.Title == "gamma");
        Assert.Equal("alpha", vm.CurrentNote?.Title);
        Assert.Equal("gamma", vm.SelectedVisibleNote?.DisplayName);
    }

    [Fact]
    public async Task OpenNoteAsync_LoadsRequestedFileIntoPrimaryPane()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.OpenNoteAsync(Path.Combine(_tempRoot, "beta.md"));

        Assert.Equal("beta", vm.CurrentNote?.Title);
        Assert.Equal("body beta", vm.EditorBody);
        Assert.True(vm.IsPrimaryPaneActive);
    }

    [Fact]
    public async Task OpenNoteAsync_FlushesPendingEditBeforeSwitchingNotes()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "original alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "original beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var alphaPath = Path.Combine(_tempRoot, "alpha.md");
        await vm.OpenNoteAsync(alphaPath);
        vm.EditorBody = "edited alpha";

        await vm.OpenNoteAsync(Path.Combine(_tempRoot, "beta.md"));
        var savedAlpha = await new NotesRepository().LoadNoteAsync(alphaPath);

        Assert.Equal("edited alpha", savedAlpha?.Body);
        Assert.Equal("beta", vm.CurrentNote?.Title);
        Assert.Equal("original beta", vm.EditorBody);
    }

    [Fact]
    public async Task PrepareToCloseAsync_FlushesPendingEditorSave()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "original", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.OpenNoteAsync(Path.Combine(_tempRoot, "alpha.md"));
        vm.EditorBody = "saved before close";

        var canClose = await vm.PrepareToCloseAsync();
        var saved = await new NotesRepository().LoadNoteAsync(Path.Combine(_tempRoot, "alpha.md"));

        Assert.True(canClose);
        Assert.Equal("saved before close", saved?.Body);
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task InitializeForFolderAsync_SavesOnlyInsideExplicitWorkspace()
    {
        var folderA = Path.Combine(_tempRoot, "workspace-a");
        var folderB = Path.Combine(_tempRoot, "workspace-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        var notePath = Path.Combine(folderA, "alpha.md");
        await File.WriteAllTextAsync(notePath, "original");
        var settingsService = new FakeSettingsService();
        await settingsService.UpdateSettingsAsync(settings => settings with { NotesFolder = folderB });

        using var vm = await CreateViewModelAsync(
            settingsService: settingsService,
            folderOverride: folderA);
        await vm.OpenNoteAsync(notePath);
        vm.EditorBody = "saved in workspace A";

        Assert.True(await vm.PrepareToCloseAsync());
        var saved = await new NotesRepository().LoadNoteAsync(notePath);

        Assert.True(MainViewModel.AreSameNotesFolder(folderA, vm.NotesFolder));
        Assert.True(MainViewModel.AreSameNotesFolder(folderB, settingsService.GetSettingsSync().NotesFolder));
        Assert.Equal("saved in workspace A", saved?.Body);
        Assert.False(File.Exists(Path.Combine(folderB, "alpha.md")));
    }

    [Fact]
    public async Task ChooseFolderCommand_FlushesCurrentWorkspaceBeforeSwitching()
    {
        var folderA = Path.Combine(_tempRoot, "workspace-a");
        var folderB = Path.Combine(_tempRoot, "workspace-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        var notePath = Path.Combine(folderA, "alpha.md");
        await File.WriteAllTextAsync(notePath, "original");
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = folderA };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.OpenNoteAsync(notePath);
        vm.EditorBody = "saved before switching";
        dialogService.FolderToPick = folderB;

        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var saved = await new NotesRepository().LoadNoteAsync(notePath);

        Assert.Equal("saved before switching", saved?.Body);
        Assert.True(MainViewModel.AreSameNotesFolder(folderB, vm.NotesFolder));
        Assert.False(File.Exists(Path.Combine(folderB, "alpha.md")));
    }

    [Fact]
    public async Task OpenNoteAsync_RejectsFileOutsideCurrentWorkspace()
    {
        var folderA = Path.Combine(_tempRoot, "workspace-a");
        var folderB = Path.Combine(_tempRoot, "workspace-b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        var externalNotePath = Path.Combine(folderB, "external.md");
        await File.WriteAllTextAsync(externalNotePath, "external");

        using var vm = await CreateViewModelAsync(folderOverride: folderA);

        await vm.OpenNoteAsync(externalNotePath);

        Assert.Null(vm.CurrentNote);
        Assert.Contains("outside", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForeignSave_WithPendingLocalEdit_BlocksAutosaveAndClose()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "shared.md");
        await File.WriteAllTextAsync(notePath, "original");
        var repository = new NotesRepository();
        var mutationService = new NoteMutationService(repository);

        using var first = await CreateViewModelAsync(
            repository: repository,
            noteMutationService: mutationService,
            folderOverride: _tempRoot);
        using var second = await CreateViewModelAsync(
            repository: repository,
            noteMutationService: mutationService,
            folderOverride: _tempRoot);
        await first.OpenNoteAsync(notePath);
        await second.OpenNoteAsync(notePath);
        second.EditorBody = "pending edit from second window";
        first.EditorBody = "saved by first window";

        Assert.True(await first.PrepareToCloseAsync());
        await WaitForConditionAsync(() => second.HasConflict);
        await Task.Delay(600);
        var saved = await repository.LoadNoteAsync(notePath);

        Assert.Equal("saved by first window", saved?.Body);
        Assert.True(second.HasUnsavedChanges);
        Assert.False(await second.PrepareToCloseAsync());
        Assert.Contains("conflict", second.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveCompletion_DoesNotReplaceEditorChangesMadeWhileSaveWasRunning()
    {
        Directory.CreateDirectory(_tempRoot);
        var notePath = Path.Combine(_tempRoot, "alpha.md");
        await File.WriteAllTextAsync(notePath, "original");
        var repository = new BlockingSaveNotesRepository(new NotesRepository());
        var mutationService = new NoteMutationService(repository);

        using var vm = await CreateViewModelAsync(
            repository: repository,
            noteMutationService: mutationService,
            folderOverride: _tempRoot);
        await vm.OpenNoteAsync(notePath);
        repository.BlockNextSave();
        vm.EditorBody = "first edit";
        await repository.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        vm.EditorBody = "newer edit";
        repository.ReleaseSave();
        await Task.Delay(1200);
        var saved = await repository.LoadNoteAsync(notePath);

        Assert.Equal("newer edit", vm.EditorBody);
        Assert.Equal("newer edit", vm.CurrentNote?.Body);
        Assert.Equal("newer edit", saved?.Body);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.HasConflict);
    }

    [Fact]
    public void AreSameNotesFolder_NormalizesPathsAndRejectsDifferentFolders()
    {
        var nested = Path.Combine(_tempRoot, "nested");

        Assert.True(MainViewModel.AreSameNotesFolder(_tempRoot, _tempRoot + Path.DirectorySeparatorChar));
        Assert.False(MainViewModel.AreSameNotesFolder(_tempRoot, nested));

        var upperCaseFolder = Path.Combine(_tempRoot, "CaseSensitive");
        var lowerCaseFolder = Path.Combine(_tempRoot, "casesensitive");
        Assert.Equal(
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(),
            MainViewModel.AreSameNotesFolder(upperCaseFolder, lowerCaseFolder));
    }

    [Fact]
    public async Task ActivatePane_UpdatesSidebarSelectionWithoutReloadingAnotherPane()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.OpenSidebarNoteCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "alpha"));
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1 && vm.SecondaryPanes[0].CurrentNote?.Title == "beta");

        vm.ActivatePane(vm.SecondaryPanes[0]);

        Assert.Equal("beta", vm.SelectedVisibleNote?.DisplayName);
        Assert.Equal("alpha", vm.CurrentNote?.Title);
        Assert.Equal("beta", vm.SecondaryPanes[0].CurrentNote?.Title);
    }

    [Fact]
    public async Task ClosePaneCommand_ActivatesLeftNeighbor()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));
        await WriteNoteAsync("gamma.md", "gamma", "body gamma", createdAt: new DateTime(2026, 3, 11, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SelectedVisibleNote = vm.VisibleNotes.First(note => note.DisplayName == "alpha");
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "gamma"));
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 2);

        Assert.Equal(new[] { "gamma", "beta" }, vm.SecondaryPanes.Select(pane => pane.CurrentNote?.Title).ToArray());

        vm.ActivatePane(vm.SecondaryPanes[1]);
        await vm.ClosePaneCommand.ExecuteAsync(vm.SecondaryPanes[1]);

        Assert.Single(vm.SecondaryPanes);
        Assert.Equal("gamma", vm.ActiveSecondaryPane?.CurrentNote?.Title);
    }

    [Fact]
    public async Task CloseActivePaneAsync_WhenPrimaryIsActive_PromotesNextPane()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SelectedVisibleNote = vm.VisibleNotes.First(note => note.DisplayName == "alpha");
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");
        await vm.OpenNoteInSplitCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));
        await WaitForConditionAsync(() => vm.SecondaryPanes.Count == 1);

        vm.ActivatePrimaryPane();
        await vm.CloseActivePaneAsync();

        Assert.Equal("beta", vm.CurrentNote?.Title);
        Assert.False(vm.HasSecondaryPane);
        Assert.True(vm.IsPrimaryPaneActive);
    }

    [Fact]
    public async Task CloseActivePaneAsync_WhenLastPrimaryPaneIsActive_ClearsEditor()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };

        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SelectedVisibleNote = vm.VisibleNotes.First(note => note.DisplayName == "alpha");
        await WaitForConditionAsync(() => vm.CurrentNote?.Title == "alpha");

        await vm.CloseActivePaneAsync();

        Assert.Null(vm.CurrentNote);
        Assert.Equal(string.Empty, vm.EditorBody);
        Assert.True(vm.IsPrimaryPaneActive);
    }

    [Fact]
    public async Task SidebarTree_GroupsNotesUnderNestedTagFolders()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("template.md", "template", "body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0), tags: ["luxoft/template"]);
        await WriteNoteAsync("jql.md", "jql", "body", createdAt: new DateTime(2026, 3, 10, 7, 33, 0), tags: ["luxoft/jql"]);
        await WriteNoteAsync("root.md", "root", "body", createdAt: new DateTime(2026, 3, 11, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "luxoft", "root" }, vm.VisibleSidebarRows.Select(row => row.Label).ToArray());

        var luxoft = vm.VisibleSidebarRows.First();
        luxoft.ToggleExpandedCommand.Execute(null);

        Assert.Equal(new[] { "luxoft", "jql", "template", "root" }, vm.VisibleSidebarRows.Select(row => row.Label).ToArray());
        Assert.Equal(new[] { 0, 1, 1, 0 }, vm.VisibleSidebarRows.Select(row => row.Depth).ToArray());

        vm.VisibleSidebarRows.Single(row => row.Label == "template").ToggleExpandedCommand.Execute(null);
        Assert.Contains(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "template" && row.Depth == 2);
    }

    [Fact]
    public async Task OpenSidebarNoteCommand_WhenTreeStructureIsUnchanged_PreservesRowInstances()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", createdAt: new DateTime(2026, 3, 9, 7, 33, 0));
        await WriteNoteAsync("beta.md", "beta", "body beta", createdAt: new DateTime(2026, 3, 10, 7, 33, 0));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var rows = vm.VisibleSidebarRows;
        var rowInstances = rows.ToArray();

        await vm.OpenSidebarNoteCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "alpha"));
        await vm.OpenSidebarNoteCommand.ExecuteAsync(vm.VisibleNotes.First(note => note.DisplayName == "beta"));

        Assert.Same(rows, vm.VisibleSidebarRows);
        Assert.Equal(rowInstances.Length, vm.VisibleSidebarRows.Count);
        for (var i = 0; i < rowInstances.Length; i++)
        {
            Assert.Same(rowInstances[i], vm.VisibleSidebarRows[i]);
        }
    }

    [Fact]
    public async Task SidebarTree_SearchAutomaticallyExpandsMatchingBranches()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("template.md", "template", "body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0), tags: ["luxoft/template"]);
        await WriteNoteAsync("other.md", "other", "body", createdAt: new DateTime(2026, 3, 10, 7, 33, 0), tags: ["other"]);

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SearchText = "template";

        Assert.Equal(new[] { "luxoft", "template", "template" }, vm.VisibleSidebarRows.Select(row => row.Label).ToArray());
        Assert.True(vm.VisibleSidebarRows[0].IsFolder);
        Assert.True(vm.VisibleSidebarRows[1].IsFolder);
        Assert.True(vm.VisibleSidebarRows[2].IsNote);
    }

    [Fact]
    public async Task SidebarTree_CollapsedBranchContainingActiveNoteStaysCollapsed()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("nested.md", "nested", "body", new DateTime(2026, 3, 9), ["work/projects"]);
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var work = vm.VisibleSidebarRows.Single(row => row.TagPath == "work");
        work.ToggleExpandedCommand.Execute(null);
        var projects = vm.VisibleSidebarRows.Single(row => row.TagPath == "work/projects");
        projects.ToggleExpandedCommand.Execute(null);
        var note = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "nested");
        await vm.OpenSidebarNoteCommand.ExecuteAsync(note.Note);

        vm.VisibleSidebarRows.Single(row => row.TagPath == "work").ToggleExpandedCommand.Execute(null);

        Assert.False(vm.VisibleSidebarRows.Single(row => row.TagPath == "work").IsExpanded);
        Assert.DoesNotContain(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "nested");
        Assert.Equal("nested", vm.CurrentNote?.Title);
    }

    [Fact]
    public async Task SidebarTree_ExplicitCollapseOverridesSearchAutoExpansion()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("nested.md", "nested", "matching body", new DateTime(2026, 3, 9), ["work/projects"]);
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SearchText = "nested";
        vm.VisibleSidebarRows.Single(row => row.TagPath == "work").ToggleExpandedCommand.Execute(null);

        Assert.False(vm.VisibleSidebarRows.Single(row => row.TagPath == "work").IsExpanded);
        Assert.DoesNotContain(vm.VisibleSidebarRows, row => row.TagPath == "work/projects");
    }

    [Fact]
    public async Task FocusSidebarFolder_ShowsWholeSubtreeAndShowAllRestoresRoot()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("direct.md", "direct", "body", new DateTime(2026, 3, 9), ["work"]);
        await WriteNoteAsync("nested.md", "nested", "body", new DateTime(2026, 3, 10), ["work/projects"]);
        await WriteNoteAsync("personal.md", "personal", "body", new DateTime(2026, 3, 11), ["personal"]);
        await WriteNoteAsync("root.md", "root", "body", new DateTime(2026, 3, 12));
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.FocusSidebarFolderCommand.Execute("work");

        Assert.True(vm.IsSidebarFolderFocused);
        Assert.Equal("Focused: work", vm.FocusedSidebarFolderLabel);
        Assert.Equal(new[] { "projects", "direct" }, vm.VisibleSidebarRows.Select(row => row.Label).ToArray());
        vm.VisibleSidebarRows.Single(row => row.TagPath == "work/projects").ToggleExpandedCommand.Execute(null);
        Assert.Contains(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "nested");
        Assert.DoesNotContain(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "personal");
        Assert.DoesNotContain(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "root");

        vm.ClearSidebarFolderFocusCommand.Execute(null);

        Assert.False(vm.IsSidebarFolderFocused);
        Assert.Contains(vm.VisibleSidebarRows, row => row.TagPath == "personal");
        Assert.Contains(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "root");
    }

    [Fact]
    public async Task FocusSidebarFolder_PreservesSearchAndDoesNotCloseOutsideActiveNote()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("matching.md", "matching", "needle", new DateTime(2026, 3, 9), ["work/projects"]);
        await WriteNoteAsync("hidden.md", "hidden", "other", new DateTime(2026, 3, 10), ["work"]);
        await WriteNoteAsync("personal.md", "personal", "needle", new DateTime(2026, 3, 11), ["personal"]);
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(vm.VisibleNotes.Single(note => note.DisplayName == "personal"));

        vm.SearchText = "matching";
        vm.FocusSidebarFolderCommand.Execute("work");

        Assert.Equal("personal", vm.CurrentNote?.Title);
        Assert.Contains(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "matching");
        Assert.DoesNotContain(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "hidden");
        Assert.DoesNotContain(vm.VisibleSidebarRows, row => row.Note?.DisplayName == "personal");
    }

    [Fact]
    public async Task FocusSidebarFolder_RenameUpdatesFocusAndDeleteClearsIt()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("note.md", "note", "body", new DateTime(2026, 3, 9), ["work/projects"]);
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            RenameTagFolderResult = "archive",
            ConfirmDeleteTagFolderResult = true
        };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.FocusSidebarFolderCommand.Execute("work");

        await vm.RenameTagFolderCommand.ExecuteAsync("work");

        Assert.Equal("archive", vm.FocusedSidebarTagPath);
        Assert.True(vm.IsSidebarFolderFocused);

        await vm.DeleteTagFolderCommand.ExecuteAsync("archive");

        Assert.False(vm.IsSidebarFolderFocused);
        Assert.Null(vm.FocusedSidebarTagPath);
    }

    [Fact]
    public async Task FocusSidebarFolder_NoSearchMatchesKeepsFocusUntilWorkspaceChanges()
    {
        var firstFolder = Path.Combine(_tempRoot, "first");
        var secondFolder = Path.Combine(_tempRoot, "second");
        Directory.CreateDirectory(firstFolder);
        Directory.CreateDirectory(secondFolder);
        await File.WriteAllTextAsync(Path.Combine(firstFolder, "note.md"), "---\ntitle: note\ntags: [work]\n---\nbody");
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = firstFolder };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.FocusSidebarFolderCommand.Execute("work");

        vm.SearchText = "no-match";

        Assert.True(vm.IsSidebarFolderFocused);
        Assert.Empty(vm.VisibleSidebarRows);

        dialogService.FolderToPick = secondFolder;
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.False(vm.IsSidebarFolderFocused);
    }

    [Fact]
    public async Task SidebarSelection_ShiftRangeSkipsFoldersAndDeduplicatesNoteOccurrences()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9), ["one", "two"]);
        await WriteNoteAsync("omega.md", "omega", "body", new DateTime(2026, 3, 10));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.VisibleSidebarRows.First(row => row.IsFolder).ToggleExpandedCommand.Execute(null);
        vm.VisibleSidebarRows.First(row => row.IsFolder && !row.IsExpanded).ToggleExpandedCommand.Execute(null);
        var alphaOccurrences = vm.VisibleSidebarRows.Where(row => row.Note?.DisplayName == "alpha").ToList();
        var omega = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "omega");

        vm.SelectOnlySidebarNote(alphaOccurrences[0]);
        vm.SelectSidebarNoteRange(omega);

        Assert.Equal(2, vm.SelectedSidebarNotes.Count);
        Assert.Single(vm.SelectedSidebarNotes.Where(note => note.DisplayName == "alpha"));
        Assert.Contains(vm.SelectedSidebarNotes, note => note.DisplayName == "omega");
    }

    [Fact]
    public async Task ClearAdditionalSidebarSelection_KeepsActiveNoteAndRemovesOtherNotes()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", new DateTime(2026, 3, 9));
        await WriteNoteAsync("beta.md", "beta", "body beta", new DateTime(2026, 3, 10));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        var alphaRow = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "alpha");
        var betaRow = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alphaRow);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(alphaRow.Note);
        await WaitForConditionAsync(() => vm.CurrentNote?.FilePath == alphaRow.Note!.FilePath);
        vm.ToggleSidebarNoteSelection(betaRow);

        Assert.Equal(2, vm.SelectedSidebarNotes.Count);
        Assert.True(vm.ClearAdditionalSidebarSelection());

        Assert.Equal("alpha", Assert.Single(vm.SelectedSidebarNotes).DisplayName);
        Assert.Same(alphaRow, vm.SelectedSidebarRow);
        Assert.True(alphaRow.Note!.IsSelected);
        Assert.False(betaRow.Note!.IsSelected);
        Assert.False(vm.ClearAdditionalSidebarSelection());
    }

    [Fact]
    public async Task RestoreSidebarSelection_RevertsTemporaryContextMenuSelection()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9));
        await WriteNoteAsync("beta.md", "beta", "body", new DateTime(2026, 3, 10));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var alphaRow = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "alpha");
        var betaRow = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alphaRow);
        var selectionBeforeContextMenu = vm.CaptureSidebarSelection();
        vm.SelectOnlySidebarNote(betaRow);

        vm.RestoreSidebarSelection(selectionBeforeContextMenu);

        Assert.Equal("alpha", Assert.Single(vm.SelectedSidebarNotes).DisplayName);
        Assert.Same(alphaRow, vm.SelectedSidebarRow);
        Assert.True(alphaRow.Note!.IsSelected);
        Assert.False(betaRow.Note!.IsSelected);
    }

    [Fact]
    public async Task AddSelectedNotesToTagFolderCommand_UsesSelectionCapturedBeforeDialogCompletes()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9));
        await WriteNoteAsync("beta.md", "beta", "body", new DateTime(2026, 3, 10));

        var destinationSource = new TaskCompletionSource<string?>();
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            TagFolderDestinationSource = destinationSource
        };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var alphaRow = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "alpha");
        var betaRow = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alphaRow);
        var selectionBeforeContextMenu = vm.CaptureSidebarSelection();
        vm.SelectOnlySidebarNote(betaRow);

        var addTask = vm.AddSelectedNotesToTagFolderCommand.ExecuteAsync(null);
        vm.RestoreSidebarSelection(selectionBeforeContextMenu);
        destinationSource.SetResult("work");
        await addTask;

        var repository = new NotesRepository();
        var alpha = await repository.LoadNoteAsync(Path.Combine(_tempRoot, "alpha.md"));
        var beta = await repository.LoadNoteAsync(Path.Combine(_tempRoot, "beta.md"));
        Assert.Empty(alpha!.Tags);
        Assert.Contains("work", beta!.Tags);
        Assert.Equal("alpha", Assert.Single(vm.SelectedSidebarNotes).DisplayName);
    }

    [Fact]
    public async Task CreateTagFolderCommand_PersistsAndShowsEmptyFolder()
    {
        Directory.CreateDirectory(_tempRoot);
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            CreateTagFolderResult = "work/projects"
        };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.CreateTagFolderCommand.ExecuteAsync(null);

        Assert.Contains(vm.VisibleSidebarRows, row => row.IsFolder && row.TagPath == "work");
        Assert.Contains(vm.VisibleSidebarRows, row => row.IsFolder && row.TagPath == "work/projects");
        var saved = await new TagFolderCatalogService().LoadAsync(_tempRoot);
        Assert.Contains("work/projects", saved);
    }

    [Fact]
    public async Task AddSidebarSelectionToTagFolderAsync_AddsTagToEverySelectedNote()
    {
        Directory.CreateDirectory(_tempRoot);
        var timestamp = new DateTime(2026, 3, 9, 7, 33, 0);
        await WriteNoteAsync("alpha.md", "alpha", "body alpha", timestamp);
        await WriteNoteAsync("beta.md", "beta", "body beta", timestamp.AddDays(1));

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var alpha = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "alpha");
        var beta = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alpha);
        vm.ToggleSidebarNoteSelection(beta);

        await vm.AddSidebarSelectionToTagFolderAsync("work/projects");

        var repository = new NotesRepository();
        var updatedAlpha = await repository.LoadNoteAsync(Path.Combine(_tempRoot, "alpha.md"));
        var updatedBeta = await repository.LoadNoteAsync(Path.Combine(_tempRoot, "beta.md"));
        Assert.Contains("work/projects", updatedAlpha!.Tags);
        Assert.Contains("work/projects", updatedBeta!.Tags);
        Assert.Equal(timestamp.AddMinutes(1), updatedAlpha.UpdatedAt);
        Assert.Equal(timestamp.AddDays(1).AddMinutes(1), updatedBeta.UpdatedAt);
        Assert.Equal(2, vm.SelectedSidebarNotes.Count);
    }

    [Fact]
    public async Task AddSidebarNotesToTagFolderAsync_UsesDragPayloadInsteadOfCurrentSelection()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9));
        await WriteNoteAsync("beta.md", "beta", "body", new DateTime(2026, 3, 10));
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        var alpha = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "alpha");
        var beta = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alpha);
        var payloadPaths = new[] { alpha.Note!.FilePath };
        vm.SelectOnlySidebarNote(beta);

        await vm.AddSidebarNotesToTagFolderAsync(payloadPaths, "work");

        var repository = new NotesRepository();
        Assert.Contains("work", (await repository.LoadNoteAsync(alpha.Note.FilePath))!.Tags);
        Assert.Empty((await repository.LoadNoteAsync(beta.Note!.FilePath))!.Tags);
    }

    [Fact]
    public async Task MoveSidebarSelectionToRootAsync_RemovesAllTagsAndKeepsNoteSelected()
    {
        Directory.CreateDirectory(_tempRoot);
        var timestamp = new DateTime(2026, 3, 9, 7, 33, 0);
        await WriteNoteAsync("alpha.md", "alpha", "body", timestamp, ["work/projects", "pinned"]);

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.VisibleSidebarRows.First(row => row.IsFolder).ToggleExpandedCommand.Execute(null);
        var alpha = vm.VisibleSidebarRows.First(row => row.Note?.DisplayName == "alpha");
        vm.SelectOnlySidebarNote(alpha);
        Assert.True(vm.CanMoveSelectedNotesToRoot);

        await vm.MoveSidebarSelectionToRootAsync();

        var updated = await new NotesRepository().LoadNoteAsync(Path.Combine(_tempRoot, "alpha.md"));
        Assert.Empty(updated!.Tags);
        Assert.Equal(timestamp.AddMinutes(1), updated.UpdatedAt);
        Assert.Equal("alpha", Assert.Single(vm.SelectedSidebarNotes).DisplayName);
        Assert.Contains(vm.VisibleSidebarRows, row => row.Depth == 0 && row.Note?.DisplayName == "alpha");
        Assert.False(vm.CanMoveSelectedNotesToRoot);
    }

    [Fact]
    public async Task MoveSidebarNotesToRootAsync_UsesDragPayloadInsteadOfCurrentSelection()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9), ["work"]);
        await WriteNoteAsync("beta.md", "beta", "body", new DateTime(2026, 3, 10), ["personal"]);
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        foreach (var folder in vm.VisibleSidebarRows.Where(row => row.IsFolder).ToList())
        {
            folder.ToggleExpandedCommand.Execute(null);
        }
        var alpha = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "alpha");
        var beta = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alpha);
        var payloadPaths = new[] { alpha.Note!.FilePath };
        vm.SelectOnlySidebarNote(beta);

        await vm.MoveSidebarNotesToRootAsync(payloadPaths);

        var repository = new NotesRepository();
        Assert.Empty((await repository.LoadNoteAsync(alpha.Note.FilePath))!.Tags);
        Assert.Equal(["personal"], (await repository.LoadNoteAsync(beta.Note!.FilePath))!.Tags);
    }

    [Fact]
    public async Task DeleteSelectedSidebarNotesCommand_DeletesDistinctPhysicalFiles()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9), ["one", "two"]);
        await WriteNoteAsync("beta.md", "beta", "body", new DateTime(2026, 3, 10));
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            ConfirmDeleteNotesResult = true
        };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.VisibleSidebarRows.First(row => row.IsFolder).ToggleExpandedCommand.Execute(null);
        var alpha = vm.VisibleSidebarRows.First(row => row.Note?.DisplayName == "alpha");
        var beta = vm.VisibleSidebarRows.Single(row => row.Note?.DisplayName == "beta");
        vm.SelectOnlySidebarNote(alpha);
        vm.ToggleSidebarNoteSelection(beta);

        await vm.DeleteSelectedSidebarNotesCommand.ExecuteAsync(null);

        Assert.False(File.Exists(Path.Combine(_tempRoot, "alpha.md")));
        Assert.False(File.Exists(Path.Combine(_tempRoot, "beta.md")));
        Assert.Equal(2, dialogService.LastDeleteNotes.Count);
        Assert.Empty(vm.VisibleNotes);
    }

    [Fact]
    public async Task RenameTagFolderCommand_UpdatesDescendantTagsAndPreservesTimestamp()
    {
        Directory.CreateDirectory(_tempRoot);
        var timestamp = new DateTime(2026, 3, 9, 7, 33, 0);
        await WriteNoteAsync("alpha.md", "alpha", "body", timestamp, ["work/projects", "pinned"]);
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            RenameTagFolderResult = "archive"
        };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.RenameTagFolderCommand.ExecuteAsync("work");

        var updated = await new NotesRepository().LoadNoteAsync(Path.Combine(_tempRoot, "alpha.md"));
        Assert.Contains("archive/projects", updated!.Tags);
        Assert.Contains("pinned", updated.Tags);
        Assert.DoesNotContain("work/projects", updated.Tags);
        Assert.Equal(timestamp.AddMinutes(1), updated.UpdatedAt);
    }

    [Fact]
    public async Task DeleteTagFolderCommand_RemovesMatchingTagsButKeepsNotes()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("alpha.md", "alpha", "body", new DateTime(2026, 3, 9), ["work/projects", "pinned"]);
        var dialogService = new FakeWorkspaceDialogService
        {
            FolderToPick = _tempRoot,
            ConfirmDeleteTagFolderResult = true
        };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        await vm.DeleteTagFolderCommand.ExecuteAsync("work");

        var notePath = Path.Combine(_tempRoot, "alpha.md");
        Assert.True(File.Exists(notePath));
        var updated = await new NotesRepository().LoadNoteAsync(notePath);
        Assert.Equal(["pinned"], updated!.Tags);
        Assert.DoesNotContain(vm.TagFolderPaths, path => path.StartsWith("work", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChooseFolderCommand_ClearsSidebarSelectionFromPreviousWorkspace()
    {
        var firstFolder = Path.Combine(_tempRoot, "first");
        var secondFolder = Path.Combine(_tempRoot, "second");
        Directory.CreateDirectory(firstFolder);
        Directory.CreateDirectory(secondFolder);
        await File.WriteAllTextAsync(Path.Combine(firstFolder, "first.md"), "first");
        await File.WriteAllTextAsync(Path.Combine(secondFolder, "second.md"), "second");
        var dialogService = new FakeWorkspaceDialogService { FolderToPick = firstFolder };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.SelectOnlySidebarNote(Assert.Single(vm.VisibleSidebarRows.Where(row => row.IsNote)));

        dialogService.FolderToPick = secondFolder;
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Empty(vm.SelectedSidebarNotes);
        Assert.Equal("second", Assert.Single(vm.VisibleNotes).DisplayName);
    }

    [Fact]
    public async Task SidebarTree_KeepsTagCatalogForEditorSuggestions()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("template.md", "template", "body", createdAt: new DateTime(2026, 3, 9, 7, 33, 0), tags: ["luxoft/template"]);

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        using var vm = await CreateViewModelAsync(dialogService: dialogService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Contains("luxoft", vm.AvailableTags);
        Assert.Contains("luxoft/template", vm.AvailableTags);
    }

    [Fact]
    public async Task EditorTags_DoNotPersistUntilConfirmed()
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

        vm.EditorTags = "alpha, beta";
        await Task.Delay(700);

        Assert.Equal(new[] { "alpha" }, vm.CurrentNote!.Tags);
        var beforeCommit = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "note.md"));
        Assert.DoesNotContain("beta", beforeCommit, StringComparison.Ordinal);

        await vm.ConfirmEditorTagsCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "alpha", "beta" }, vm.CurrentNote.Tags);
        Assert.Equal(
            new[] { "alpha", "beta" },
            vm.VisibleSidebarRows.Where(row => row.IsFolder).Select(row => row.Label).ToArray());
        var afterCommit = await File.ReadAllTextAsync(Path.Combine(_tempRoot, "note.md"));
        Assert.Contains("beta", afterCommit, StringComparison.Ordinal);
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

        await vm.OpenSidebarNoteCommand.ExecuteAsync(first);
        vm.TitleSuggestionsContext = "Prefer concise release note naming.";

        await vm.OpenSidebarNoteCommand.ExecuteAsync(second);

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
        await settingsService.UpdateSettingsAsync(s => s with { AiSettings = new AiSettings("secret", "gpt-5.6-terra", false) });
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline", "meeting-summary", "deployment-checklist"]
        };

        using var vm = await CreateViewModelAsync(dialogService: dialogService, settingsService: settingsService, aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(Assert.Single(vm.VisibleNotes));

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Empty(vm.TitleSuggestions);
        Assert.Equal("AI is disabled in settings.", vm.StatusMessage);
        Assert.Null(aiTitleSuggestionService.LastDocument);
    }

    [Fact]
    public async Task GenerateTitleSuggestionsCommand_DoesNotRunWhenTitleGenerationIsDisabled()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "note.md"), "body");

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        var settingsService = new FakeSettingsService();
        await settingsService.UpdateSettingsAsync(settings => settings with
        {
            AiSettings = settings.AiSettings with
            {
                TitleGeneration = new AiTitleGenerationSettings(false, "gpt-5.6-terra")
            }
        });
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline"]
        };

        using var vm = await CreateViewModelAsync(
            dialogService: dialogService,
            settingsService: settingsService,
            aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(Assert.Single(vm.VisibleNotes));

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Empty(vm.TitleSuggestions);
        Assert.Equal("AI title generation is disabled in settings.", vm.StatusMessage);
        Assert.Null(aiTitleSuggestionService.LastDocument);
    }

    [Fact]
    public async Task GenerateTitleSuggestionsCommand_PassesConfiguredTitleGenerationModel()
    {
        Directory.CreateDirectory(_tempRoot);
        await File.WriteAllTextAsync(Path.Combine(_tempRoot, "note.md"), "body");

        var dialogService = new FakeWorkspaceDialogService { FolderToPick = _tempRoot };
        var settingsService = new FakeSettingsService();
        await settingsService.UpdateSettingsAsync(settings => settings with
        {
            AiSettings = settings.AiSettings with
            {
                TitleGeneration = new AiTitleGenerationSettings(true, "gpt-5.6-luna", "high")
            }
        });
        var aiTitleSuggestionService = new FakeAiTitleSuggestionService
        {
            Suggestions = ["project-outline"]
        };

        using var vm = await CreateViewModelAsync(
            dialogService: dialogService,
            settingsService: settingsService,
            aiTitleSuggestionService: aiTitleSuggestionService);
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await vm.OpenSidebarNoteCommand.ExecuteAsync(Assert.Single(vm.VisibleNotes));

        await vm.GenerateTitleSuggestionsCommand.ExecuteAsync(null);

        Assert.Equal("gpt-5.6-luna", aiTitleSuggestionService.LastSettings?.TitleGeneration.DefaultModel);
        Assert.Equal("high", aiTitleSuggestionService.LastSettings?.TitleGeneration.DefaultReasoningEffort);
    }

    [Fact]
    public async Task SelectCalendarDayCommand_FiltersByCreatedDate_AndSecondClickClearsFilter()
    {
        Directory.CreateDirectory(_tempRoot);
        await WriteNoteAsync("march-9.md", "march-9", "release planning", createdAt: new DateTime(2026, 3, 9, 7, 33, 0), tags: ["luxoft/template"]);
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
        Assert.Equal(new[] { "luxoft", "template", "march-9" }, vm.VisibleSidebarRows.Select(row => row.Label).ToArray());

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
    public async Task ApplySettingsLive_AppliesCodeFontImmediately()
    {
        var appearanceService = new FakeAppAppearanceService();
        var editorLayoutState = new FakeEditorLayoutState();
        using var vm = await CreateViewModelAsync(appearanceService: appearanceService, editorLayoutState: editorLayoutState);

        var callsBefore = appearanceService.ApplyCodeFontCallCount;

        var model = new SettingsDialogModel(
            ["Default"],
            new FakeFontCatalogService().LoadBundledFonts(),
            "Default",
            "JetBrains Mono",
            FontCatalogService.DefaultVariantKey,
            "Iosevka Slab",
            FontCatalogService.DefaultVariantKey,
            "JetBrains Mono",
            FontCatalogService.DefaultVariantKey,
            12,
            12,
            10,
            false,
            true,
            2,
            1.3,
            true,
            true,
            string.Empty,
            "gpt-5.6-terra",
            "none",
            string.Empty,
            string.Empty,
            true,
            "gpt-5-mini",
            "none",
            string.Empty,
            [],
            KeyboardShortcutSettings.CreateDefault());

        vm.ApplySettingsLive(model);

        Assert.Equal(callsBefore + 1, appearanceService.ApplyCodeFontCallCount);
        Assert.Equal(10, appearanceService.LastFileListFontSize);
        Assert.Equal("JetBrains Mono", appearanceService.LastUiFontFamilyName);
        Assert.False(vm.ShowSidebarListBackground);
        Assert.True(vm.ShowSidebarListBorder);
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
        FakeAiTitleSuggestionService? aiTitleSuggestionService = null,
        INotesRepository? repository = null,
        INoteMutationService? noteMutationService = null,
        string? folderOverride = null)
    {
        dialogService ??= new FakeWorkspaceDialogService();
        chatViewModelFactory ??= new FakeChatViewModelFactory();
        appearanceService ??= new FakeAppAppearanceService();
        editorLayoutState ??= new FakeEditorLayoutState();
        settingsService ??= new FakeSettingsService();
        aiTitleSuggestionService ??= new FakeAiTitleSuggestionService();

        repository ??= new NotesRepository();
        var fileWatcherService = new FakeFileWatcherService();
        noteMutationService ??= new NoteMutationService(repository);
        var noteSearchServiceFactory = new NoteSearchServiceFactory(repository);
        var vm = new MainViewModel(
            repository,
            settingsService,
            fileWatcherService,
            new FakeThemeLoaderService(),
            new FakeFontCatalogService(),
            new FakeAiPromptCatalogService(),
            new FakeAiPromptEditorService(),
            new FakeAiTextActionService(),
            aiTitleSuggestionService,
            noteMutationService,
            dialogService,
            appearanceService,
            editorLayoutState,
            chatViewModelFactory,
            new KeyboardShortcutService(),
            noteSearchServiceFactory,
            new TagFolderCatalogService());

        if (string.IsNullOrWhiteSpace(folderOverride))
        {
            await vm.InitializeAsync();
        }
        else
        {
            await vm.InitializeForFolderAsync(folderOverride);
        }
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

        public string? CreateTagFolderResult { get; set; }

        public string? RenameTagFolderResult { get; set; }

        public string? TagFolderDestinationResult { get; set; }

        public TaskCompletionSource<string?>? TagFolderDestinationSource { get; set; }

        public bool ConfirmDeleteTagFolderResult { get; set; }

        public bool ConfirmDeleteNotesResult { get; set; }

        public IReadOnlyList<string> LastDeleteNotes { get; private set; } = [];

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

        public Task<string?> PromptCreateTagFolderAsync() => Task.FromResult(CreateTagFolderResult);

        public Task<string?> PromptRenameTagFolderAsync(string currentPath) => Task.FromResult(RenameTagFolderResult);

        public Task<string?> ChooseTagFolderDestinationAsync(IReadOnlyList<string> folderPaths) =>
            TagFolderDestinationSource?.Task ?? Task.FromResult(TagFolderDestinationResult);

        public Task<bool> ConfirmDeleteTagFolderAsync(string folderPath) => Task.FromResult(ConfirmDeleteTagFolderResult);

        public Task<bool> ConfirmDeleteNotesAsync(IReadOnlyList<string> noteNames)
        {
            LastDeleteNotes = noteNames;
            return Task.FromResult(ConfirmDeleteNotesResult);
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

        public Task ShowSettingsAsync(SettingsDialogModel model, Action<SettingsDialogModel> onChange, SettingsPromptActions promptActions)
        {
            return Task.CompletedTask;
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
                ["gpt-5.6-terra"],
                originNote,
                initialNotes);
        }
    }

    private sealed class FakeAppAppearanceService : IAppAppearanceService
    {
        public int ApplyCodeFontCallCount { get; private set; }

        public string? LastCodeFontFamilyName { get; private set; }

        public string? LastCodeFontVariantName { get; private set; }

        public double? LastFileListFontSize { get; private set; }

        public string? LastUiFontFamilyName { get; private set; }

        public void ApplyTheme(GroundNotes.Styles.AppTheme theme)
        {
        }

        public void ApplyUiFontSize(double fontSize)
        {
        }

        public void ApplyUiFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant)
        {
            LastUiFontFamilyName = fontFamily.DisplayName;
        }

        public void ApplyFileListFontSize(double fontSize)
        {
            LastFileListFontSize = fontSize;
        }

        public void ApplyTerminalFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant)
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

    private sealed class BlockingSaveNotesRepository : INotesRepository
    {
        private readonly INotesRepository _inner;
        private TaskCompletionSource _releaseSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _blockNextSave;

        public BlockingSaveNotesRepository(INotesRepository inner)
        {
            _inner = inner;
        }

        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void BlockNextSave()
        {
            _blockNextSave = true;
            _releaseSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void ReleaseSave() => _releaseSave.TrySetResult();

        public Task<IReadOnlyList<NoteSummary>> LoadSummariesAsync(string folderPath, CancellationToken cancellationToken = default)
            => _inner.LoadSummariesAsync(folderPath, cancellationToken);

        public Task<NoteDocument?> LoadNoteAsync(string filePath, CancellationToken cancellationToken = default)
            => _inner.LoadNoteAsync(filePath, cancellationToken);

        public NoteDocument CreateDraftNote(string folderPath, DateTimeOffset timestamp)
            => _inner.CreateDraftNote(folderPath, timestamp);

        public async Task<NoteDocument> SaveNoteAsync(
            string folderPath,
            NoteDocument document,
            CancellationToken cancellationToken = default,
            bool preserveTimestamp = false)
        {
            if (_blockNextSave)
            {
                _blockNextSave = false;
                SaveStarted.TrySetResult();
                await _releaseSave.Task;
                cancellationToken = CancellationToken.None;
            }

            return await _inner.SaveNoteAsync(folderPath, document, cancellationToken, preserveTimestamp);
        }

        public Task<NoteDocument> RenameNoteAsync(
            string folderPath,
            NoteDocument document,
            string newTitle,
            CancellationToken cancellationToken = default)
            => _inner.RenameNoteAsync(folderPath, document, newTitle, cancellationToken);

        public Task DeleteNoteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
            => _inner.DeleteNoteIfExistsAsync(filePath, cancellationToken);

        public IReadOnlyList<NoteSummary> QueryNotes(
            IEnumerable<NoteSummary> notes,
            string searchText,
            DateTime? selectedDate,
            SortOption sortOption)
            => _inner.QueryNotes(notes, searchText, selectedDate, sortOption);

        public IReadOnlyList<NoteSummary> QueryNotesForPicker(
            IEnumerable<NoteSummary> notes,
            string searchText,
            int maxResults)
            => _inner.QueryNotesForPicker(notes, searchText, maxResults);
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
            return Path.Combine(notesFolder, ".groundnotes", "ai-prompts");
        }

        public Task<AiPromptCatalogLoadResult> LoadPromptsAsync(string? notesFolder, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiPromptCatalogLoadResult([], []));
        }
    }

    private sealed class FakeAiPromptEditorService : IAiPromptEditorService
    {
        public Task SaveCustomPromptAsync(string notesFolder, AiPromptDefinition prompt, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteCustomPromptAsync(string notesFolder, string promptId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public string GetCustomPromptFilePath(string notesFolder, string promptId)
        {
            return Path.Combine(notesFolder, ".groundnotes", "ai-prompts", promptId + ".json");
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

        public AiSettings? LastSettings { get; private set; }

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
            LastSettings = settings;
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

        public void UpdateSettingsSync(Func<AppSettings, AppSettings> update)
        {
            _settings = update(_settings);
        }

        public Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
        {
            _settings = update(_settings);
            return Task.CompletedTask;
        }

        public Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings.AiSettings);
    }
}
