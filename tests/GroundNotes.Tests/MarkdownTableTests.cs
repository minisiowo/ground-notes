using AvaloniaEdit.Document;
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
    public void PresentationIndex_ReusesParsedTableAcrossConsumers()
    {
        var document = new TextDocument("| A | B |\n|---|---|\n| x | y |");
        using var index = new MarkdownTablePresentationIndex();

        var table = Assert.Single(index.GetTables(document));

        Assert.Same(table, index.GetTableForLine(document, 1));
        Assert.Same(table, index.GetTableForLine(document, 3));
    }

    [Fact]
    public void PresentationIndex_OrdinaryTextEditDoesNotRequestTableLayoutRefresh()
    {
        var document = new TextDocument("ordinary text\n\n| A | B |\n|---|---|\n| x | y |");
        using var index = new MarkdownTablePresentationIndex();
        _ = index.GetTables(document);
        var refreshRequests = 0;
        index.Invalidated += (_, _) => refreshRequests++;

        document.Insert("ordinary".Length, " updated");

        Assert.Equal(0, refreshRequests);
    }

    [Fact]
    public void PresentationIndex_TableEditInvalidatesEntireTableRange()
    {
        var document = new TextDocument("before\n| A | B |\n|---|---|\n| x | y |\nafter");
        using var index = new MarkdownTablePresentationIndex();
        var table = Assert.Single(index.GetTables(document));
        MarkdownTablePresentationInvalidatedEventArgs? invalidation = null;
        index.Invalidated += (_, args) => invalidation = args;

        document.Replace(table.Rows[2].Cells[0].ContentStart, 1, "longer");

        Assert.NotNull(invalidation);
        Assert.Equal(table.StartLineNumber, invalidation.StartLine);
        Assert.Equal(table.EndLineNumber, invalidation.EndLine);
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
    public void CellHitTesting_TreatsOffsetAfterClosingPipeAsOutsideTable()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var row = table.Rows[2];
        var closingPipeOffset = row.Cells[^1].SegmentStart + row.Cells[^1].SegmentLength;

        Assert.True(table.TryGetCellAtOffset(closingPipeOffset, out var atPipe));
        Assert.Equal(1, atPipe.ColumnIndex);
        Assert.True(MarkdownTableEditingCommands.IsInTable(text, closingPipeOffset));
        Assert.False(table.TryGetCellAtOffset(closingPipeOffset + 1, out _));
        Assert.False(MarkdownTableEditingCommands.IsInTable(text, closingPipeOffset + 1));
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

    [Theory]
    [InlineData(true, "Column 1", "A", "B")]
    [InlineData(false, "A", "Column 2", "B")]
    public void InsertColumn_AddsColumnOnRequestedSide(bool before, string firstHeader, string secondHeader, string thirdHeader)
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| x | y |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var caret = table.Rows[2].Cells[0].EditableStart;

        Assert.True(MarkdownTableEditingCommands.TryInsertColumn(text, caret, before, out var edit));
        var inserted = Assert.Single(MarkdownTableParser.FindTables(Apply(text, edit)));

        Assert.Equal(3, inserted.ColumnCount);
        Assert.Equal(firstHeader, inserted.Header.Cells[0].Content);
        Assert.Equal(secondHeader, inserted.Header.Cells[1].Content);
        Assert.Equal(thirdHeader, inserted.Header.Cells[2].Content);
    }

    [Fact]
    public void MoveColumn_LeftAndRightUseCurrentCaretColumn()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B | C |\n|---|---|---|\n| one | two | three |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var middleCaret = table.Rows[2].Cells[1].EditableStart;

        Assert.True(MarkdownTableEditingCommands.TryMoveColumn(text, middleCaret, right: false, out var moveLeft));
        var movedLeftText = Apply(text, moveLeft);
        var movedLeft = Assert.Single(MarkdownTableParser.FindTables(movedLeftText));
        Assert.Equal(new[] { "B", "A", "C" }, movedLeft.Header.Cells.Select(cell => cell.Content).ToArray());

        Assert.True(MarkdownTableEditingCommands.TryMoveColumn(movedLeftText, moveLeft.SelectionStart, right: true, out var moveRight));
        var movedRight = Assert.Single(MarkdownTableParser.FindTables(Apply(movedLeftText, moveRight)));
        Assert.Equal(new[] { "A", "B", "C" }, movedRight.Header.Cells.Select(cell => cell.Content).ToArray());
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
    public void FormatAllWithMetadata_FormatsMultipleTablesAndPreservesSourceBoundaries()
    {
        const string source = "| A | Long |\n|---|---|\n| x | value |\n\nbetween\n\n| B | C |\n|---|---|\n| y | z |";

        var result = MarkdownTableFormatter.FormatAllWithMetadata(source);

        Assert.True(result.ContainsTables);
        Assert.Equal(source.Length, result.SourceLength);
        Assert.Equal(2, result.SourceTables.Count);
        Assert.Equal(0, result.SourceTables[0].Start);
        var lastTable = result.SourceTables[^1];
        Assert.Equal(source.Length, lastTable.Start + lastTable.Length);
        Assert.Contains("| A   | Long  |", result.Text, StringComparison.Ordinal);
        Assert.Contains("| B   | C   |", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void TryFormatPastedText_FormatsSingleTable()
    {
        const string pasted = "| A | Long header |\n|---|---|\n| x | value |";
        Assert.True(MarkdownTableFormatter.TryFormatPastedText(pasted, out var formatted));
        Assert.Equal("| A   | Long header |\n|-----|-------------|\n| x   | value       |", formatted);
    }

    [Fact]
    public void TryFormatPastedText_FormatsTableInsideLargerTextOnly()
    {
        const string pasted = "Before\n\n| A | Long header |\n|---|---|\n| x | value |\n\nAfter";
        Assert.True(MarkdownTableFormatter.TryFormatPastedText(pasted, out var formatted));
        Assert.Equal("Before\n\n| A   | Long header |\n|-----|-------------|\n| x   | value       |\n\nAfter", formatted);
    }

    [Fact]
    public void TryFormatPastedText_LeavesTextWithoutTablesOnDefaultPastePath()
    {
        const string pasted = "Regular text\nwithout a table";
        Assert.False(MarkdownTableFormatter.TryFormatPastedText(pasted, out var formatted));
        Assert.Equal(pasted, formatted);
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
    public void InsertCellText_SlashInlineCodeSnippetFormatsTableAndPlacesCaretBetweenMarkers()
    {
        var text = MarkdownTableFormatter.FormatAll("| Header 1 | Header 2 |\n|---|---|\n| content | value |\n| /code | |");
        var triggerStart = text.IndexOf("/code", StringComparison.Ordinal);

        Assert.True(MarkdownTableEditingCommands.TryInsertCellText(text, triggerStart, "/code".Length, "``", 1, out var edit));
        var result = Apply(text, edit);
        var table = Assert.Single(MarkdownTableParser.FindTables(result));
        var snippetCell = table.Rows[3].Cells[0];

        Assert.Equal("``", snippetCell.Content);
        Assert.Equal(snippetCell.EditableStart + 1, edit.SelectionStart);
        Assert.Equal(table.Rows[2].Cells[0].SegmentLength, snippetCell.SegmentLength);
    }

    [Fact]
    public void AdaptCellEdit_WrapsSelectionAndKeepsTableFormatted()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | value |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var cell = table.Rows[2].Cells[0];
        var rawEdit = MarkdownEditingCommands.ToggleWrap(text, cell.EditableStart, cell.ContentLength, "`");

        Assert.True(MarkdownTableEditingCommands.TryAdaptCellEdit(text, rawEdit, out var edit));
        var result = Apply(text, edit);
        var formatted = Assert.Single(MarkdownTableParser.FindTables(result));
        Assert.Equal("`one`", formatted.Rows[2].Cells[0].Content);
        Assert.Equal("one", result.Substring(edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void AdaptCellEdit_RejectsStructuralRange()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | value |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var firstCell = table.Rows[2].Cells[0];
        var secondCell = table.Rows[2].Cells[1];
        var rawEdit = new MarkdownEditResult(firstCell.EditableStart, secondCell.EditableEnd - firstCell.EditableStart, string.Empty, firstCell.EditableStart, 0);
        Assert.False(MarkdownTableEditingCommands.TryAdaptCellEdit(text, rawEdit, out _));
    }

    [Fact]
    public void ReplaceSelectionContainingTables_AllowsCompleteTablesOnly()
    {
        const string tableText = "| A | B |\n|---|---|\n| one | two |";
        var document = "Before\n" + tableText + "\nAfter";
        var table = Assert.Single(MarkdownTableParser.FindTables(document));

        Assert.True(MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(document, table.Start, table.Length));
        Assert.True(MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(document, 0, document.Length));
        Assert.False(MarkdownTableEditingCommands.CanReplaceSelectionContainingTables(document, table.Start, table.Rows[0].Length));
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

    [Fact]
    public void DeleteCellContent_ByWordNeverCrossesCellBoundary()
    {
        const string content = "one two";
        MarkdownTableEditingCommands.DeleteCellContent(content, content.Length, 0, true, true, out var first, out var firstCaret);
        MarkdownTableEditingCommands.DeleteCellContent(first, firstCaret, 0, true, true, out var second, out var secondCaret);
        MarkdownTableEditingCommands.DeleteCellContent(second, secondCaret, 0, true, true, out var third, out var thirdCaret);
        Assert.Equal("one ", first);
        Assert.Equal(string.Empty, second);
        Assert.Equal(string.Empty, third);
        Assert.Equal(0, thirdCaret);
    }

    [Fact]
    public void DeleteCellContent_ReescapesPipeExposedByDeletion()
    {
        const string content = "a\\|b";
        MarkdownTableEditingCommands.DeleteCellContent(content, content.IndexOf('|'), 0, true, false, out var result, out var caret);
        Assert.Equal(content, result);
        Assert.Equal(content.IndexOf('\\'), caret);
    }

    [Fact]
    public void DeleteCellSelection_RejectsCrossCellSelection()
    {
        var text = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var table = Assert.Single(MarkdownTableParser.FindTables(text));
        var start = table.Rows[2].Cells[0].EditableStart;
        var end = table.Rows[2].Cells[1].EditableEnd;
        Assert.False(MarkdownTableEditingCommands.TryDeleteCellSelection(text, start, end - start, out _));
    }

    private static string Apply(string text, MarkdownEditResult edit)
        => text[..edit.Start] + edit.Replacement + text[(edit.Start + edit.Length)..];
}
