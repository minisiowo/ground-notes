namespace GroundNotes.Models;

public sealed record SettingsPromptActions(
    Func<AiPromptDefinition, Task<AiPromptMutationResult>> SavePromptAsync,
    Func<AiPromptDefinition, Task<AiPromptMutationResult>> DeletePromptAsync,
    Func<Task<IReadOnlyList<AiPromptDefinition>>> ReloadPromptsAsync);
