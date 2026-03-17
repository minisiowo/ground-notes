using System.Text.RegularExpressions;

namespace QuickNotesTxt.Editors;

internal static partial class MarkdownLineParser
{
    public static MarkdownLineAnalysis Analyze(string lineText, MarkdownFenceState fenceStateBeforeLine)
    {
        MarkdownDiagnostics.RecordLineAnalyzed();

        var analysis = new MarkdownLineAnalysis(fenceStateBeforeLine);

        if (string.IsNullOrEmpty(lineText))
        {
            return analysis;
        }

        analysis.Fence = TryMatchFence(lineText);
        if (analysis.IsFencedCodeLine)
        {
            return analysis;
        }

        List<MarkdownRange>? protectedSpans = null;

        if (lineText.Contains('`'))
        {
            analysis.InlineCodeSpans.AddRange(FindInlineCodeSpans(lineText));
            if (analysis.InlineCodeSpans.Count > 0)
            {
                protectedSpans = GetFullSpans(analysis.InlineCodeSpans);
            }
        }

        analysis.Heading = TryMatchHeading(lineText);
        analysis.HorizontalRule = TryMatchHorizontalRule(lineText);
        analysis.Blockquote = TryMatchBlockquote(lineText);
        analysis.TaskList = TryMatchTaskList(lineText);
        analysis.ListMarker = analysis.TaskList is null ? TryMatchListMarker(lineText) : null;

        if (lineText.IndexOf('[', StringComparison.Ordinal) >= 0)
        {
            foreach (var link in FindLinks(lineText, protectedSpans))
            {
                analysis.Links.Add(link);
                analysis.LinkSpans.Add(link.FullSpan);
            }
        }

        if (lineText.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var urlSpan in FindBareUrls(lineText, protectedSpans, analysis.LinkSpans))
            {
                analysis.BareUrls.Add(urlSpan);
            }
        }

        if (lineText.Contains("~~", StringComparison.Ordinal))
        {
            analysis.StrikethroughSpans.AddRange(FindDelimitedSpans(lineText, "~~", "~~", protectedSpans));
        }

        if (lineText.Contains("**", StringComparison.Ordinal) || lineText.Contains("__", StringComparison.Ordinal))
        {
            analysis.BoldSpans.AddRange(FindStrongSpans(lineText, protectedSpans));
        }

        if (lineText.IndexOf('*') >= 0 || lineText.IndexOf('_') >= 0)
        {
            analysis.ItalicSpans.AddRange(FindItalicSpans(lineText, protectedSpans));
        }

