using GroundNotes.Models;

namespace GroundNotes.Services;

public interface INoteSearchService
{
    IReadOnlyList<NoteSummary> Search(string query, int maxResults = 10);
}
