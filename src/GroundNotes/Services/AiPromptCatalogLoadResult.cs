using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed record AiPromptCatalogLoadResult(
    IReadOnlyList<AiPromptDefinition> Prompts,
    IReadOnlyList<string> Warnings)
{
    public static AiPromptCatalogLoadResult Empty { get; } = new([], []);

    public bool HasWarnings => Warnings.Count > 0;
}
