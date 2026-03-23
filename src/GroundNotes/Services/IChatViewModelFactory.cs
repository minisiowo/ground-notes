using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Services;

public interface IChatViewModelFactory
{
    ChatViewModel Create(
        string notesFolder,
        string selectedModel,
        Func<IEnumerable<NoteSummary>> allNotesProvider,
        NoteSummary? originNote,
        IEnumerable<NoteSummary>? initialNotes);
}
