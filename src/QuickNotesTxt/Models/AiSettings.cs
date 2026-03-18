namespace QuickNotesTxt.Models;

public sealed record AiSettings(
    string ApiKey,
    string DefaultModel,
    bool IsEnabled,
    string ProjectId = "",
    string OrganizationId = "")
{
    public static AiSettings Default { get; } = new(string.Empty, "gpt-5.4-mini", true);
}
