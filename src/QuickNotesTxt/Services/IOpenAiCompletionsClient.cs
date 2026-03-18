using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IOpenAiCompletionsClient
{
    Task<string> CompleteAsync(
        IReadOnlyList<AiChatMessage> messages,
        string model,
        AiSettings settings,
        OpenAiCompletionOptions? options = null,
        CancellationToken cancellationToken = default);
}
