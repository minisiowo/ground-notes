using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IOpenAiCompletionsClient
{
    Task<string> CompleteAsync(
        IReadOnlyList<AiChatMessage> messages,
        string model,
        AiSettings settings,
        OpenAiCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
