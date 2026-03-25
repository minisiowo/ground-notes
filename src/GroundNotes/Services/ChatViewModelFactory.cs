using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Services;

public sealed class ChatViewModelFactory : IChatViewModelFactory
{
    private readonly IAiChatService _aiChatService;
    private readonly INotesRepository _notesRepository;
    private readonly ISettingsService _settingsService;
    private readonly INoteMutationService _noteMutationService;
    private readonly INoteSearchServiceFactory _noteSearchServiceFactory;

    public ChatViewModelFactory(
        IAiChatService aiChatService,
        INotesRepository notesRepository,
        ISettingsService settingsService,
        INoteMutationService noteMutationService,
        INoteSearchServiceFactory noteSearchServiceFactory)
    {
        _aiChatService = aiChatService;
        _notesRepository = notesRepository;
        _settingsService = settingsService;
        _noteMutationService = noteMutationService;
        _noteSearchServiceFactory = noteSearchServiceFactory;
    }

    public ChatViewModel Create(
        string notesFolder,
        string selectedModel,
        Func<IEnumerable<NoteSummary>> allNotesProvider,
        NoteSummary? originNote,
        IEnumerable<NoteSummary>? initialNotes)
    {
        var noteSearchService = _noteSearchServiceFactory.Create(allNotesProvider);

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
