using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IAiTextActionService
{
    Task<string> RunPromptAsync(AiPromptDefinition prompt, string selectedText, AiSettings settings, CancellationToken cancellationToken = default);
}
