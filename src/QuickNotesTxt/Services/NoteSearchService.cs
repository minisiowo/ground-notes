using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class NoteSearchService : INoteSearchService
{
    private readonly INotesRepository _notesRepository;
    private readonly Func<IEnumerable<NoteSummary>> _notesProvider;

    public NoteSearchService(INotesRepository notesRepository, Func<IEnumerable<NoteSummary>> notesProvider)
    {
        _notesRepository = notesRepository;
        _notesProvider = notesProvider;
    }

    public IReadOnlyList<NoteSummary> Search(string query, int maxResults = 10)
    {
        return _notesRepository.QueryNotesForPicker(_notesProvider(), query, maxResults);
    }
}
