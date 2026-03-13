namespace QuickNotesTxt.Models;

public sealed record AiPromptDefinition(
    string Id,
    string Name,
    string PromptTemplate,
    string? Description = null,
    string? Model = null,
    bool ReplaceSelection = true,
    int Order = 0,
    bool IsBuiltIn = false);
