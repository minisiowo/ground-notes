namespace GroundNotes.Services;

internal static class TagHierarchyHelper
{
    public static string Normalize(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);

        var segments = tag
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return string.Join('/', segments);
    }

    public static List<string> ParseCommaSeparated(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(TryNormalize)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> ParseYamlList(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            return [];
        }

        trimmed = trimmed[1..^1];
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        return trimmed
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim().Trim('"'))
            .Select(TryNormalize)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ExpandWithAncestors(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var expanded = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            var normalized = TryNormalize(tag);
            if (normalized is null)
            {
                continue;
            }

            var segments = normalized.Split('/');
            for (var length = 1; length <= segments.Length; length++)
            {
                var candidate = string.Join('/', segments.Take(length));
                if (seen.Add(candidate))
                {
                    expanded.Add(candidate);
                }
            }
        }

        return expanded;
    }

    public static bool MatchesSelection(IEnumerable<string> noteTags, string selectedTag)
    {
        ArgumentNullException.ThrowIfNull(noteTags);

        var normalizedSelectedTag = TryNormalize(selectedTag);
        if (normalizedSelectedTag is null)
        {
            return false;
        }

        return ExpandWithAncestors(noteTags).Contains(normalizedSelectedTag, StringComparer.OrdinalIgnoreCase);
    }

    public static string? GetParentTag(string tag)
    {
        var normalized = TryNormalize(tag);
        if (normalized is null)
        {
            return null;
        }

        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex < 0 ? null : normalized[..separatorIndex];
    }

    public static string GetLeafName(string tag)
    {
        var normalized = Normalize(tag);
        var separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex < 0 ? normalized : normalized[(separatorIndex + 1)..];
    }

    private static string? TryNormalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var normalized = Normalize(tag);
        return normalized.Length == 0 ? null : normalized;
    }
}
