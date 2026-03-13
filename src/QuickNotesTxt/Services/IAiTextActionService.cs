using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface IAiTextActionService
{
    Task<string> RunPromptAsync(AiPromptDefinition prompt, string selectedText, AiSettings settings, CancellationToken cancellationToken = default);
}
