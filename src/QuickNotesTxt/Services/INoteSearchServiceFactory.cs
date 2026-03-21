using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface INoteSearchServiceFactory
{
    INoteSearchService Create(Func<IEnumerable<NoteSummary>> notesProvider);
}
