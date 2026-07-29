namespace GroundNotes.Editors;

internal static class MarkdownTableEditingCommands
{
    public static MarkdownEditResult InsertTable(string text, int selectionStart, int selectionLength, int columns = 2, int bodyRows = 1)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var columnCount = Math.Clamp(columns, 1, 12);
        var rowCount = Math.Clamp(bodyRows, 1, 50);
        var rows = new List<IReadOnlyList<string>>
        {
            Enumerable.Range(1, columnCount).Select(index => $"Column {index}").ToList()
        };
        rows.AddRange(Enumerable.Range(0, rowCount).Select(_ => (IReadOnlyList<string>)Enumerable.Repeat(string.Empty, columnCount).ToList()));
        var alignments = Enumerable.Repeat(MarkdownTableAlignment.None, columnCount).ToList();
        var newLine = DetectNewLine(text);
        var tableText = MarkdownTableFormatter.Format(rows, alignments, newLine);
        var needsLeadingBreak = start > 0 && text[start - 1] is not ('\r' or '\n');
        var needsTrailingBreak = start < text.Length && text[start] is not ('\r' or '\n');
        var replacement = (needsLeadingBreak ? newLine : string.Empty)
                          + tableText
                          + (needsTrailingBreak ? newLine : string.Empty);
        var parsed = MarkdownTableParser.FindTables(replacement).Single();
        var firstCell = parsed.Header.Cells[0];
        return new MarkdownEditResult(start, 0, replacement, start + firstCell.ContentStart, firstCell.ContentLength);
    }

    public static bool TryFormat(string text, int caretOffset, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        edit = BuildEdit(table, GetRows(table), table.Alignments, position);
        return true;
    }

    public static bool TryNavigate(string text, int caretOffset, bool backwards, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var logicalRow = ToLogicalRow(position.RowIndex);
        var flatIndex = (logicalRow * table.ColumnCount) + position.ColumnIndex + (backwards ? -1 : 1);
        if (flatIndex < 0)
        {
            flatIndex = 0;
        }
        else if (flatIndex >= rows.Count * table.ColumnCount)
        {
            rows.Add(Enumerable.Repeat(string.Empty, table.ColumnCount).ToList());
        }

        var targetLogicalRow = flatIndex / table.ColumnCount;
        var targetColumn = flatIndex % table.ColumnCount;
        edit = BuildEdit(table, rows, table.Alignments, new MarkdownTableCellPosition(ToTableRow(targetLogicalRow), targetColumn, 0));
        return true;
    }

    public static bool TryInsertRow(string text, int caretOffset, bool above, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var currentLogicalRow = ToLogicalRow(position.RowIndex);
        var insertAt = position.RowIndex == 0 ? 1 : currentLogicalRow + (above ? 0 : 1);
        insertAt = Math.Clamp(insertAt, 1, rows.Count);
        rows.Insert(insertAt, Enumerable.Repeat(string.Empty, table.ColumnCount).ToList());
        edit = BuildEdit(table, rows, table.Alignments, new MarkdownTableCellPosition(ToTableRow(insertAt), position.ColumnIndex, 0));
        return true;
    }

    public static bool TryMoveRow(string text, int caretOffset, bool down, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position) || position.RowIndex == 0)
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var logicalRow = ToLogicalRow(position.RowIndex);
        var targetRow = logicalRow + (down ? 1 : -1);
        if (targetRow < 1 || targetRow >= rows.Count)
        {
            edit = default;
            return false;
        }

        (rows[logicalRow], rows[targetRow]) = (rows[targetRow], rows[logicalRow]);
        edit = BuildEdit(table, rows, table.Alignments, new MarkdownTableCellPosition(ToTableRow(targetRow), position.ColumnIndex, position.ContentOffset));
        return true;
    }

    public static bool TryDeleteRow(string text, int caretOffset, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position) || position.RowIndex == 0)
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var logicalRow = ToLogicalRow(position.RowIndex);
        rows.RemoveAt(logicalRow);
        if (rows.Count == 1)
        {
            edit = BuildExitAfterTableEdit(table, rows, table.Alignments);
            return true;
        }

        var targetLogicalRow = Math.Min(logicalRow, rows.Count - 1);
        edit = BuildEdit(table, rows, table.Alignments, new MarkdownTableCellPosition(ToTableRow(targetLogicalRow), position.ColumnIndex, 0));
        return true;
    }

    public static bool TryHandleEnter(string text, int caretOffset, bool above, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var isLastEmptyBodyRow = position.RowIndex >= 2
                                 && position.RowIndex == table.Rows.Count - 1
                                 && table.Rows[position.RowIndex].Cells.All(static cell => string.IsNullOrWhiteSpace(cell.Content));
        if (isLastEmptyBodyRow && !above)
        {
            var rows = GetRows(table);
            rows.RemoveAt(ToLogicalRow(position.RowIndex));
            edit = BuildExitAfterTableEdit(table, rows, table.Alignments);
            return true;
        }

        return TryInsertRow(text, caretOffset, above, out edit);
    }

    public static bool TrySetCellContent(
        string text,
        int tableStart,
        int rowIndex,
        int columnIndex,
        string content,
        int caretOffsetInContent,
        out MarkdownEditResult edit)
    {
        var table = MarkdownTableParser.FindTables(text).FirstOrDefault(candidate => candidate.Start == tableStart);
        if (table is null || rowIndex == 1 || rowIndex < 0 || rowIndex >= table.Rows.Count || columnIndex < 0 || columnIndex >= table.ColumnCount)
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var logicalRow = ToLogicalRow(rowIndex);
        rows[logicalRow][columnIndex] = content;
        edit = BuildEdit(
            table,
            rows,
            table.Alignments,
            new MarkdownTableCellPosition(rowIndex, columnIndex, Math.Clamp(caretOffsetInContent, 0, content.Length)));
        return true;
    }

    public static bool TryInsertColumn(string text, int caretOffset, bool before, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var insertAt = position.ColumnIndex + (before ? 0 : 1);
        var rows = GetRows(table);
        foreach (var row in rows)
        {
            row.Insert(insertAt, string.Empty);
        }

        rows[0][insertAt] = $"Column {insertAt + 1}";
        var alignments = table.Alignments.ToList();
        alignments.Insert(insertAt, MarkdownTableAlignment.None);
        edit = BuildEdit(table, rows, alignments, new MarkdownTableCellPosition(position.RowIndex, insertAt, 0));
        return true;
    }

    public static bool TryMoveColumn(string text, int caretOffset, bool right, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var targetColumn = position.ColumnIndex + (right ? 1 : -1);
        if (targetColumn < 0 || targetColumn >= table.ColumnCount)
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        foreach (var row in rows)
        {
            (row[position.ColumnIndex], row[targetColumn]) = (row[targetColumn], row[position.ColumnIndex]);
        }

        var alignments = table.Alignments.ToList();
        (alignments[position.ColumnIndex], alignments[targetColumn]) = (alignments[targetColumn], alignments[position.ColumnIndex]);
        edit = BuildEdit(table, rows, alignments, new MarkdownTableCellPosition(position.RowIndex, targetColumn, position.ContentOffset));
        return true;
    }

    public static bool TryDeleteColumn(string text, int caretOffset, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position) || table.ColumnCount <= 1)
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        foreach (var row in rows)
        {
            row.RemoveAt(position.ColumnIndex);
        }

        var alignments = table.Alignments.ToList();
        alignments.RemoveAt(position.ColumnIndex);
        var targetColumn = Math.Min(position.ColumnIndex, alignments.Count - 1);
        edit = BuildEdit(table, rows, alignments, new MarkdownTableCellPosition(position.RowIndex, targetColumn, 0));
        return true;
    }

    public static bool TrySetAlignment(string text, int caretOffset, MarkdownTableAlignment alignment, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var alignments = table.Alignments.ToList();
        alignments[position.ColumnIndex] = alignment;
        edit = BuildEdit(table, GetRows(table), alignments, position);
        return true;
    }

    public static bool IsInTable(string text, int caretOffset)
        => MarkdownTableParser.TryFindTableAtOffset(text, caretOffset, out _);

    public static bool TryGetEditableCaretOffset(string text, int caretOffset, out int editableOffset)
    {
        editableOffset = caretOffset;
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            return false;
        }

        var row = table.Rows[position.RowIndex];
        var cell = row.Cells[position.ColumnIndex];
        editableOffset = cell.EditableStart + Math.Clamp(position.ContentOffset, 0, cell.ContentLength);
        return true;
    }

    public static bool TryInsertCellText(string text, int selectionStart, int selectionLength, string insertedText, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, selectionStart, out var table, out var startPosition))
        {
            edit = default;
            return false;
        }

        var selectionEnd = Math.Clamp(selectionStart + selectionLength, selectionStart, text.Length);
        if (!table.TryGetCellAtOffset(selectionEnd, out var endPosition)
            || endPosition.RowIndex != startPosition.RowIndex
            || endPosition.ColumnIndex != startPosition.ColumnIndex)
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var logicalRow = ToLogicalRow(startPosition.RowIndex);
        var content = rows[logicalRow][startPosition.ColumnIndex];
        var contentStart = Math.Clamp(startPosition.ContentOffset, 0, content.Length);
        var contentEnd = Math.Clamp(endPosition.ContentOffset, contentStart, content.Length);
        var normalizedInput = NormalizeCellInput(insertedText);
        rows[logicalRow][startPosition.ColumnIndex] = content[..contentStart] + normalizedInput + content[contentEnd..];
        edit = BuildEdit(
            table,
            rows,
            table.Alignments,
            startPosition with { ContentOffset = contentStart + normalizedInput.Length });
        return true;
    }

    public static bool TryDeleteCharacter(string text, int caretOffset, bool backwards, out MarkdownEditResult edit)
    {
        if (!TryGetContext(text, caretOffset, out var table, out var position))
        {
            edit = default;
            return false;
        }

        var rows = GetRows(table);
        var logicalRow = ToLogicalRow(position.RowIndex);
        var content = rows[logicalRow][position.ColumnIndex];
        var deleteAt = backwards ? position.ContentOffset - 1 : position.ContentOffset;
        if (deleteAt < 0 || deleteAt >= content.Length)
        {
            edit = BuildEdit(table, rows, table.Alignments, position);
            return true;
        }

        var deleteLength = 1;
        if (backwards
            && deleteAt > 0
            && char.IsLowSurrogate(content[deleteAt])
            && char.IsHighSurrogate(content[deleteAt - 1]))
        {
            deleteAt--;
            deleteLength = 2;
        }
        else if (!backwards
                 && deleteAt + 1 < content.Length
                 && char.IsHighSurrogate(content[deleteAt])
                 && char.IsLowSurrogate(content[deleteAt + 1]))
        {
            deleteLength = 2;
        }

        rows[logicalRow][position.ColumnIndex] = EscapeUnescapedPipes(content.Remove(deleteAt, deleteLength));
        var targetOffset = backwards ? position.ContentOffset - deleteLength : position.ContentOffset;
        edit = BuildEdit(table, rows, table.Alignments, position with { ContentOffset = Math.Max(0, targetOffset) });
        return true;
    }

    private static bool TryGetContext(string text, int caretOffset, out MarkdownTable table, out MarkdownTableCellPosition position)
    {
        if (MarkdownTableParser.TryFindTableAtOffset(text, caretOffset, out table)
            && table.TryGetCellAtOffset(caretOffset, out position))
        {
            return true;
        }

        table = null!;
        position = default;
        return false;
    }

    private static List<List<string>> GetRows(MarkdownTable table)
    {
        return table.Rows
            .Where(static row => !row.IsDelimiter)
            .Select(row => row.Cells.Select(static cell => cell.Content).Take(table.ColumnCount).Concat(Enumerable.Repeat(string.Empty, table.ColumnCount)).Take(table.ColumnCount).ToList())
            .ToList();
    }

    private static MarkdownEditResult BuildEdit(
        MarkdownTable original,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<MarkdownTableAlignment> alignments,
        MarkdownTableCellPosition target)
    {
        var replacement = MarkdownTableFormatter.Format(rows, alignments, original.NewLine);
        var formatted = MarkdownTableParser.FindTables(replacement).Single();
        var targetRowIndex = Math.Clamp(target.RowIndex, 0, formatted.Rows.Count - 1);
        if (targetRowIndex == 1)
        {
            targetRowIndex = formatted.Rows.Count > 2 ? 2 : 0;
        }

        var row = formatted.Rows[targetRowIndex];
        var targetColumn = Math.Clamp(target.ColumnIndex, 0, row.Cells.Count - 1);
        var cell = row.Cells[targetColumn];
        var selectionStart = original.Start + cell.SegmentStart + Math.Min(cell.SegmentLength, 1 + Math.Max(0, target.ContentOffset));
        return new MarkdownEditResult(original.Start, original.Length, replacement, selectionStart, 0);
    }

    private static MarkdownEditResult BuildExitAfterTableEdit(
        MarkdownTable original,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<MarkdownTableAlignment> alignments)
    {
        var replacement = MarkdownTableFormatter.Format(rows, alignments, original.NewLine) + original.NewLine;
        return new MarkdownEditResult(original.Start, original.Length, replacement, original.Start + replacement.Length, 0);
    }

    private static int ToLogicalRow(int tableRow) => tableRow == 0 ? 0 : tableRow - 1;

    private static int ToTableRow(int logicalRow) => logicalRow == 0 ? 0 : logicalRow + 1;

    internal static string NormalizeCellInput(string text)
    {
        var flattened = text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        return EscapeUnescapedPipes(flattened);
    }

    private static string EscapeUnescapedPipes(string text)
    {
        if (!text.Contains('|'))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '|')
            {
                var backslashes = 0;
                for (var previous = i - 1; previous >= 0 && text[previous] == '\\'; previous--)
                {
                    backslashes++;
                }

                if (backslashes % 2 == 0)
                {
                    builder.Append('\\');
                }
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    private static string DetectNewLine(string text)
    {
        var newlineIndex = text.IndexOf('\n');
        return newlineIndex > 0 && text[newlineIndex - 1] == '\r' ? "\r\n" : "\n";
    }
}
