using GroundNotes.Models;

namespace GroundNotes.Services;

public interface INotesRepository
{
    Task<IReadOnlyList<NoteSummary>> LoadSummariesAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<NoteDocument?> LoadNoteAsync(string filePath, CancellationToken cancellationToken = default);

    NoteDocument CreateDraftNote(string folderPath, DateTimeOffset timestamp);

    Task<NoteDocument> SaveNoteAsync(string folderPath, NoteDocument document, CancellationToken cancellationToken = default);

    Task<NoteDocument> RenameNoteAsync(string folderPath, NoteDocument document, string newTitle, CancellationToken cancellationToken = default);

    Task DeleteNoteIfExistsAsync(string filePath, CancellationToken cancellationToken = default);

    IReadOnlyList<NoteSummary> QueryNotes(IEnumerable<NoteSummary> notes, string searchText, IReadOnlyList<string> selectedTags, bool matchAllSelectedTags, DateTime? selectedDate, SortOption sortOption);

    IReadOnlyList<NoteSummary> QueryNotesForPicker(IEnumerable<NoteSummary> notes, string searchText, int maxResults);
}
