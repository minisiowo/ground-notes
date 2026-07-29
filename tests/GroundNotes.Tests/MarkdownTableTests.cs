using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownTableTests
{
    [Fact]
    public void FindTables_ParsesGfmTableAndSourceLocations()
    {
        var text = "before\n| Product | Price |\n| --- | ---: |\n| Laptop | 3500 |\nafter";

        var table = Assert.Single(MarkdownTableParser.FindTables(text));

        Assert.Equal(2, table.ColumnCount);
        Assert.Equal(MarkdownTableAlignment.None, table.Alignments[0]);
        Assert.Equal(MarkdownTableAlignment.Right, table.Alignments[1]);
        Assert.Equal("Product", table.Header.Cells[0].Content);
        Assert.Equal("3500", table.Rows[2].Cells[1].Content);
        Assert.Equal(text.IndexOf("Product", StringComparison.Ordinal), table.Header.Cells[0].ContentStart);
    }

    [Fact]
    public void FindTables_PreservesEscapedPipesInsideCells()
    {
        var text = "| Value | Note |\n| --- | --- |\n| a\\|b | `x\\|y` |";

        var table = Assert.Single(MarkdownTableParser.FindTables(text));

        Assert.Equal("a\\|b", table.Rows[2].Cells[0].Content);
        Assert.Equal("`x\\|y`", table.Rows[2].Cells[1].Content);
    }

    [Fact]
    public void FindTables_IgnoresTablesInsideFencedCodeBlocks()
    {
        var text = "```\n| A | B |\n| --- | --- |\n| 1 | 2 |\n```";

        Assert.Empty(MarkdownTableParser.FindTables(text));
    }

    [Fact]
    public void FindTables_RejectsMismatchedHeaderAndDelimiter()
    {
        var text = "| A | B |\n| --- |";

        Assert.Empty(MarkdownTableParser.FindTables(text));
    }

    [Fact]
    public void FindTables_IgnoresIndentedCodeThatLooksLikeTable()
    {
        var text = "    | A | B |\n    | --- | --- |\n    | 1 | 2 |";

        Assert.Empty(MarkdownTableParser.FindTables(text));
    }

    [Fact]
    public void Format_AlignsColumnsAndIsIdempotent()
    {
        var text = "| Produkt | Kategoria | Cena | Dostępność |\n| --- | --- | --- | --- |\n| Laptop | Elektronika | 3500 zł | Dostępny |\n| Słuchawki | Akcesoria | 299 zł | Dostępny |";
        Assert.True(MarkdownTableEditingCommands.TryFormat(text, text.IndexOf("Laptop", StringComparison.Ordinal), out var first));
        var formatted = Apply(text, first);

        Assert.Equal(
            "| Produkt   | Kategoria   | Cena    | Dostępność |\n" +
            "|-----------|-------------|---------|------------|\n" +
            "| Laptop    | Elektronika | 3500 zł | Dostępny   |\n" +
            "| Słuchawki | Akcesoria   | 299 zł  | Dostępny   |",
            formatted);

        Assert.True(MarkdownTableEditingCommands.TryFormat(formatted, first.SelectionStart, out var second));
        Assert.Equal(formatted, Apply(formatted, second));
    }

    [Fact]
    public void Format_PreservesColumnAlignmentMarkers()
    {
        var text = "| Left | Center | Right |\n| :--- | :---: | ---: |\n| a | b | c |";

        Assert.True(MarkdownTableEditingCommands.TryFormat(text, text.IndexOf("a", StringComparison.Ordinal), out var edit));

        var formatted = Assert.Single(MarkdownTableParser.FindTables(edit.Replacement));
        Assert.Equal(
            new[] { MarkdownTableAlignment.Left, MarkdownTableAlignment.Center, MarkdownTableAlignment.Right },
            formatted.Alignments);
    }

    [Fact]
    public void Navigate_FromLastCellAddsBodyRowAndMovesCaretToFirstCell()
    {
        var text = "| A | B |\n|---|---|\n| x | y |";
        var caret = text.IndexOf("y", StringComparison.Ordinal) + 1;

        Assert.True(MarkdownTableEditingCommands.TryNavigate(text, caret, backwards: false, out var edit));
        var result = Apply(text, edit);
        var table = Assert.Single(MarkdownTableParser.FindTables(result));

        Assert.Equal(4, table.Rows.Count);
        Assert.Equal(table.Rows[3].Cells[0].EditableStart, edit.SelectionStart);
        Assert.True(edit.SelectionStart < table.Rows[3].Cells[0].SegmentStart + table.Rows[3].Cells[0].SegmentLength);
    }

    [Fact]
    public void DeleteOnlyBodyRow_LeavesValidHeaderOnlyTableAndExitsBelow()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| x | y |");
        var caret = text.IndexOf("x", StringComparison.Ordinal);

        Assert.True(MarkdownTableEditingCommands.TryDeleteRow(text, caret, out var edit));
        var result = Apply(text, edit);
        var table = Assert.Single(MarkdownTableParser.FindTables(result));

        Assert.Equal(2, table.Rows.Count);
        Assert.EndsWith("\n", result, StringComparison.Ordinal);
        Assert.Equal(edit.Start + edit.Replacement.Length, edit.SelectionStart);
    }

    [Fact]
    public void EnterOnEmptyLastRow_RemovesItAndExitsTable()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| x | y |\n| | |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var caret = table.Rows[^1].Cells[0].EditableStart;

        Assert.True(MarkdownTableEditingCommands.TryHandleEnter(text, caret, above: false, out var edit));
        var result = Apply(text, edit);
        var formatted = Assert.Single(MarkdownTableParser.FindTables(result));

        Assert.Equal(3, formatted.Rows.Count);
        Assert.Equal("x", formatted.Rows[2].Cells[0].Content);
        Assert.Equal(edit.Start + edit.Replacement.Length, edit.SelectionStart);
    }

    [Fact]
    public void InsertAndDeleteColumn_PreservesTableAndCaretColumn()
    {
        var text = "| A | B |\n|---|---|\n| x | y |";
        var caret = text.IndexOf("x", StringComparison.Ordinal);

        Assert.True(MarkdownTableEditingCommands.TryInsertColumn(text, caret, before: false, out var insert));
        var inserted = Apply(text, insert);
        Assert.Equal(3, Assert.Single(MarkdownTableParser.FindTables(inserted)).ColumnCount);

        Assert.True(MarkdownTableEditingCommands.TryDeleteColumn(inserted, insert.SelectionStart, out var delete));
        Assert.Equal(2, Assert.Single(MarkdownTableParser.FindTables(Apply(inserted, delete))).ColumnCount);
    }

    [Fact]
    public void MoveRowAndColumn_ReordersContentAndKeepsAlignmentWithColumn()
    {
        var text = "| A | B |\n|:---|---:|\n| one | two |\n| three | four |";
        var rowCaret = text.IndexOf("one", StringComparison.Ordinal);

        Assert.True(MarkdownTableEditingCommands.TryMoveRow(text, rowCaret, down: true, out var moveRow));
        var rowsMoved = Apply(text, moveRow);
        var movedTable = Assert.Single(MarkdownTableParser.FindTables(rowsMoved));
        Assert.Equal("three", movedTable.Rows[2].Cells[0].Content);
        Assert.Equal("one", movedTable.Rows[3].Cells[0].Content);

        Assert.True(MarkdownTableEditingCommands.TryMoveColumn(rowsMoved, moveRow.SelectionStart, right: true, out var moveColumn));
        var columnsMoved = Assert.Single(MarkdownTableParser.FindTables(Apply(rowsMoved, moveColumn)));
        Assert.Equal("B", columnsMoved.Header.Cells[0].Content);
        Assert.Equal(MarkdownTableAlignment.Right, columnsMoved.Alignments[0]);
        Assert.Equal(MarkdownTableAlignment.Left, columnsMoved.Alignments[1]);
    }

    [Fact]
    public void InsertTable_SelectsFirstHeaderName()
    {
        var edit = MarkdownTableEditingCommands.InsertTable(string.Empty, 0, 0);
        var table = Assert.Single(MarkdownTableParser.FindTables(edit.Replacement));

        Assert.Equal("Column 1", edit.Replacement.Substring(edit.SelectionStart, edit.SelectionLength));
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal(3, table.Rows.Count);
    }

    [Fact]
    public void InsertTable_DoesNotDeleteSelectionAndSeparatesTableFromSurroundingText()
    {
        const string text = "before selected after";
        var selectionStart = text.IndexOf("selected", StringComparison.Ordinal);

        var edit = MarkdownTableEditingCommands.InsertTable(text, selectionStart, "selected".Length);
        var result = Apply(text, edit);

        Assert.Contains("before \n| Column 1", result, StringComparison.Ordinal);
        Assert.Contains("\nselected after", result, StringComparison.Ordinal);
        Assert.Contains("selected", result, StringComparison.Ordinal);
        Assert.Single(MarkdownTableParser.FindTables(result));
    }

    [Fact]
    public void FormatAll_NormalizesEveryExistingTableWithoutTouchingOtherText()
    {
        var text = "before\n| A | Long header |\n|---|---|\n| value | x |\nafter";

        var normalized = MarkdownTableFormatter.FormatAll(text);

        Assert.Equal("before\n| A     | Long header |\n|-------|-------------|\n| value | x           |\nafter", normalized);
        Assert.Equal(normalized, MarkdownTableFormatter.FormatAll(normalized));
    }

    [Fact]
    public void InsertCellText_RejectsSelectionAcrossCells()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var start = table.Rows[2].Cells[0].EditableStart;
        var end = table.Rows[2].Cells[1].EditableEnd;

        Assert.False(MarkdownTableEditingCommands.TryInsertCellText(text, start, end - start, "replacement", out _));
    }

    [Fact]
    public void DeleteCharacter_DoesNotExposeEscapedPipeAsDelimiter()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| a\\|b | value |");
        var slashOffset = text.IndexOf("\\|", StringComparison.Ordinal);

        Assert.True(MarkdownTableEditingCommands.TryDeleteCharacter(text, slashOffset + 1, backwards: true, out var edit));
        var result = Apply(text, edit);
        var table = Assert.Single(MarkdownTableParser.FindTables(result));

        Assert.Equal("a\\|b", table.Rows[2].Cells[0].Content);
        Assert.Equal(2, table.ColumnCount);
    }

    [Fact]
    public void DeleteCharacter_ProtectsPaddingAndDeletesCellContent()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| text | value |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var cell = table.Rows[2].Cells[0];

        Assert.True(MarkdownTableEditingCommands.TryDeleteCharacter(text, cell.EditableStart, backwards: true, out var protectedEdit));
        Assert.Equal(text, Apply(text, protectedEdit));

        Assert.True(MarkdownTableEditingCommands.TryDeleteCharacter(text, cell.EditableEnd, backwards: true, out var deleteEdit));
        Assert.Contains("tex", Apply(text, deleteEdit), StringComparison.Ordinal);
    }

    private static string Apply(string text, MarkdownEditResult edit)
        => text[..edit.Start] + edit.Replacement + text[(edit.Start + edit.Length)..];
}
