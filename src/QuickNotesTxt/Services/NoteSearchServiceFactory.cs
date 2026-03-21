using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class NoteSearchServiceFactory : INoteSearchServiceFactory
{
    private readonly INotesRepository _notesRepository;

    public NoteSearchServiceFactory(INotesRepository notesRepository)
    {
        _notesRepository = notesRepository;
    }

    public INoteSearchService Create(Func<IEnumerable<NoteSummary>> notesProvider)
    {
        return new NoteSearchService(_notesRepository, notesProvider);
    }
}
