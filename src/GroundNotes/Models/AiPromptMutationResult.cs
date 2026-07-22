namespace GroundNotes.Models;

public sealed record AiPromptMutationResult(
    IReadOnlyList<AiPromptDefinition> Prompts,
    bool Succeeded);
