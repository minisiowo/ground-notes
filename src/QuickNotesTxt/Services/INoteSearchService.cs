using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface INoteSearchService
{
    IReadOnlyList<NoteSummary> Search(string query, int maxResults = 10);
}
