using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class ChatViewModelTests
{
    [Fact]
    public void AppendToFileCommand_IsDisabledWithoutOriginNote()
    {
        var notesRepository = new FakeNotesRepository();
        var vm = CreateViewModel(notesRepository);
        vm.EditorBody = "Edited chat content";

        Assert.False(vm.AppendToFileCommand.CanExecute(null));
    }

    [Fact]
    public void UpdateMentionSuggestions_OpensPopupForMatchingTrigger()
    {
        var notesRepository = new FakeNotesRepository();
        var vm = CreateViewModel(notesRepository, initialNotes: [CreateNoteSummary("alpha")]);

        vm.UpdateMentionSuggestions("@al", 3);

        Assert.True(vm.IsMentionPopupOpen);
        Assert.Single(vm.MentionSuggestions);
        Assert.Equal(0, vm.SelectedMentionIndex);
    }

    [Fact]
    public void UpdateMentionSuggestions_ClosesPopupWhenNoTriggerIsPresent()
    {
        var notesRepository = new FakeNotesRepository();
        var vm = CreateViewModel(notesRepository, initialNotes: [CreateNoteSummary("alpha")]);

        vm.UpdateMentionSuggestions("plain text", 10);

        Assert.False(vm.IsMentionPopupOpen);
        Assert.Empty(vm.MentionSuggestions);
        Assert.Equal(-1, vm.SelectedMentionIndex);
    }

    [Fact]
    public async Task SendMessageAsync_DoesNotPersist_WhenConversationNotSavedYet()
    {
        var notesRepository = new FakeNotesRepository();
        var chatService = new CapturingAiChatService();
        var vm = CreateViewModel(notesRepository, chatService: chatService);

        vm.InputText = "What should I do next?";

        await vm.SendMessageCommand.ExecuteAsync(null);

        Assert.Equal(0, notesRepository.SaveCallCount);
        Assert.Contains("ME:", vm.EditorBody, StringComparison.Ordinal);
        Assert.Contains("CHAT RESPONSE:", vm.EditorBody, StringComparison.Ordinal);
        Assert.Collection(
            chatService.LastHistory!,
            message => Assert.Equal("system", message.Role),
            message =>
            {
                Assert.Equal("user", message.Role);
                Assert.Equal("What should I do next?", message.Content);
            });
    }

    [Fact]
    public async Task SendMessageAsync_IncludesAttachedNoteBodiesInSystemContext()
    {
        var notesRepository = new FakeNotesRepository();
        var linkedNote = CreateNoteSummary("origin-note");
        notesRepository.StoreNote(CreateNoteDocument(linkedNote, "Attached note body"));
        var chatService = new CapturingAiChatService();
        var vm = CreateViewModel(notesRepository, chatService: chatService, initialNotes: [linkedNote]);
        vm.InputText = "Summarize";

        await vm.SendMessageCommand.ExecuteAsync(null);

        var systemMessage = Assert.Single(chatService.LastHistory!.Where(message => message.Role == "system"));
        Assert.Contains("Attached note body", systemMessage.Content, StringComparison.Ordinal);
        Assert.Contains("[File: origin-note]", systemMessage.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveConversationAsync_CreatesNoteAndPersistsBody()
    {
        var notesRepository = new FakeNotesRepository();
        var vm = CreateViewModel(notesRepository);
        vm.EditorBody = "Conversation text";

        await vm.SaveConversationCommand.ExecuteAsync(null);

        Assert.Equal(1, notesRepository.SaveCallCount);
        Assert.NotNull(notesRepository.LastSavedDocument);
        Assert.Contains("Conversation text", notesRepository.LastSavedDocument!.Body, StringComparison.Ordinal);
        Assert.True(vm.IsPersisted);
    }

    [Fact]
    public async Task SaveConversationAsync_AddsLinksForAllAttachedNotes()
    {
        var notesRepository = new FakeNotesRepository();
        var linkedNotes = new[]
        {
            new NoteSummary
            {
                FilePath = Path.Combine("/tmp", "source-note.md"),
                Title = "source-note"
            },
            new NoteSummary
            {
                FilePath = Path.Combine("/tmp", "extra-context.md"),
                Title = "extra-context"
            }
        };

        var vm = CreateViewModel(notesRepository, initialNotes: linkedNotes);
        vm.EditorBody = "Chat body";

        await vm.SaveConversationCommand.ExecuteAsync(null);

        var linkedBody = notesRepository.LastSavedDocument?.Body ?? string.Empty;
        Assert.Contains("Related notes:", linkedBody, StringComparison.Ordinal);
        Assert.Contains("[[source-note]]", linkedBody, StringComparison.Ordinal);
        Assert.Contains("[[extra-context]]", linkedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppendToFileAsync_AppendsEditorBodyToOriginNote()
    {
        var notesRepository = new FakeNotesRepository();
        var mutationService = new FakeNoteMutationService(notesRepository);
        var originNote = CreateNoteSummary("origin-note");
        notesRepository.StoreNote(CreateNoteDocument(originNote, "Existing note body"));
        var vm = CreateViewModel(notesRepository, mutationService: mutationService, originNote: originNote);
        vm.EditorBody = "ME:\n---\nSummarize this\n\nCHAT RESPONSE:\n---\nSummary text";

        await vm.AppendToFileCommand.ExecuteAsync(null);

        Assert.Equal(1, notesRepository.SaveCallCount);
        Assert.NotNull(notesRepository.LastSavedDocument);
        Assert.Equal(originNote.FilePath, notesRepository.LastSavedDocument!.FilePath);
        Assert.Equal("Existing note body\n\nME:\n---\nSummarize this\n\nCHAT RESPONSE:\n---\nSummary text", notesRepository.LastSavedDocument.Body);
        Assert.False(vm.IsPersisted);
    }

    [Fact]
    public async Task AppendToFileAsync_UsesSingleBlankLineGapAfterTrailingWhitespace()
    {
        var notesRepository = new FakeNotesRepository();
        var mutationService = new FakeNoteMutationService(notesRepository);
        var originNote = CreateNoteSummary("origin-note");
        notesRepository.StoreNote(CreateNoteDocument(originNote, "Existing note body\n\n"));
        var vm = CreateViewModel(notesRepository, mutationService: mutationService, originNote: originNote);
        vm.EditorBody = "Edited summary";

        await vm.AppendToFileCommand.ExecuteAsync(null);

        Assert.Equal("Existing note body\n\nEdited summary", notesRepository.LastSavedDocument!.Body);
    }

    [Fact]
    public async Task AppendToFileAsync_WritesEditorBodyDirectlyWhenOriginBodyIsEmpty()
    {
        var notesRepository = new FakeNotesRepository();
        var mutationService = new FakeNoteMutationService(notesRepository);
        var originNote = CreateNoteSummary("origin-note");
        notesRepository.StoreNote(CreateNoteDocument(originNote, string.Empty));
        var vm = CreateViewModel(notesRepository, mutationService: mutationService, originNote: originNote);
        vm.EditorBody = "Edited recap";

        await vm.AppendToFileCommand.ExecuteAsync(null);

        Assert.Equal("Edited recap", notesRepository.LastSavedDocument!.Body);
    }

    [Fact]
    public async Task AppendToFileAsync_FailsWhenOriginNoteCannotBeLoaded()
    {
        var notesRepository = new FakeNotesRepository();
        var mutationService = new FakeNoteMutationService(notesRepository);
        var originNote = CreateNoteSummary("missing-note");
        var vm = CreateViewModel(notesRepository, mutationService: mutationService, originNote: originNote);
        vm.EditorBody = "Edited recap";

        await vm.AppendToFileCommand.ExecuteAsync(null);

        Assert.Equal(0, notesRepository.SaveCallCount);
        Assert.Equal("Error: The original note could not be loaded.", vm.StatusMessage);
    }

    [Fact]
    public void TryAcceptMention_RemovesTriggerAndAddsNoteToContext()
    {
        var notesRepository = new FakeNotesRepository();
        var mentionedNote = CreateNoteSummary("alpha");
        var vm = CreateViewModel(notesRepository, initialNotes: [mentionedNote]);

        vm.UpdateMentionSuggestions("@al", 3);

        var accepted = vm.TryAcceptMention("Hello @al", 9, out var updatedText, out var updatedCaretIndex);

        Assert.True(accepted);
        Assert.Equal("Hello ", updatedText);
        Assert.Equal(6, updatedCaretIndex);
        Assert.Contains(vm.AttachedNotes, note => note.FilePath == mentionedNote.FilePath);
        Assert.False(vm.IsMentionPopupOpen);
        Assert.Empty(vm.MentionSuggestions);
    }

    private static ChatViewModel CreateViewModel(
        FakeNotesRepository notesRepository,
        CapturingAiChatService? chatService = null,
        FakeNoteMutationService? mutationService = null,
        NoteSummary? originNote = null,
        IEnumerable<NoteSummary>? initialNotes = null)
    {
        return new ChatViewModel(
            chatService ?? new CapturingAiChatService(),
            notesRepository,
            new FakeSettingsService(),
            mutationService ?? new FakeNoteMutationService(notesRepository),
            new FakeNoteSearchService(initialNotes ?? []),
            Path.Combine(Path.GetTempPath(), "GroundNotes.Tests"),
            "gpt-5.4-mini",
            ["gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano"],
            originNote,
            initialNotes: initialNotes);
    }

    private static NoteSummary CreateNoteSummary(string name)
    {
        return new NoteSummary
        {
            FilePath = Path.Combine(Path.GetTempPath(), name + ".md"),
            Title = name
        };
    }

    private static NoteDocument CreateNoteDocument(NoteSummary summary, string body)
    {
        return new NoteDocument
        {
            Id = summary.FilePath,
            FilePath = summary.FilePath,
            Title = summary.DisplayName,
            OriginalTitle = summary.DisplayName,
            Body = body,
            Tags = [],
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private sealed class CapturingAiChatService : IAiChatService
    {
        public IReadOnlyList<AiChatMessage>? LastHistory { get; private set; }

        public Task<string> GetResponseAsync(
            IEnumerable<AiChatMessage> history,
            AiSettings settings,
            string model,
            CancellationToken cancellationToken = default)
        {
            LastHistory = history.ToList();
            return Task.FromResult("Assistant reply");
        }
    }

    private sealed class FakeNoteSearchService : INoteSearchService
    {
        private readonly IReadOnlyList<NoteSummary> _results;

        public FakeNoteSearchService(IEnumerable<NoteSummary> results)
        {
            _results = results.ToList();
        }

        public IReadOnlyList<NoteSummary> Search(string query, int maxResults = 10)
        {
            return _results.Take(maxResults).ToList();
        }
    }

    private sealed class FakeNoteMutationService : INoteMutationService
    {
        private readonly FakeNotesRepository _notesRepository;

        public FakeNoteMutationService(FakeNotesRepository notesRepository)
        {
            _notesRepository = notesRepository;
        }

        public event EventHandler<NoteMutationEventArgs>? NoteMutated;

        public async Task<NoteDocument> SaveAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default)
        {
            var saved = await _notesRepository.SaveNoteAsync(folderPath, document, cancellationToken);
            NoteMutated?.Invoke(this, new NoteMutationEventArgs(NoteMutationKind.Saved, document.FilePath, saved));
            return saved;
        }

        public async Task DeleteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            await _notesRepository.DeleteNoteIfExistsAsync(filePath, cancellationToken);
            NoteMutated?.Invoke(this, new NoteMutationEventArgs(NoteMutationKind.Deleted, filePath));
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public AppSettings GetSettingsSync() => new(
            null,
            null,
            null,
            4,
            1.15,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            true,
            null,
            new AiSettings("api-key", "gpt-5.4-mini", true));

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GetSettingsSync());

        public void SaveSettingsSync(AppSettings settings)
        {
        }

        public Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AiSettings("api-key", "gpt-5.4-mini", true));

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNotesRepository : INotesRepository
    {
        private readonly Dictionary<string, NoteDocument> _notes = new(StringComparer.OrdinalIgnoreCase);

        public int SaveCallCount { get; private set; }

        public NoteDocument? LastSavedDocument { get; private set; }

        public void StoreNote(NoteDocument document)
        {
            _notes[document.FilePath] = document;
        }

        public Task<IReadOnlyList<NoteSummary>> LoadSummariesAsync(string folderPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NoteSummary>>([]);

        public Task<NoteDocument?> LoadNoteAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (_notes.TryGetValue(filePath, out var note))
            {
                return Task.FromResult<NoteDocument?>(note with { Tags = [.. note.Tags] });
            }

            return Task.FromResult<NoteDocument?>(null);
        }

        public NoteDocument CreateDraftNote(string folderPath, DateTimeOffset timestamp)
        {
            var baseName = timestamp.ToString("yyyy-MM-dd-HHmm");
            var path = Path.Combine(folderPath, baseName + ".md");
            return new NoteDocument
            {
                Id = path,
                FilePath = path,
                Title = baseName,
                OriginalTitle = baseName,
                Body = string.Empty,
                Tags = [],
                CreatedAt = timestamp.DateTime,
                UpdatedAt = timestamp.DateTime,
                IsAutoCreated = true
            };
        }

        public Task<NoteDocument> SaveNoteAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            LastSavedDocument = document with { Tags = [.. document.Tags], UpdatedAt = DateTime.Now };
            _notes[LastSavedDocument.FilePath] = LastSavedDocument;
            return Task.FromResult(LastSavedDocument);
        }

        public Task<NoteDocument> RenameNoteAsync(string folderPath, NoteDocument document, string newTitle, CancellationToken cancellationToken = default)
            => Task.FromResult(document);

        public Task DeleteNoteIfExistsAsync(string filePath, CancellationToken cancellationToken = default)
        {
            _notes.Remove(filePath);
            return Task.CompletedTask;
        }

        public IReadOnlyList<NoteSummary> QueryNotes(IEnumerable<NoteSummary> notes, string searchText, string? selectedTag, DateTime? selectedDate, SortOption sortOption)
            => notes.ToList();

        public IReadOnlyList<NoteSummary> QueryNotesForPicker(IEnumerable<NoteSummary> notes, string searchText, int maxResults)
            => notes.Take(maxResults).ToList();
    }
}