        return analysis;
    }

    public static MarkdownFenceMatch? TryMatchFence(string lineText)
    {
        if (string.IsNullOrEmpty(lineText) || (!lineText.Contains('`') && !lineText.Contains('~')))
        {
            return null;
        }

        var match = FenceRegex().Match(lineText);
        if (!match.Success)
        {
            return null;
        }

        MarkdownRange? info = match.Groups["info"].Success
            ? new MarkdownRange(match.Groups["info"].Index, match.Groups["info"].Length)
            : null;

        return new MarkdownFenceMatch(
            match.Groups["fence"].ValueSpan[0],
            match.Groups["fence"].Length,
            new(match.Groups["fence"].Index, match.Groups["fence"].Length),
            info);
    }

    public static MarkdownFenceState AdvanceFenceState(MarkdownFenceState state, string lineText)
    {
        var fence = TryMatchFence(lineText);
        if (fence is null)
        {
            return state;
        }

        if (!state.IsInsideFence)
        {
            return new MarkdownFenceState(true, fence.Value.MarkerChar, fence.Value.MarkerLength);
        }

        if (state.MarkerChar == fence.Value.MarkerChar && fence.Value.MarkerLength >= state.MarkerLength)
        {
            return MarkdownFenceState.None;
        }

        return state;
    }

    private static MarkdownHeadingMatch? TryMatchHeading(string lineText)
    {
        if (string.IsNullOrEmpty(lineText) || lineText.IndexOf('#') < 0)
        {
            return null;
        }

        var match = HeadingRegex().Match(lineText);
        if (!match.Success)
        {
            return null;
        }

        MarkdownRange? closing = match.Groups["closing"].Success
            ? new MarkdownRange(match.Groups["closing"].Index, match.Groups["closing"].Length)
            : null;

        return new MarkdownHeadingMatch(
            match.Groups["marker"].Length,
            new(match.Groups["marker"].Index, match.Groups["marker"].Length),
            new(match.Groups["text"].Index, match.Groups["text"].Length),
            closing);
    }

    private static MarkdownRange? TryMatchHorizontalRule(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
        {
            return null;
        }

        var firstNonWhitespace = FindFirstNonWhitespace(lineText);
        if (firstNonWhitespace < 0)
        {
            return null;
        }

        var marker = lineText[firstNonWhitespace];
        if (marker is not ('-' or '*' or '_'))
        {
            return null;
        }

        var match = HorizontalRuleRegex().Match(lineText);
        return match.Success
            ? new MarkdownRange(match.Groups["rule"].Index, match.Groups["rule"].Length)
            : null;
    }

    private static MarkdownBlockquoteMatch? TryMatchBlockquote(string lineText)
    {
        if (string.IsNullOrEmpty(lineText) || !lineText.Contains('>'))
        {
            return null;
        }

        var match = BlockquoteRegex().Match(lineText);
        if (!match.Success)
        {
            return null;
        }

        return new MarkdownBlockquoteMatch(
            match.Groups["marker"].Length,
            new(match.Groups["marker"].Index, match.Groups["marker"].Length),
            new(match.Groups["text"].Index, match.Groups["text"].Length));
    }

    private static MarkdownListMatch? TryMatchTaskList(string lineText)
    {
        if (string.IsNullOrEmpty(lineText) || lineText.IndexOf('[') < 0)
        {
            return null;
        }

        var match = TaskListRegex().Match(lineText);
        if (!match.Success)
        {
            return null;
        }

        return new MarkdownListMatch(
            match.Groups["indent"].Length,
            new(match.Groups["marker"].Index, match.Groups["marker"].Length),
            new(match.Groups["checkbox"].Index, match.Groups["checkbox"].Length),
            string.Equals(match.Groups["state"].Value, "x", StringComparison.OrdinalIgnoreCase),
            new(match.Groups["text"].Index, match.Groups["text"].Length));
    }

    private static MarkdownListMatch? TryMatchListMarker(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
        {
            return null;
        }

        var firstNonWhitespace = FindFirstNonWhitespace(lineText);
        if (firstNonWhitespace < 0)
        {
            return null;
        }

        var marker = lineText[firstNonWhitespace];
        if (marker is not ('-' or '+' or '*' or >= '0' and <= '9'))
        {
            return null;
        }

        var match = ListMarkerRegex().Match(lineText);
        if (!match.Success)
        {
            return null;
        }

        return new MarkdownListMatch(match.Groups["indent"].Length, new(match.Groups["marker"].Index, match.Groups["marker"].Length), null, false, null);
    }

    private static List<MarkdownDelimitedSpan> FindInlineCodeSpans(string lineText)
    {
        List<MarkdownDelimitedSpan> spans = [];

        for (var i = 0; i < lineText.Length; i++)
        {
            if (lineText[i] != '`')
            {
                continue;
            }

            var end = lineText.IndexOf('`', i + 1);
            if (end <= i + 1)
            {
                continue;
            }

            spans.Add(new MarkdownDelimitedSpan(new(i, 1), new(i + 1, end - i - 1), new(end, 1)));
            i = end;
        }

        return spans;
    }

    private static List<MarkdownLinkMatch> FindLinks(string lineText, IReadOnlyList<MarkdownRange>? protectedSpans)
    {
        List<MarkdownLinkMatch> links = [];

        for (var i = 0; i < lineText.Length; i++)
        {
            if (lineText[i] != '[')
            {
                continue;
            }

            var closeBracket = lineText.IndexOf(']', i + 1);
            if (closeBracket <= i + 1 || closeBracket + 1 >= lineText.Length || lineText[closeBracket + 1] != '(')
            {
                continue;
            }

            var closeParen = lineText.IndexOf(')', closeBracket + 2);
            if (closeParen <= closeBracket + 2)
            {
                continue;
            }

            var fullSpan = new MarkdownRange(i, closeParen - i + 1);
            if (protectedSpans is not null && OverlapsAny(fullSpan, protectedSpans))
            {
                continue;
            }

            var urlStart = closeBracket + 2;
            var urlLength = closeParen - urlStart;
            if (urlLength == 0 || ContainsWhitespace(lineText, urlStart, closeParen))
            {
                continue;
            }

            links.Add(new MarkdownLinkMatch(
                new(i, 1),
                new(i + 1, closeBracket - i - 1),
                new(closeBracket, 1),
                new(closeBracket + 1, 1),
                new(urlStart, urlLength),
                new(closeParen, 1)));
            i = closeParen;
        }

        return links;
    }

    private static List<MarkdownRange> FindBareUrls(string lineText, IReadOnlyList<MarkdownRange>? protectedSpans, IReadOnlyList<MarkdownRange> linkSpans)
    {
        List<MarkdownRange> urls = [];

        for (var i = 0; i < lineText.Length; i++)
        {
            if (!StartsWithUrlPrefix(lineText, i))
            {
                continue;
            }

            var end = i;
            while (end < lineText.Length && !char.IsWhiteSpace(lineText[end]) && lineText[end] is not ')' and not ']')
            {
                end++;
            }

            var span = new MarkdownRange(i, end - i);
            if ((protectedSpans is not null && OverlapsAny(span, protectedSpans)) || OverlapsAny(span, linkSpans))
            {
                i = end;
                continue;
            }

            urls.Add(span);
            i = end;
        }

        return urls;
    }

    private static List<MarkdownDelimitedSpan> FindStrongSpans(string lineText, IReadOnlyList<MarkdownRange>? protectedSpans)
    {
        List<MarkdownDelimitedSpan> spans = [];
        spans.AddRange(FindDelimitedSpans(lineText, "**", "**", protectedSpans));
        spans.AddRange(FindDelimitedSpans(lineText, "__", "__", protectedSpans));
        spans.Sort(static (left, right) => left.MarkerStart.Start.CompareTo(right.MarkerStart.Start));
        return spans;
    }

    private static List<MarkdownDelimitedSpan> FindItalicSpans(string lineText, IReadOnlyList<MarkdownRange>? protectedSpans)
    {
        List<MarkdownDelimitedSpan> spans = [];

        for (var i = 0; i < lineText.Length; i++)
        {
            var marker = lineText[i];
            if (marker is not '*' and not '_')
            {
                continue;
            }

            if ((i > 0 && lineText[i - 1] == marker) || (i + 1 < lineText.Length && lineText[i + 1] == marker))
            {
                continue;
            }

            var end = lineText.IndexOf(marker, i + 1);
            if (end <= i + 1)
            {
                continue;
            }

            if ((end + 1 < lineText.Length && lineText[end + 1] == marker) || ContainsEither(lineText, i + 1, end, '*', '_'))
            {
                continue;
            }

            var span = new MarkdownDelimitedSpan(new(i, 1), new(i + 1, end - i - 1), new(end, 1));
            if (protectedSpans is not null && OverlapsAny(span.FullSpan, protectedSpans))
            {
                continue;
            }

            spans.Add(span);
            i = end;
        }

        return spans;
    }

    private static List<MarkdownDelimitedSpan> FindDelimitedSpans(string lineText, string markerStart, string markerEnd, IReadOnlyList<MarkdownRange>? protectedSpans)
    {
        List<MarkdownDelimitedSpan> spans = [];
        var markerLength = markerStart.Length;

        for (var i = 0; i <= lineText.Length - markerLength; i++)
        {
            if (!lineText.AsSpan(i).StartsWith(markerStart, StringComparison.Ordinal))
            {
                continue;
            }

            var contentStart = i + markerLength;
            var end = lineText.IndexOf(markerEnd, contentStart, StringComparison.Ordinal);
            if (end <= contentStart)
            {
                continue;
            }

            var span = new MarkdownDelimitedSpan(new(i, markerLength), new(contentStart, end - contentStart), new(end, markerEnd.Length));
            if (protectedSpans is not null && OverlapsAny(span.FullSpan, protectedSpans))
            {
                continue;
            }

            spans.Add(span);
            i = end + markerEnd.Length - 1;
        }

        return spans;
    }

    private static List<MarkdownRange> GetFullSpans(IEnumerable<MarkdownDelimitedSpan> spans)
    {
        List<MarkdownRange> fullSpans = [];
        foreach (var span in spans)
        {
            fullSpans.Add(span.FullSpan);
        }

        return fullSpans;
    }

    private static bool OverlapsAny(MarkdownRange span, IReadOnlyList<MarkdownRange> others)
    {
        foreach (var other in others)
        {
            if (span.Start < other.End && span.End > other.Start)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindFirstNonWhitespace(string lineText)
    {
        for (var i = 0; i < lineText.Length; i++)
        {
            if (!char.IsWhiteSpace(lineText[i]))
            {
                return i;
            }
        }

        return -1;
    }

    [GeneratedRegex("^(?<indent>\\s*)(?<fence>(?:`{3,}|~{3,}))(?<spacing>\\s*)(?<info>[^`~\\s][^`~]*)?\\s*$", RegexOptions.Compiled)]
    private static partial Regex FenceRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<rule>(?:-{3,}|\\*{3,}|_{3,}))\\s*$", RegexOptions.Compiled)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>#{1,6})(?<spacing>\\s+)(?<text>.*?)(?<closing>\\s+#+\\s*)?$", RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>>+)(?<spacing>\\s*)(?<text>.+)$", RegexOptions.Compiled)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>(?:[-+*]|\\d+[.)]))(?<spacing>\\s+)(?<checkbox>\\[(?<state> |x|X)\\])(?=\\s)(?<textSpacing>\\s+)(?<text>.*)$", RegexOptions.Compiled)]
    private static partial Regex TaskListRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>(?:[-+*]|\\d+[.)]))(?=\\s)", RegexOptions.Compiled)]
    private static partial Regex ListMarkerRegex();

    private static bool ContainsWhitespace(string value, int start, int endExclusive)
    {
        for (var i = start; i < endExclusive; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsEither(string value, int start, int endExclusive, char first, char second)
    {
        for (var i = start; i < endExclusive; i++)
        {
            if (value[i] == first || value[i] == second)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithUrlPrefix(string value, int index)
    {
        return value.AsSpan(index).StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.AsSpan(index).StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class MarkdownLineAnalysis(MarkdownFenceState fenceStateBeforeLine)
{
    public MarkdownFenceState FenceStateBeforeLine { get; } = fenceStateBeforeLine;

    public MarkdownFenceMatch? Fence { get; set; }

    public bool IsFencedCodeLine => FenceStateBeforeLine.IsInsideFence || Fence is not null;

    public MarkdownHeadingMatch? Heading { get; set; }

    public MarkdownRange? HorizontalRule { get; set; }

    public MarkdownBlockquoteMatch? Blockquote { get; set; }

    public MarkdownListMatch? TaskList { get; set; }

    public MarkdownListMatch? ListMarker { get; set; }

    public List<MarkdownLinkMatch> Links { get; } = [];

    public List<MarkdownRange> LinkSpans { get; } = [];

    public List<MarkdownRange> BareUrls { get; } = [];

    public List<MarkdownDelimitedSpan> InlineCodeSpans { get; } = [];

    public List<MarkdownDelimitedSpan> StrikethroughSpans { get; } = [];

    public List<MarkdownDelimitedSpan> BoldSpans { get; } = [];

    public List<MarkdownDelimitedSpan> ItalicSpans { get; } = [];
}

internal readonly record struct MarkdownRange(int Start, int Length)
{
    public int End => Start + Length;
}

internal readonly record struct MarkdownDelimitedSpan(MarkdownRange MarkerStart, MarkdownRange Text, MarkdownRange MarkerEnd)
{
    public MarkdownRange FullSpan => new(MarkerStart.Start, MarkerEnd.End - MarkerStart.Start);
}

internal readonly record struct MarkdownHeadingMatch(int Level, MarkdownRange Marker, MarkdownRange Text, MarkdownRange? Closing);

internal readonly record struct MarkdownBlockquoteMatch(int Depth, MarkdownRange Marker, MarkdownRange Text);

internal readonly record struct MarkdownListMatch(int IndentLength, MarkdownRange Marker, MarkdownRange? Checkbox, bool IsChecked, MarkdownRange? Text);

internal readonly record struct MarkdownLinkMatch(MarkdownRange OpenBracket, MarkdownRange Label, MarkdownRange CloseBracket, MarkdownRange OpenParen, MarkdownRange Url, MarkdownRange CloseParen)
{
    public MarkdownRange FullSpan => new(OpenBracket.Start, CloseParen.End - OpenBracket.Start);
}

internal readonly record struct MarkdownFenceMatch(char MarkerChar, int MarkerLength, MarkdownRange Fence, MarkdownRange? Info);

internal readonly record struct MarkdownFenceState(bool IsInsideFence, char MarkerChar, int MarkerLength)
{
    public static MarkdownFenceState None { get; } = new(false, '\0', 0);
}
