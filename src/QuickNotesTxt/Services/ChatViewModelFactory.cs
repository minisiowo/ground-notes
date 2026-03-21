using QuickNotesTxt.Models;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Services;

public sealed class ChatViewModelFactory : IChatViewModelFactory
{
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
            AiModelCatalog.ChatCompletionModels,
            originNote,
            initialNotes);
    }
}
