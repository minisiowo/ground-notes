using QuickNotesTxt.Models;
using QuickNotesTxt.ViewModels;

namespace QuickNotesTxt.Services;

public interface IChatViewModelFactory
{
    ChatViewModel Create(
        string notesFolder,
        string selectedModel,
        Func<IEnumerable<NoteSummary>> allNotesProvider,
        NoteSummary? originNote,
        IEnumerable<NoteSummary>? initialNotes);
}
