namespace QuickNotesTxt.Services;

public sealed record OpenAiCompletionOptions(
    double? Temperature = null,
    int? MaxTokens = null,
    string? ReasoningEffort = null);
