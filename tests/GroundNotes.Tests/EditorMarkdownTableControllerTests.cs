using Avalonia;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using GroundNotes.Editors.Vim;
using GroundNotes.Models;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class EditorMarkdownTableControllerTests
{
    [AvaloniaFact]
    public void TextInput_InEmptyCellKeepsCaretAtContentAndFormatsImmediately()
    {
        var initial = MarkdownTableFormatter.FormatAll("| Agent | Description |\n|---|---|\n| oracle | |\n| delegate | text |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var initialTable = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = initialTable.Rows[2].Cells[1].EditableStart;

        editor.TextArea.PerformTextInput("coś piszę");

        var table = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        var editedCell = table.Rows[2].Cells[1];
        Assert.Equal("coś piszę", editedCell.Content);
        Assert.Equal(editedCell.EditableEnd, editor.CaretOffset);
        Assert.Equal(table.Rows[2].Cells[1].SegmentLength, table.Rows[3].Cells[1].SegmentLength);
    }

    [AvaloniaFact]
    public void TextInput_TypedCharacterByCharacterPreservesSpaces()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | Description |\n|---|---|\n| x | |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[1].EditableStart;

        foreach (var character in "hello world")
        {
            editor.TextArea.PerformTextInput(character.ToString());
        }

        var formatted = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("hello world", formatted.Rows[2].Cells[1].Content);
        Assert.Equal(formatted.Rows[2].Cells[1].EditableEnd, editor.CaretOffset);
    }

    [AvaloniaFact]
    public void TextInput_FromRightPaddingIsCoercedToContentEnd()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | Long header |\n|---|---|\n| x | value |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        var cell = table.Rows[2].Cells[0];
        editor.CaretOffset = cell.SegmentStart + cell.SegmentLength;
        Assert.Equal(cell.EditableEnd, editor.CaretOffset);

        editor.TextArea.PerformTextInput("y");

        var formatted = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("xy", formatted.Rows[2].Cells[0].Content);
        Assert.Equal(formatted.Rows[2].Cells[0].EditableEnd, editor.CaretOffset);
    }

    [AvaloniaFact]
    public void TextInput_ReplacesSelectionInsideCellAndBlocksCrossCellSelection()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        var firstCell = table.Rows[2].Cells[0];
        editor.Select(firstCell.EditableStart, firstCell.ContentLength);

        editor.TextArea.PerformTextInput("changed");

        var changed = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("changed", changed.Rows[2].Cells[0].Content);
        var beforeBlockedInput = editor.Document.Text;
        editor.Select(changed.Rows[2].Cells[0].EditableStart, changed.Rows[2].Cells[1].EditableEnd - changed.Rows[2].Cells[0].EditableStart);
        editor.TextArea.PerformTextInput("blocked");
        Assert.Equal(beforeBlockedInput, editor.Document.Text);
    }

    [AvaloniaFact]
    public void TextInput_EscapesPipesAndFlattensPastedNewlines()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| x | |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[1].EditableStart;

        editor.TextArea.PerformTextInput("a|b\nnext");

        var formatted = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("a\\|b next", formatted.Rows[2].Cells[1].Content);
        Assert.Equal(formatted.Rows[2].Cells[1].EditableEnd, editor.CaretOffset);
    }

    [AvaloniaFact]
    public void TextInputAndLiveFormattingUndoAsSingleOperation()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| x | |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[1].EditableStart;

        editor.TextArea.PerformTextInput("long value");
        Assert.NotEqual(initial, editor.Document.Text);

        editor.Undo();
        Assert.Equal(initial, editor.Document.Text);
    }

    [AvaloniaFact]
    public void ExternalDocumentReplacementInvalidatesActiveCellBuffer()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| old | |");
        var replacement = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| new | |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var initialTable = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = initialTable.Rows[2].Cells[1].EditableStart;
        editor.TextArea.PerformTextInput("stale ");

        editor.Document.Text = replacement;
        var replacementTable = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        editor.CaretOffset = replacementTable.Rows[2].Cells[1].EditableStart;
        editor.TextArea.PerformTextInput("c");

        var result = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("new", result.Rows[2].Cells[0].Content);
        Assert.Equal("c", result.Rows[2].Cells[1].Content);
    }

    [AvaloniaFact]
    public void LeavingCellWithTrailingSpacePreservesRequestedTargetCell()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        controller.SetCanHandleTextInput(static () => true);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[0].EditableEnd;
        editor.TextArea.PerformTextInput(" ");
        var withPendingSpace = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        editor.CaretOffset = withPendingSpace.Rows[2].Cells[1].EditableStart;

        var committed = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("one", committed.Rows[2].Cells[0].Content);
        Assert.Equal(committed.Rows[2].Cells[1].EditableStart, editor.CaretOffset);
    }

    [AvaloniaFact]
    public void DeleteRowRepeatedlyDoesNotReenterCaretHandlingOrCrash()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | first |\n| two | second |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[0].EditableStart;

        Assert.True(controller.TryDeleteRow());
        Assert.True(controller.TryDeleteRow());

        var remaining = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal(2, remaining.Rows.Count);
        Assert.True(editor.CaretOffset >= remaining.Start + remaining.Length);
    }

    [AvaloniaFact]
    public void CtrlBackspaceRepeatedlyKeepsTableStructureIntact()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one two | value |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[0].EditableEnd;

        for (var i = 0; i < 5; i++)
        {
            Assert.True(controller.TryHandleCellDeletion(true, true));
            Assert.Equal(2, Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text)).ColumnCount);
        }
    }

    [AvaloniaFact]
    public void DeleteWithCrossCellSelectionIsBlocked()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.Select(table.Rows[2].Cells[0].EditableStart, table.Rows[2].Cells[1].EditableEnd - table.Rows[2].Cells[0].EditableStart);
        Assert.True(controller.TryHandleCellDeletion(true));
        Assert.Equal(initial, editor.Document.Text);
    }

    [AvaloniaFact]
    public void BackspaceDeletesFullySelectedTable()
    {
        var tableText = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var initial = "Before\n" + tableText + "\nAfter";
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.Select(table.Start, table.Length);
        Assert.True(controller.TryHandleCellDeletion(true));
        Assert.Equal("Before\n\nAfter", editor.Document.Text);
    }

    [AvaloniaFact]
    public void BackspaceDeletesTextContainingCompleteTable()
    {
        var tableText = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var initial = "Before\n" + tableText + "\nAfter";
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        editor.Select(0, initial.Length);
        Assert.True(controller.TryHandleCellDeletion(true));
        Assert.Equal(string.Empty, editor.Document.Text);
    }

    [AvaloniaFact]
    public void BackspaceCannotExposeEscapedPipeAsSeparator()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| a\\|b | value |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var cell = Assert.Single(MarkdownTableParser.FindTables(initial)).Rows[2].Cells[0];
        editor.CaretOffset = cell.EditableStart + cell.Content.IndexOf('|');
        Assert.True(controller.TryHandleCellDeletion(true));
        Assert.Equal("a\\|b", Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text)).Rows[2].Cells[0].Content);
    }

    [AvaloniaFact]
    public void ExternalTextEdit_ChangesCellContentWithoutExposingStructure()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | value |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var cell = Assert.Single(MarkdownTableParser.FindTables(initial)).Rows[2].Cells[0];
        Assert.True(controller.TryApplyExternalTextEdit(cell.EditableStart, 1, string.Empty, cell.EditableStart));
        Assert.Equal("ne", Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text)).Rows[2].Cells[0].Content);
    }

    [AvaloniaFact]
    public void ExternalTextEdit_BlocksSeparatorDeletion()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | value |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var cell = Assert.Single(MarkdownTableParser.FindTables(initial)).Rows[2].Cells[0];
        var separator = cell.SegmentStart + cell.SegmentLength;
        Assert.True(controller.TryApplyExternalTextEdit(separator, 1, string.Empty, separator));
        Assert.Equal(initial, editor.Document.Text);
    }

    [AvaloniaFact]
    public void ExternalTextEdit_DeletesWholeBodyRowSemantically()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | value |\n| two | second |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var row = Assert.Single(MarkdownTableParser.FindTables(initial)).Rows[2];
        Assert.True(controller.TryApplyExternalTextEdit(row.Start, row.Length + 1, string.Empty, row.Start));
        Assert.Equal("two", Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text)).Rows[2].Cells[0].Content);
    }

    [AvaloniaFact]
    public void SelectAll_ExpandsFromCellToTableToDocument()
    {
        var tableText = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| one | two |");
        var initial = "Before\n" + tableText + "\nAfter";
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        var cell = table.Rows[2].Cells[0];
        editor.CaretOffset = cell.EditableStart + 1;

        Assert.True(controller.TryHandleSelectAll());
        Assert.Equal(cell.EditableStart, editor.SelectionStart);
        Assert.Equal(cell.ContentLength, editor.SelectionLength);

        Assert.True(controller.TryHandleSelectAll());
        Assert.Equal(table.Start, editor.SelectionStart);
        Assert.Equal(table.Length, editor.SelectionLength);

        Assert.True(controller.TryHandleSelectAll());
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(initial.Length, editor.SelectionLength);
    }

    [AvaloniaFact]
    public void SelectAll_EmptyCellStillAdvancesToWholeTableOnSecondInvocation()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| | two |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var controller = new EditorMarkdownTableController(editor);
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[0].EditableStart;

        Assert.True(controller.TryHandleSelectAll());
        Assert.Equal(0, editor.SelectionLength);
        Assert.True(controller.TryHandleSelectAll());
        Assert.Equal(table.Start, editor.SelectionStart);
        Assert.Equal(table.Length, editor.SelectionLength);
    }

    [AvaloniaFact]
    public void HostController_LiveFormattingWorksInVimInsertMode()
    {
        var initial = MarkdownTableFormatter.FormatAll("| A | B |\n|---|---|\n| x | |");
        var editor = new TextEditor { Document = new TextDocument(initial) };
        using var tableController = new EditorMarkdownTableController(editor);
        using var vimController = new VimEditorController(editor, new VimWorkspaceState());
        tableController.SetTextInputCoordination(
            () => !vimController.IsEnabled || vimController.Mode == VimMode.Insert,
            vimController.BeginExternalInsertUndoGroup,
            vimController.EndExternalInsertUndoGroup);
        vimController.SetSettings(VimModeSettings.Default with { IsEnabled = true });
        var table = Assert.Single(MarkdownTableParser.FindTables(initial));
        editor.CaretOffset = table.Rows[2].Cells[1].EditableStart;

        editor.TextArea.PerformTextInput("i");
        editor.TextArea.PerformTextInput("a");
        editor.TextArea.PerformTextInput("b");

        var formatted = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("ab", formatted.Rows[2].Cells[1].Content);
        Assert.Equal(formatted.Rows[2].Cells[1].EditableEnd, editor.CaretOffset);
        Assert.Equal(VimMode.Insert, vimController.Mode);

        Assert.True(vimController.ProcessSpecialKey(VimKey.Escape));
        editor.Undo();
        Assert.Equal(initial, editor.Document.Text);

        editor.TextArea.PerformTextInput("i");
        editor.TextArea.PerformTextInput("c");
        var afterUndoEdit = Assert.Single(MarkdownTableParser.FindTables(editor.Document.Text));
        Assert.Equal("c", afterUndoEdit.Rows[2].Cells[1].Content);
    }

    [AvaloniaFact]
    public void SyncFromViewModel_NormalizesTablesBeforeTheyReachEditor()
    {
        var editor = new TextEditor { Document = new TextDocument() };
        var sync = new EditorTextSyncController(editor)
        {
            TextNormalizer = MarkdownTableFormatter.FormatAll
        };

        Assert.True(sync.SyncFromViewModel("| A | Long |\n|---|---|\n| value | x |", appendSuffixWhenPossible: false, out _));

        Assert.Equal("| A     | Long |\n|-------|------|\n| value | x    |", editor.Document.Text);
    }

}
