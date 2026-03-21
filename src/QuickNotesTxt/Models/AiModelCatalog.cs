namespace QuickNotesTxt.Models;

/// <summary>
/// Single source for OpenAI chat/completion model identifiers shown in the UI and passed to the API.
/// </summary>
public static class AiModelCatalog
{
    /// <summary>
    /// Models offered in chat and as defaults; order is display order.
    /// </summary>
    public static IReadOnlyList<string> ChatCompletionModels { get; } =
    [
        "gpt-5.4",
        "gpt-5.4-mini",
        "gpt-5.4-nano"
    ];

    public static string DefaultChatModel => "gpt-5.4-mini";
}
