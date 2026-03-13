namespace QuickNotesTxt.Models;

public sealed record AiSettings(
    string ApiKey,
    string DefaultModel,
    bool IsEnabled,
    string ProjectId = "",
    string OrganizationId = "")
{
    public static AiSettings Default { get; } = new(string.Empty, "gpt-4.1-mini", true);
}
