using Avalonia;
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
    private static readonly object ApplicationLock = new();
    private static bool s_applicationInitialized;

    [Fact]
    public void TextInput_InEmptyCellKeepsCaretAtContentAndFormatsImmediately()
    {
        EnsureApplication();
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

    [Fact]
    public void TextInput_TypedCharacterByCharacterPreservesSpaces()
    {
        EnsureApplication();
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

    [Fact]
    public void TextInput_FromRightPaddingIsCoercedToContentEnd()
    {
        EnsureApplication();
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

    [Fact]
    public void TextInput_ReplacesSelectionInsideCellAndBlocksCrossCellSelection()
    {
        EnsureApplication();
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

    [Fact]
    public void TextInput_EscapesPipesAndFlattensPastedNewlines()
    {
        EnsureApplication();
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

    [Fact]
    public void TextInputAndLiveFormattingUndoAsSingleOperation()
    {
        EnsureApplication();
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

    [Fact]
    public void ExternalDocumentReplacementInvalidatesActiveCellBuffer()
    {
        EnsureApplication();
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

    [Fact]
    public void LeavingCellWithTrailingSpacePreservesRequestedTargetCell()
    {
        EnsureApplication();
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

    [Fact]
    public void DeleteRowRepeatedlyDoesNotReenterCaretHandlingOrCrash()
    {
        EnsureApplication();
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

    [Fact]
    public void HostController_LiveFormattingWorksInVimInsertMode()
    {
        EnsureApplication();
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

    [Fact]
    public void SyncFromViewModel_NormalizesTablesBeforeTheyReachEditor()
    {
        EnsureApplication();
        var editor = new TextEditor { Document = new TextDocument() };
        var sync = new EditorTextSyncController(editor)
        {
            TextNormalizer = MarkdownTableFormatter.FormatAll
        };

        Assert.True(sync.SyncFromViewModel("| A | Long |\n|---|---|\n| value | x |", appendSuffixWhenPossible: false, out _));

        Assert.Equal("| A     | Long |\n|-------|------|\n| value | x    |", editor.Document.Text);
    }

    private static void EnsureApplication()
    {
        lock (ApplicationLock)
        {
            if (s_applicationInitialized || Application.Current is not null)
            {
                s_applicationInitialized = true;
                return;
            }

            try
            {
                GroundNotes.Program.BuildAvaloniaApp().SetupWithoutStarting();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
            {
            }

            s_applicationInitialized = true;
        }
    }
}
