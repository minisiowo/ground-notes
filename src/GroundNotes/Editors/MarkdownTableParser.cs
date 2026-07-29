namespace GroundNotes.Editors;

internal static class MarkdownTableParser
{
    public static bool TryFindTableAtOffset(string text, int offset, out MarkdownTable table)
    {
        var clampedOffset = Math.Clamp(offset, 0, text.Length);
        foreach (var candidate in FindTables(text))
        {
            if (clampedOffset >= candidate.Start && clampedOffset <= candidate.Start + candidate.Length)
            {
                table = candidate;
                return true;
            }
        }

        table = null!;
        return false;
    }

    public static IReadOnlyList<MarkdownTable> FindTables(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var lines = ReadLines(text);
        List<MarkdownTable> tables = [];
        var fenceState = MarkdownFenceState.None;

        for (var i = 0; i < lines.Count - 1; i++)
        {
            var line = lines[i];
            var stateBeforeLine = fenceState;
            fenceState = MarkdownLineParser.AdvanceFenceState(fenceState, line.Text);
            if (stateBeforeLine.IsInsideFence || MarkdownLineParser.TryMatchFence(line.Text) is not null)
            {
                continue;
            }

            if (!TryParseRow(line, out var headerCells)
                || !TryParseDelimiterRow(lines[i + 1], out var alignments)
                || headerCells.Count != alignments.Count
                || headerCells.Count == 0)
            {
                continue;
            }

            var rows = new List<MarkdownTableRow>
            {
                CreateRow(0, line, isDelimiter: false, headerCells),
                CreateRow(1, lines[i + 1], isDelimiter: true, ParseCells(lines[i + 1]))
            };

            var endIndex = i + 1;
            for (var bodyIndex = i + 2; bodyIndex < lines.Count; bodyIndex++)
            {
                var bodyLine = lines[bodyIndex];
                if (string.IsNullOrWhiteSpace(bodyLine.Text)
                    || MarkdownLineParser.TryMatchFence(bodyLine.Text) is not null
                    || !TryParseRow(bodyLine, out var bodyCells))
                {
                    break;
                }

                if (bodyCells.Count > alignments.Count)
                {
                    break;
                }

                while (bodyCells.Count < alignments.Count)
                {
                    bodyCells.Add(CreateEmptyCell(bodyLine));
                }

                rows.Add(CreateRow(rows.Count, bodyLine, isDelimiter: false, bodyCells));
                endIndex = bodyIndex;
            }

            var start = line.Start;
            var end = lines[endIndex].Start + lines[endIndex].Text.Length;
            tables.Add(new MarkdownTable(
                start,
                end - start,
                i + 1,
                endIndex + 1,
                line.NewLine.Length > 0 ? line.NewLine : "\n",
                alignments,
                rows));

            for (var skipped = i + 1; skipped <= endIndex; skipped++)
            {
                fenceState = MarkdownLineParser.AdvanceFenceState(fenceState, lines[skipped].Text);
            }

            i = endIndex;
        }

        return tables;
    }

    private static bool TryParseRow(SourceLine line, out List<MarkdownTableCell> cells)
    {
        cells = ParseCells(line);
        return FindFirstNonWhitespace(line.Text) == 0 && line.Text.Contains('|') && cells.Count > 0;
    }

    private static bool TryParseDelimiterRow(SourceLine line, out IReadOnlyList<MarkdownTableAlignment> alignments)
    {
        alignments = [];
        if (FindFirstNonWhitespace(line.Text) != 0 || !line.Text.Contains('|'))
        {
            return false;
        }

        var cells = ParseCells(line);
        if (cells.Count == 0)
        {
            return false;
        }

        List<MarkdownTableAlignment> parsed = [];
        foreach (var cell in cells)
        {
            var value = cell.Content;
            var left = value.StartsWith(':');
            var right = value.EndsWith(':');
            var dashStart = left ? 1 : 0;
            var dashEnd = right ? value.Length - 1 : value.Length;
            if (dashEnd - dashStart < 3)
            {
                return false;
            }

            for (var i = dashStart; i < dashEnd; i++)
            {
                if (value[i] != '-')
                {
                    return false;
                }
            }

            parsed.Add((left, right) switch
            {
                (true, true) => MarkdownTableAlignment.Center,
                (true, false) => MarkdownTableAlignment.Left,
                (false, true) => MarkdownTableAlignment.Right,
                _ => MarkdownTableAlignment.None
            });
        }

        alignments = parsed;
        return true;
    }

    private static List<MarkdownTableCell> ParseCells(SourceLine line)
    {
        var text = line.Text;
        var separators = new List<int>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '|' || IsEscaped(text, i))
            {
                continue;
            }

            separators.Add(i);
        }

        if (separators.Count == 0)
        {
            return [];
        }

        var startsWithPipe = separators[0] == FindFirstNonWhitespace(text);
        var lastNonWhitespace = FindLastNonWhitespace(text);
        var endsWithPipe = separators[^1] == lastNonWhitespace;
        var segmentStart = startsWithPipe ? separators[0] + 1 : 0;
        var segmentEndLimit = endsWithPipe ? separators[^1] : text.Length;
        var innerSeparators = separators.Where((_, index) => !(startsWithPipe && index == 0) && !(endsWithPipe && index == separators.Count - 1));

        List<MarkdownTableCell> cells = [];
        foreach (var segmentEnd in innerSeparators.Append(segmentEndLimit))
        {
            var contentStart = segmentStart;
            while (contentStart < segmentEnd && char.IsWhiteSpace(text[contentStart]))
            {
                contentStart++;
            }

            var contentEnd = segmentEnd;
            while (contentEnd > contentStart && char.IsWhiteSpace(text[contentEnd - 1]))
            {
                contentEnd--;
            }

            cells.Add(new MarkdownTableCell(
                text[contentStart..contentEnd],
                line.Start + contentStart,
                contentEnd - contentStart,
                line.Start + segmentStart,
                segmentEnd - segmentStart));
            segmentStart = segmentEnd + 1;
        }

        return cells;
    }

    private static MarkdownTableCell CreateEmptyCell(SourceLine line)
    {
        var offset = line.Start + line.Text.Length;
        return new MarkdownTableCell(string.Empty, offset, 0, offset, 0);
    }

    private static MarkdownTableRow CreateRow(int index, SourceLine line, bool isDelimiter, IReadOnlyList<MarkdownTableCell> cells)
        => new(index, line.Start, line.Text.Length, isDelimiter, cells);

    private static bool IsEscaped(string text, int index)
    {
        var backslashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 != 0;
    }

    private static int FindFirstNonWhitespace(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindLastNonWhitespace(string text)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<SourceLine> ReadLines(string text)
    {
        List<SourceLine> lines = [];
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\r' && text[i] != '\n')
            {
                continue;
            }

            var endingLength = text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
            lines.Add(new SourceLine(start, text[start..i], text.Substring(i, endingLength)));
            i += endingLength - 1;
            start = i + 1;
        }

        lines.Add(new SourceLine(start, text[start..], string.Empty));
        return lines;
    }

    private sealed record SourceLine(int Start, string Text, string NewLine);
}
