namespace GroundNotes.Models;

public sealed record AiTitleGenerationSettings(
    bool IsEnabled,
    string DefaultModel,
    string DefaultReasoningEffort = "",
    string TitleStylePrompt = "")
{
    public const string DefaultTitleStylePrompt = "Use concise, descriptive titles suitable for note filenames.";

    public static AiTitleGenerationSettings Default { get; } = new(
        true,
        AiModelCatalog.DefaultTitleGenerationModel,
        AiReasoningEffortCatalog.DefaultReasoningEffort,
        DefaultTitleStylePrompt);

    public static AiTitleGenerationSettings Normalize(
        string? defaultModel,
        bool isEnabled,
        string? defaultReasoningEffort = "",
        string? titleStylePrompt = null)
    {
        return new AiTitleGenerationSettings(
            isEnabled,
            string.IsNullOrWhiteSpace(defaultModel)
                ? Default.DefaultModel
                : defaultModel.Trim(),
            AiReasoningEffortCatalog.Normalize(defaultReasoningEffort),
            NormalizeTitleStylePrompt(titleStylePrompt));
    }

    public static AiTitleGenerationSettings Normalize(AiTitleGenerationSettings? settings)
    {
        return settings is null
            ? Default
            : Normalize(settings.DefaultModel, settings.IsEnabled, settings.DefaultReasoningEffort, settings.TitleStylePrompt);
    }

    public static string NormalizeTitleStylePrompt(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DefaultTitleStylePrompt : value.Trim();
    }
}
