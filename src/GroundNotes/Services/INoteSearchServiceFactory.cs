using GroundNotes.Models;

namespace GroundNotes.Services;

public interface INoteSearchServiceFactory
{
    INoteSearchService Create(Func<IEnumerable<NoteSummary>> notesProvider);
}
