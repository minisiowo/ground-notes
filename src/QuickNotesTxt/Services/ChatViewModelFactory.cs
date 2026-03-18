using QuickNotesTxt.Models;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Services;

public sealed class ChatViewModelFactory : IChatViewModelFactory
{
    private static readonly IReadOnlyList<string> s_availableModels = ["gpt-5.4", "gpt-5.4-mini", "gpt-5.4-nano"];

    private readonly IAiChatService _aiChatService;
    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private readonly INoteMutationService _noteMutationService;

    public ChatViewModelFactory(
        IAiChatService aiChatService,
        INotesRepository notesRepository,
        ISettingsService settingsService,
        INoteMutationService noteMutationService)
    {
        _aiChatService = aiChatService;
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _noteMutationService = noteMutationService;
    }

    public ChatViewModel Create(
        string notesFolder,
        string selectedModel,
        Func<IEnumerable<NoteSummary>> allNotesProvider,
        NoteSummary? originNote,
        IEnumerable<NoteSummary>? initialNotes)
    {
        var noteSearchService = new NoteSearchService(_notesRepository, allNotesProvider);

        return new ChatViewModel(
            _aiChatService,
            _notesRepository,
            _settingsService,
            _noteMutationService,
            noteSearchService,
            notesFolder,
            selectedModel,
            s_availableModels,
            originNote,
            initialNotes);
    }
}
