using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IAiTitleSuggestionService
{
    Task<IReadOnlyList<string>> GetSuggestionsAsync(
        NoteDocument document,
        AiSettings settings,
        string? additionalContext = null,
        CancellationToken cancellationToken = default);
}
