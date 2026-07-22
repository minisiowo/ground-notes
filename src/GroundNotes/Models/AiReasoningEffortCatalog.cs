namespace GroundNotes.Models;

public static class AiReasoningEffortCatalog
{
    public static IReadOnlyList<string> ReasoningEfforts { get; } =
    [
        "none",
        "low",
        "medium",
        "high",
        "xhigh",
        "max"
    ];

    public static string DefaultReasoningEffort => "none";

    public static string Normalize(string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return DefaultReasoningEffort;
        }

        var trimmed = reasoningEffort.Trim();
        return ReasoningEfforts.Contains(trimmed, StringComparer.OrdinalIgnoreCase)
            ? trimmed.ToLowerInvariant()
            : DefaultReasoningEffort;
    }
}
