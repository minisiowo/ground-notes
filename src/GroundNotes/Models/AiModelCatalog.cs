namespace GroundNotes.Models;

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
        "gpt-5.6-sol",
        "gpt-5.6-terra",
        "gpt-5.6-luna"
    ];

    public static IReadOnlyList<string> TitleGenerationModels { get; } =
    [
        "gpt-5-mini",
        "gpt-5.6-sol",
        "gpt-5.6-terra",
        "gpt-5.6-luna"
    ];

    public static string DefaultChatModel => "gpt-5.6-terra";

    public static string DefaultTitleGenerationModel => "gpt-5-mini";
}
