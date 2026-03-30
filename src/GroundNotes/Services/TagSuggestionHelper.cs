namespace GroundNotes.Services;

internal static class TagSuggestionHelper
{
    public static IReadOnlyList<string> GetSuggestions(string input, int caretIndex, IEnumerable<string> availableTags)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(availableTags);

        var context = GetContext(input, caretIndex);
        if (string.IsNullOrWhiteSpace(context.Query))
        {
            return [];
        }

        return availableTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(tag => !context.OtherTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            .Where(tag => !string.Equals(tag, context.Query, StringComparison.OrdinalIgnoreCase))
            .Select(tag => new TagSuggestion(tag, Score(tag, context.Query)))
            .Where(item => item.Score >= 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Tag)
            .ToList();
    }

    public static TagSuggestionApplyResult ApplySuggestion(string input, int caretIndex, string suggestion)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestion);

        var context = GetContext(input, caretIndex);
        var updatedSegments = context.Segments
            .Select(segment => new TagSegmentState(segment.Index, segment.Tag, segment.Index == context.SegmentIndex))
            .ToList();

        var targetSegment = updatedSegments.First(segment => segment.IsTarget);
        var replacement = targetSegment with { Tag = suggestion.Trim() };
        updatedSegments[targetSegment.Index] = replacement;

        var keptSegments = updatedSegments
            .Where(segment => !string.IsNullOrWhiteSpace(segment.Tag))
            .ToList();

        var text = string.Join(", ", keptSegments.Select(segment => segment.Tag));
        var caret = 0;
        for (var i = 0; i < keptSegments.Count; i++)
        {
            var segment = keptSegments[i];
            caret += segment.Tag.Length;
            if (segment.IsTarget)
            {
                return new TagSuggestionApplyResult(text, caret);
            }

            caret += 2;
        }

        return new TagSuggestionApplyResult(text, text.Length);
    }

    private static TagSuggestionContext GetContext(string input, int caretIndex)
    {
        var safeCaretIndex = Math.Clamp(caretIndex, 0, input.Length);
        var rawSegments = input.Split(',');
        var segmentIndex = input[..safeCaretIndex].Count(c => c == ',');
        if (segmentIndex >= rawSegments.Length)
        {
            segmentIndex = rawSegments.Length - 1;
        }

        var segments = rawSegments
            .Select((segment, index) => new TagSegment(index, segment.Trim()))
            .ToList();

        if (segments.Count == 0)
        {
            segments.Add(new TagSegment(0, string.Empty));
            segmentIndex = 0;
        }

        var query = segments[segmentIndex].Tag;
        var otherTags = segments
            .Where(segment => segment.Index != segmentIndex && !string.IsNullOrWhiteSpace(segment.Tag))
            .Select(segment => segment.Tag)
            .ToList();

        return new TagSuggestionContext(segments, segmentIndex, query, otherTags);
    }

    private static int Score(string candidate, string query)
    {
        if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2_000 - candidate.Length;
        }

        var containsIndex = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (containsIndex >= 0)
        {
            return 1_000 - containsIndex;
        }

        return -1;
    }

    private readonly record struct TagSegment(int Index, string Tag);

    private readonly record struct TagSegmentState(int Index, string Tag, bool IsTarget);

    private readonly record struct TagSuggestion(string Tag, int Score);

    private readonly record struct TagSuggestionContext(
        IReadOnlyList<TagSegment> Segments,
        int SegmentIndex,
        string Query,
        IReadOnlyList<string> OtherTags);
}

internal readonly record struct TagSuggestionApplyResult(string Text, int CaretIndex);
