namespace GroundNotes.Models;

public sealed record AiSettings(
    string ApiKey,
    string DefaultModel,
    bool IsEnabled,
    string ProjectId = "",
    string OrganizationId = "",
    string DefaultReasoningEffort = "")
{
    public static AiSettings Default { get; } = new(
        string.Empty,
        AiModelCatalog.DefaultChatModel,
        true,
        DefaultReasoningEffort: AiReasoningEffortCatalog.DefaultReasoningEffort);

    public static AiSettings Normalize(
        string? apiKey,
        string? defaultModel,
        bool isEnabled,
        string? projectId = "",
        string? organizationId = "",
        string? defaultReasoningEffort = "")
    {
        return new AiSettings(
            apiKey?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(defaultModel) ? Default.DefaultModel : defaultModel.Trim(),
            isEnabled,
            projectId?.Trim() ?? string.Empty,
            organizationId?.Trim() ?? string.Empty,
            AiReasoningEffortCatalog.Normalize(defaultReasoningEffort));
    }

    public static AiSettings Normalize(AiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Normalize(
            settings.ApiKey,
            settings.DefaultModel,
            settings.IsEnabled,
            settings.ProjectId,
            settings.OrganizationId,
            settings.DefaultReasoningEffort);
    }
}
