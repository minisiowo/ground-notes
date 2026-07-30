using System.Text;

namespace GroundNotes.Editors;

internal static class MarkdownTableFormatter
{
    public static bool TryFormatPastedText(string text, out string formattedText)
    {
        var result = FormatAllWithMetadata(text);
        formattedText = result.Text;
        return result.ContainsTables;
    }

    public static string FormatAll(string text) => FormatAllWithMetadata(text).Text;

    public static MarkdownTableFormatResult FormatAllWithMetadata(string text)
    {
        var tables = MarkdownTableParser.FindTables(text);
        if (tables.Count == 0)
        {
            return new MarkdownTableFormatResult(text, tables, text.Length);
        }

        var builder = new StringBuilder(text.Length);
        var sourceOffset = 0;
        foreach (var table in tables)
        {
            builder.Append(text, sourceOffset, table.Start - sourceOffset);
            builder.Append(Format(table));
            sourceOffset = table.Start + table.Length;
        }

        builder.Append(text, sourceOffset, text.Length - sourceOffset);
        return new MarkdownTableFormatResult(builder.ToString(), tables, text.Length);
    }

    public static string Format(MarkdownTable table)
    {
        var contentRows = table.Rows
            .Where(static row => !row.IsDelimiter)
            .Select(row => NormalizeRow(row.Cells.Select(static cell => cell.Content), table.ColumnCount))
            .ToList();
        return Format(contentRows, table.Alignments, table.NewLine);
    }

    public static string Format(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<MarkdownTableAlignment> alignments,
        string newLine)
    {
        if (alignments.Count == 0 || rows.Count == 0)
        {
            return string.Empty;
        }

        var normalizedRows = rows.Select(row => NormalizeRow(row, alignments.Count)).ToList();
        var widths = new int[alignments.Count];
        for (var column = 0; column < widths.Length; column++)
        {
            widths[column] = Math.Max(3, normalizedRows.Max(row => GetDisplayWidth(row[column])));
        }

        var builder = new StringBuilder();
        AppendContentRow(builder, normalizedRows[0], widths);
        builder.Append(newLine);
        AppendDelimiterRow(builder, widths, alignments);

        for (var row = 1; row < normalizedRows.Count; row++)
        {
            builder.Append(newLine);
            AppendContentRow(builder, normalizedRows[row], widths);
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> NormalizeRow(IEnumerable<string> cells, int columnCount)
    {
        var normalized = cells.Take(columnCount).ToList();
        while (normalized.Count < columnCount)
        {
            normalized.Add(string.Empty);
        }

        return normalized;
    }

    private static void AppendContentRow(StringBuilder builder, IReadOnlyList<string> cells, IReadOnlyList<int> widths)
    {
        builder.Append('|');
        for (var column = 0; column < widths.Count; column++)
        {
            var content = cells[column];
            builder.Append(' ');
            builder.Append(content);
            builder.Append(' ', Math.Max(0, widths[column] - GetDisplayWidth(content)));
            builder.Append(" |");
        }
    }

    private static void AppendDelimiterRow(
        StringBuilder builder,
        IReadOnlyList<int> widths,
        IReadOnlyList<MarkdownTableAlignment> alignments)
    {
        builder.Append('|');
        for (var column = 0; column < widths.Count; column++)
        {
            var segmentWidth = widths[column] + 2;
            var alignment = alignments[column];
            var hasLeftColon = alignment is MarkdownTableAlignment.Left or MarkdownTableAlignment.Center;
            var hasRightColon = alignment is MarkdownTableAlignment.Right or MarkdownTableAlignment.Center;
            builder.Append(hasLeftColon ? ':' : '-');
            builder.Append('-', segmentWidth - 2);
            builder.Append(hasRightColon ? ':' : '-');
            builder.Append('|');
        }
    }

    private static int GetDisplayWidth(string text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.EnclosingMark
                or System.Globalization.UnicodeCategory.Format)
            {
                continue;
            }

            width += IsWide(rune.Value) ? 2 : 1;
        }

        return width;
    }

    private static bool IsWide(int value)
    {
        return value is >= 0x1100 and <= 0x115F
            or >= 0x2329 and <= 0x232A
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE10 and <= 0xFE19
            or >= 0xFE30 and <= 0xFE6F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x1F300 and <= 0x1FAFF
            or >= 0x20000 and <= 0x3FFFD;
    }
}

internal sealed record MarkdownTableFormatResult(
    string Text,
    IReadOnlyList<MarkdownTable> SourceTables,
    int SourceLength)
{
    public bool ContainsTables => SourceTables.Count > 0;
}
