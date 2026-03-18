using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class OpenAiChatService : IAiChatService
{
    private readonly IOpenAiCompletionsClient _completionsClient;

    public OpenAiChatService(IOpenAiCompletionsClient completionsClient)
    {
        _completionsClient = completionsClient;
    }

    public OpenAiChatService(HttpClient httpClient)
        : this(new OpenAiCompletionsClient(httpClient))
    {
    }

    public Task<string> GetResponseAsync(
        IEnumerable<AiChatMessage> history,
        AiSettings settings,
        string model,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        return _completionsClient.CompleteAsync(history.ToList(), model, settings, cancellationToken: cancellationToken);
    }
}
