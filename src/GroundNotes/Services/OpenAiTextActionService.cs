using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class OpenAiTextActionService : IAiTextActionService
{
    private readonly IOpenAiCompletionsClient _completionsClient;

    public OpenAiTextActionService(IOpenAiCompletionsClient completionsClient)
    {
        _completionsClient = completionsClient;
    }

    public OpenAiTextActionService(HttpClient httpClient)
        : this(new OpenAiCompletionsClient(httpClient))
    {
    }

    public Task<string> RunPromptAsync(AiPromptDefinition prompt, string selectedText, AiSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            throw new InvalidOperationException("Select text first.");
        }

        var model = !string.IsNullOrWhiteSpace(prompt.Model)
            ? prompt.Model
            : settings.DefaultModel;

        var renderedPrompt = prompt.PromptTemplate.Replace("{selected}", selectedText, StringComparison.Ordinal);
        return _completionsClient.CompleteAsync(
            [new AiChatMessage("user", renderedPrompt)],
            model,
            settings,
            new OpenAiCompletionOptions(prompt.Temperature, prompt.MaxTokens, prompt.ReasoningEffort),
            cancellationToken);
    }
}
