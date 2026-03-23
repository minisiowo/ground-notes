using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IAiChatService
{
    Task<string> GetResponseAsync(
        IEnumerable<AiChatMessage> history, 
        AiSettings settings, 
        string model, 
        CancellationToken cancellationToken = default);
}
