namespace GroundNotes.Editors;

internal enum MarkdownTableAlignment
{
    None,
    Left,
    Center,
    Right
}

internal sealed record MarkdownTableCell(
    string Content,
    int ContentStart,
    int ContentLength,
    int SegmentStart,
    int SegmentLength)
{
    public int EditableStart => ContentLength == 0
        ? SegmentStart + Math.Min(1, SegmentLength)
        : ContentStart;

    public int EditableEnd => EditableStart + ContentLength;
}

internal sealed record MarkdownTableRow(
    int Index,
    int Start,
    int Length,
    bool IsDelimiter,
    IReadOnlyList<MarkdownTableCell> Cells);

internal sealed record MarkdownTable(
    int Start,
    int Length,
    int StartLineNumber,
    int EndLineNumber,
    string NewLine,
    IReadOnlyList<MarkdownTableAlignment> Alignments,
    IReadOnlyList<MarkdownTableRow> Rows)
{
    public int ColumnCount => Alignments.Count;

    public MarkdownTableRow Header => Rows[0];

    public IReadOnlyList<MarkdownTableRow> BodyRows => Rows.Count <= 2 ? [] : Rows.Skip(2).ToList();

    public bool TryGetCellAtOffset(int offset, out MarkdownTableCellPosition position)
    {
        position = default;
        if (Rows.Count == 0)
        {
            return false;
        }

        var clampedOffset = Math.Clamp(offset, Start, Start + Length);
        MarkdownTableRow? row = null;
        foreach (var candidate in Rows)
        {
            if (clampedOffset >= candidate.Start && clampedOffset <= candidate.Start + candidate.Length)
            {
                row = candidate;
                break;
            }
        }

        if (row is null || row.IsDelimiter || row.Cells.Count == 0)
        {
            return false;
        }

        var columnIndex = row.Cells.Count - 1;
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var cell = row.Cells[i];
            if (clampedOffset <= cell.SegmentStart + cell.SegmentLength)
            {
                columnIndex = i;
                break;
            }
        }

        columnIndex = Math.Clamp(columnIndex, 0, ColumnCount - 1);
        var selectedCell = row.Cells[Math.Min(columnIndex, row.Cells.Count - 1)];
        var contentOffset = Math.Clamp(clampedOffset - selectedCell.ContentStart, 0, selectedCell.ContentLength);
        position = new MarkdownTableCellPosition(row.Index, columnIndex, contentOffset);
        return true;
    }
}

internal readonly record struct MarkdownTableCellPosition(int RowIndex, int ColumnIndex, int ContentOffset);
