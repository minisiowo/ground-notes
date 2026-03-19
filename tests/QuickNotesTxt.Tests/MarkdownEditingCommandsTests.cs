using QuickNotesTxt.Editors;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class MarkdownEditingCommandsTests
{
    [Fact]
    public void ToggleWrap_WrapsSelectedText()
    {
        var result = MarkdownEditingCommands.ToggleWrap("hello world", 6, 5, "**");

        Assert.Equal(6, result.Start);
        Assert.Equal(5, result.Length);
        Assert.Equal("**world**", result.Replacement);
        Assert.Equal(8, result.SelectionStart);
        Assert.Equal(5, result.SelectionLength);
    }

    [Fact]
    public void ToggleWrap_UnwrapsSelectedText()
    {
        var result = MarkdownEditingCommands.ToggleWrap("hello **world**", 8, 5, "**");

        Assert.Equal(6, result.Start);
        Assert.Equal(9, result.Length);
        Assert.Equal("world", result.Replacement);
    }

    [Fact]
    public void ToggleWrap_InsertsMarkersAtCaret()
    {
        var result = MarkdownEditingCommands.ToggleWrap("hello", 5, 0, "`");

        Assert.Equal("``", result.Replacement);
        Assert.Equal(6, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleTaskList_NormalizesMixedLinesToUncheckedTasks()
    {
        var result = MarkdownEditingCommands.ToggleTaskList("one\n- two\n- [x] three", 0, "one\n- two\n- [x] three".Length);

        Assert.Equal("- [ ] one\n- [ ] two\n- [ ] three", result.Replacement);
    }

    [Fact]
    public void ToggleTaskList_RemovesExistingTaskMarkers()
    {
        var result = MarkdownEditingCommands.ToggleTaskList("- [ ] one\n- [x] two", 0, "- [ ] one\n- [x] two".Length);

        Assert.Equal("one\ntwo", result.Replacement);
    }

    [Fact]
    public void ToggleBulletList_TogglesBulletPrefix()
    {
        var add = MarkdownEditingCommands.ToggleBulletList("one\ntwo", 0, "one\ntwo".Length);
        Assert.Equal("- one\n- two", add.Replacement);

        var remove = MarkdownEditingCommands.ToggleBulletList(add.Replacement, 0, add.Replacement.Length);
        Assert.Equal("one\ntwo", remove.Replacement);
    }

    [Fact]
    public void ToggleHeading_AddsAndRemovesMatchingHeadingLevel()
    {
        var add = MarkdownEditingCommands.ToggleHeading("title", 0, 5, 2);
        Assert.Equal("## title", add.Replacement);

        var remove = MarkdownEditingCommands.ToggleHeading(add.Replacement, 0, add.Replacement.Length, 2);
        Assert.Equal("title", remove.Replacement);
    }

    [Fact]
    public void ToggleHeading_OnEmptyLineInsertsHeadingMarker()
    {
        var result = MarkdownEditingCommands.ToggleHeading(string.Empty, 0, 0, 1);

        Assert.Equal("# ", result.Replacement);
        Assert.Equal(2, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleTaskList_OnEmptyLineInsertsUncheckedTask()
    {
        var result = MarkdownEditingCommands.ToggleTaskList(string.Empty, 0, 0);

        Assert.Equal("- [ ] ", result.Replacement);
        Assert.Equal(6, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleTaskList_AtStartOfFirstLineUpdatesFirstLine()
    {
        var result = MarkdownEditingCommands.ToggleTaskList("first\nsecond", 0, 0);

        Assert.Equal(0, result.Start);
        Assert.Equal(5, result.Length);
        Assert.Equal("- [ ] first", result.Replacement);
        Assert.Equal(11, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleTaskState_TogglesSingleTaskAtCaret()
    {
        var text = "- [ ] one";
        var result = MarkdownEditingCommands.ToggleTaskState(text, 4, 0);

        Assert.Equal("- [x] one", result.Replacement);
        Assert.Equal(4, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleTaskState_TogglesSelectedTaskLinesOnly()
    {
        var text = "- [ ] one\nplain\n- [x] two";
        var result = MarkdownEditingCommands.ToggleTaskState(text, 0, text.Length);

        Assert.Equal("- [x] one\nplain\n- [ ] two", result.Replacement);
    }

    [Fact]
    public void ToggleTaskState_OnNonTaskLineIsNoOp()
    {
        var result = MarkdownEditingCommands.ToggleTaskState("plain text", 3, 0);

        Assert.Equal(3, result.Start);
        Assert.Equal(0, result.Length);
        Assert.Equal(string.Empty, result.Replacement);
        Assert.Equal(3, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleBulletList_OnEmptyLineInsertsBullet()
    {
        var result = MarkdownEditingCommands.ToggleBulletList(string.Empty, 0, 0);

        Assert.Equal("- ", result.Replacement);
        Assert.Equal(2, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ToggleCodeBlock_WrapsSelectionAndInsertsEmptyBlock()
    {
        var insert = MarkdownEditingCommands.ToggleCodeBlock(string.Empty, 0, 0);
        Assert.Equal("```\n\n```", insert.Replacement);
        Assert.Equal(4, insert.SelectionStart);

        var wrap = MarkdownEditingCommands.ToggleCodeBlock("code", 0, 4);
        Assert.Equal("```\ncode\n```", wrap.Replacement);
    }

    [Fact]
    public void InsertLineBelow_InsertsBlankLineAfterCurrentLine()
    {
        var middle = MarkdownEditingCommands.InsertLineBelow("first\nsecond", 2);
        Assert.Equal(5, middle.Start);
        Assert.Equal("\n", middle.Replacement);
        Assert.Equal(6, middle.SelectionStart);
        Assert.Equal(0, middle.SelectionLength);

        var end = MarkdownEditingCommands.InsertLineBelow("first", 5);
        Assert.Equal(5, end.Start);
        Assert.Equal("\n", end.Replacement);
        Assert.Equal(6, end.SelectionStart);
        Assert.Equal(0, end.SelectionLength);
    }

    [Fact]
    public void MoveLines_MovesCurrentLineUp()
    {
        var text = "first\nsecond\nthird";
        var result = MarkdownEditingCommands.MoveLines(text, 8, 0, moveDown: false);

        Assert.Equal(0, result.Start);
        Assert.Equal("second\nfirst", result.Replacement);
        Assert.Equal(2, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void MoveLines_MovesCurrentLineDown()
    {
        var text = "first\nsecond\nthird";
        var result = MarkdownEditingCommands.MoveLines(text, 8, 0, moveDown: true);

        Assert.Equal(6, result.Start);
        Assert.Equal("third\nsecond", result.Replacement);
        Assert.Equal(14, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void MoveLines_MovingTopLineUpIsNoOp()
    {
        var text = "first\nsecond";
        var result = MarkdownEditingCommands.MoveLines(text, 2, 0, moveDown: false);

        Assert.Equal(2, result.Start);
        Assert.Equal(0, result.Length);
        Assert.Equal(string.Empty, result.Replacement);
        Assert.Equal(2, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void MoveLines_MovingBottomLineDownIsNoOp()
    {
        var text = "first\nsecond";
        var result = MarkdownEditingCommands.MoveLines(text, text.Length, 0, moveDown: true);

        Assert.Equal(text.Length, result.Start);
        Assert.Equal(0, result.Length);
        Assert.Equal(string.Empty, result.Replacement);
        Assert.Equal(text.Length, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void MoveLines_MovesTouchedSelectedLinesUpAsBlock()
    {
        var text = "zero\none\ntwo\nthree";
        var start = 7;
        var length = 5;
        var result = MarkdownEditingCommands.MoveLines(text, start, length, moveDown: false);

        Assert.Equal(0, result.Start);
        Assert.Equal("one\ntwo\nzero", result.Replacement);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(7, result.SelectionLength);
    }

    [Fact]
    public void MoveLines_MovesTouchedSelectedLinesDownAsBlock()
    {
        var text = "zero\none\ntwo\nthree";
        var start = 5;
        var length = 7;
        var result = MarkdownEditingCommands.MoveLines(text, start, length, moveDown: true);

        Assert.Equal(5, result.Start);
        Assert.Equal("three\none\ntwo", result.Replacement);
        Assert.Equal(11, result.SelectionStart);
        Assert.Equal(7, result.SelectionLength);
    }

    [Fact]
    public void MoveLines_PreservesBlankLinesAndIndentation()
    {
        var text = "before\n  one\n\n  two\nafter";
        var start = 7;
        var length = 11;
        var result = MarkdownEditingCommands.MoveLines(text, start, length, moveDown: true);

        Assert.Equal("after\n  one\n\n  two", result.Replacement);
        Assert.Equal(13, result.SelectionStart);
        Assert.Equal(12, result.SelectionLength);
    }

    [Fact]
    public void ChangeIndentation_InsertsConfiguredIndentAtCaret()
    {
        var result = MarkdownEditingCommands.ChangeIndentation("alpha", 2, 0, 2, unindent: false);

        Assert.Equal("  ", result.Replacement);
        Assert.Equal(4, result.SelectionStart);
        Assert.Equal(0, result.SelectionLength);
    }

    [Fact]
    public void ChangeIndentation_UsesConfiguredIndentWidthForSelectedLines()
    {
        var text = "one\ntwo";
        var result = MarkdownEditingCommands.ChangeIndentation(text, 0, text.Length, 2, unindent: false);

        Assert.Equal("  one\n  two", result.Replacement);
        Assert.Equal(2, result.SelectionStart);
        Assert.Equal(result.Replacement.Length - 2, result.SelectionLength);
    }

    [Fact]
    public void ChangeIndentation_UnindentsUsingConfiguredWidth()
    {
        var text = "  one\n two";
        var result = MarkdownEditingCommands.ChangeIndentation(text, 0, text.Length, 2, unindent: true);

        Assert.Equal("one\ntwo", result.Replacement);
        Assert.Equal(0, result.SelectionStart);
        Assert.Equal(result.Replacement.Length, result.SelectionLength);
    }

    [Fact]
    public void DeleteCurrentLine_RemovesCurrentOrSelectedLines()
    {
        var middle = MarkdownEditingCommands.DeleteCurrentLine("first\nsecond\nthird", 7, 0);
        Assert.Equal(5, middle.Start);
        Assert.Equal(7, middle.Length);
        Assert.Equal(string.Empty, middle.Replacement);
        Assert.Equal(5, middle.SelectionStart);

        var first = MarkdownEditingCommands.DeleteCurrentLine("first\nsecond", 0, 0);
        Assert.Equal(0, first.Start);
        Assert.Equal(6, first.Length);

        var all = MarkdownEditingCommands.DeleteCurrentLine("only line", 0, 0);
        Assert.Equal(0, all.Start);
        Assert.Equal("only line".Length, all.Length);
        Assert.Equal(string.Empty, all.Replacement);
    }
}
