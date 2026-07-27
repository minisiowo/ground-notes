namespace GroundNotes.Models;

public sealed record AiTitleGenerationSettings(
    bool IsEnabled,
    string DefaultModel,
    string DefaultReasoningEffort = "")
{
    public static AiTitleGenerationSettings Default { get; } = new(
        true,
        AiModelCatalog.DefaultTitleGenerationModel,
        AiReasoningEffortCatalog.DefaultReasoningEffort);

    public static AiTitleGenerationSettings Normalize(
        string? defaultModel,
        bool isEnabled,
        string? defaultReasoningEffort = "")
    {
        return new AiTitleGenerationSettings(
            isEnabled,
            string.IsNullOrWhiteSpace(defaultModel)
                ? Default.DefaultModel
                : defaultModel.Trim(),
            AiReasoningEffortCatalog.Normalize(defaultReasoningEffort));
    }

    public static AiTitleGenerationSettings Normalize(AiTitleGenerationSettings? settings)
    {
        return settings is null
            ? Default
            : Normalize(settings.DefaultModel, settings.IsEnabled, settings.DefaultReasoningEffort);
    }
}
