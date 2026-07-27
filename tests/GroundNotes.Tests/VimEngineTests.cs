using GroundNotes.Editors.Vim;
using Xunit;

namespace GroundNotes.Tests;

public sealed class VimEngineTests
{
    [Fact]
    public void VimInput_RepresentsPrintableAndSpecialKeys()
    {
        var printable = VimInput.Printable('x');
        var escape = VimInput.Special(VimKey.Escape);

        Assert.True(printable.IsPrintable);
        Assert.Equal('x', printable.Character);
        Assert.False(escape.IsPrintable);
        Assert.Equal(VimKey.Escape, escape.Key);
        Assert.Throws<ArgumentOutOfRangeException>(() => VimInput.Printable('\n'));
        Assert.Throws<ArgumentException>(() => VimInput.Special(VimKey.Printable));
    }

    [Fact]
    public void CountParser_ParsesMultiDigitCountAndSaturatesOverflow()
    {
        var parser = new VimCountParser();

        Assert.False(parser.TryAppend('0'));
        Assert.True(parser.TryAppend('1'));
        Assert.True(parser.TryAppend('2'));
        Assert.Equal(12, parser.Value);
        Assert.Equal(12, parser.Consume());
        Assert.False(parser.HasValue);

        foreach (var character in "999999999999999999999")
        {
            parser.TryAppend(character);
        }

        Assert.Equal(int.MaxValue, parser.Value);
    }

    [Fact]
    public void Count_PrefixesMotionAndIsClearedAfterCommand()
    {
        var engine = new VimEngine();
        var document = new VimDocumentSnapshot("abcdef", 0);

        engine.Process(VimInput.Printable('3'), document);
        Assert.Equal(3, engine.PendingCount);

        var result = engine.Process(VimInput.Printable('l'), document);

        Assert.Equal(3, Move(result).Offset);
        Assert.Null(engine.PendingCount);
    }

    [Theory]
    [InlineData('h', "abcdef", 3, 2)]
    [InlineData('l', "abcdef", 3, 4)]
    [InlineData('0', "  abc", 4, 0)]
    [InlineData('^', "  abc", 4, 2)]
    [InlineData('$', "abc\ndef", 0, 2)]
    public void CharacterMotions_MoveWithinCurrentLine(
        char command,
        string text,
        int caret,
        int expected)
    {
        var result = Send(new VimEngine(), text, caret, command);

        Assert.Equal(expected, Move(result).Offset);
    }

    [Fact]
    public void VerticalMotions_PreservePreferredColumnAcrossShortLine()
    {
        var engine = new VimEngine();
        var state = new EditorState("abcd\nx\nwxyz", 3);

        state = Apply(state, Send(engine, state, 'j'));
        Assert.Equal(5, state.Caret);

        state = Apply(state, Send(engine, state, 'j'));
        Assert.Equal(10, state.Caret);

        state = Apply(state, Send(engine, state, 'k'));
        Assert.Equal(5, state.Caret);
    }

    [Fact]
    public void GgGAndCountedG_MoveToRequestedLines()
    {
        const string text = "one\n  two\nthree";
        var engine = new VimEngine();

        engine.Process(VimInput.Printable('g'), new VimDocumentSnapshot(text, 10));
        var gg = engine.Process(VimInput.Printable('g'), new VimDocumentSnapshot(text, 10));
        Assert.Equal(0, Move(gg).Offset);

        var last = Send(engine, text, 0, 'G');
        Assert.Equal(10, Move(last).Offset);

        engine.Process(VimInput.Printable('2'), new VimDocumentSnapshot(text, 0));
        var second = engine.Process(VimInput.Printable('G'), new VimDocumentSnapshot(text, 0));
        Assert.Equal(6, Move(second).Offset);
    }

    [Theory]
    [InlineData('w', 0, 4)]
    [InlineData('b', 6, 4)]
    [InlineData('e', 0, 2)]
    public void WordMotions_UseKeywordAndWhitespaceBoundaries(char command, int caret, int expected)
    {
        var result = Send(new VimEngine(), "one two", caret, command);

        Assert.Equal(expected, Move(result).Offset);
    }

    [Fact]
    public void RepeatedWordEnd_AdvancesWhenCaretAlreadyAtWordEnd()
    {
        var engine = new VimEngine();
        var state = new EditorState("one two three", 0);

        state = Apply(state, Send(engine, state, 'e'));
        Assert.Equal(2, state.Caret);

        state = Apply(state, Send(engine, state, 'e'));
        Assert.Equal(6, state.Caret);

        state = Apply(state, Send(engine, state, 'e'));
        Assert.Equal(12, state.Caret);
    }

    [Fact]
    public void WordEnd_TreatsPunctuationAsASeparateWordClass()
    {
        var engine = new VimEngine();
        var state = new EditorState("one...two", 2);

        state = Apply(state, Send(engine, state, 'e'));
        Assert.Equal(5, state.Caret);

        state = Apply(state, Send(engine, state, 'e'));
        Assert.Equal(8, state.Caret);
    }

    [Fact]
    public void DeleteToWordEnd_FromCurrentWordEnd_ReachesNextWordEnd()
    {
        var engine = new VimEngine();
        var state = new EditorState("one two", 2);

        engine.Process(VimInput.Printable('d'), state.Snapshot);
        var result = engine.Process(VimInput.Printable('e'), state.Snapshot);

        Assert.Equal("on", Apply(state, result).Text);
        Assert.Equal("e two", engine.Register.Text);
    }

    [Fact]
    public void CountedWordEnd_MovesAcrossWords()
    {
        var engine = new VimEngine();
        var document = new VimDocumentSnapshot("one two three", 0);

        engine.Process(VimInput.Printable('2'), document);
        var result = engine.Process(VimInput.Printable('e'), document);

        Assert.Equal(6, Move(result).Offset);
    }

    [Theory]
    [InlineData('i', 3)]
    [InlineData('a', 4)]
    [InlineData('I', 2)]
    [InlineData('A', 5)]
    public void InsertCommands_EnterInsertModeAtExpectedOffset(char command, int expected)
    {
        var engine = new VimEngine();

        var result = Send(engine, "  abc\nnext", 3, command);

        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.True(result.ModeChanged);
        Assert.Equal(expected, Move(result).Offset);
    }

    [Fact]
    public void InsertMode_PassesTextThroughAndEscapeReturnsToNormal()
    {
        var engine = new VimEngine();
        var state = new EditorState("abc", 1);
        state = Apply(state, Send(engine, state, 'a'));

        var printable = engine.Process(VimInput.Printable('Z'), state.Snapshot);
        Assert.False(printable.IsHandled);
        Assert.Equal(VimMode.Insert, printable.Mode);

        state = new EditorState("abZc", 3);
        var escape = engine.Process(VimInput.Special(VimKey.Escape), state.Snapshot);

        Assert.True(escape.IsHandled);
        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Equal(2, Move(escape).Offset);
    }

    [Fact]
    public void InsertThenEscapeWithoutTyping_KeepsOriginalNormalCaret()
    {
        var engine = new VimEngine();
        var state = new EditorState("abc", 1);
        state = Apply(state, Send(engine, state, 'a'));

        var escape = engine.Process(VimInput.Special(VimKey.Escape), state.Snapshot);

        Assert.Equal(1, Move(escape).Offset);
    }

    [Fact]
    public void OpenBelowAndAbove_EmitAtomicLfEdits()
    {
        var belowEngine = new VimEngine();
        var below = Send(belowEngine, "one\ntwo", 1, 'o');
        var belowEdit = Edit(below);
        Assert.Equal(4, belowEdit.Start);
        Assert.Equal("\n", belowEdit.NewText);
        Assert.Equal(4, belowEdit.NewCaretOffset);
        Assert.Equal("one\n\ntwo", Apply(new EditorState("one\ntwo", 1), below).Text);

        var aboveEngine = new VimEngine();
        var above = Send(aboveEngine, "one\ntwo", 5, 'O');
        var aboveEdit = Edit(above);
        Assert.Equal(4, aboveEdit.Start);
        Assert.Equal("\n", aboveEdit.NewText);
        Assert.Equal(4, aboveEdit.NewCaretOffset);
    }

    [Fact]
    public void OpenCommands_PreserveCrLfAndHandleEmptyDocument()
    {
        var crlf = Send(new VimEngine(), "one\r\ntwo", 1, 'o');
        Assert.Equal("\r\n", Edit(crlf).NewText);
        Assert.Equal("one\r\n\r\ntwo", Apply(new EditorState("one\r\ntwo", 1), crlf).Text);

        var empty = Send(new VimEngine(), string.Empty, 0, 'o');
        Assert.Equal("\n", Edit(empty).NewText);
        Assert.Equal(1, Edit(empty).NewCaretOffset);
    }

    [Fact]
    public void X_DeletesCountedCharactersWithoutCrossingLineBreak()
    {
        var engine = new VimEngine();
        var document = new VimDocumentSnapshot("abcd\nef", 1);
        engine.Process(VimInput.Printable('9'), document);

        var result = engine.Process(VimInput.Printable('x'), document);
        var edit = Edit(result);

        Assert.Equal(1, edit.Start);
        Assert.Equal(3, edit.Length);
        Assert.Equal("bcd", engine.Register.Text);
        Assert.Equal(VimRegisterKind.CharacterWise, engine.Register.Kind);
        Assert.Equal("a\nef", Apply(new EditorState(document.Text, document.CaretOffset), result).Text);
    }

    [Fact]
    public void Dw_DeletesToNextWordAndStoresCharacterwiseRegister()
    {
        var engine = new VimEngine();
        var state = new EditorState("one two three", 0);

        engine.Process(VimInput.Printable('d'), state.Snapshot);
        Assert.Equal(VimMode.OperatorPending, engine.Mode);
        var result = engine.Process(VimInput.Printable('w'), state.Snapshot);

        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Equal("one ", engine.Register.Text);
        Assert.Equal(VimRegisterKind.CharacterWise, engine.Register.Kind);
        Assert.Equal("two three", Apply(state, result).Text);
    }

    [Theory]
    [InlineData("2dw", "three", "one two ")]
    [InlineData("d2w", "three", "one two ")]
    [InlineData("2d2w", "", "one two three")]
    public void OperatorAndMotionCounts_AreCombined(string keys, string expectedText, string expectedRegister)
    {
        var engine = new VimEngine();
        var state = new EditorState("one two three", 0);
        VimCommandResult? result = null;

        foreach (var key in keys)
        {
            result = engine.Process(VimInput.Printable(key), state.Snapshot);
        }

        Assert.NotNull(result);
        Assert.Equal(expectedRegister, engine.Register.Text);
        Assert.Equal(expectedText, Apply(state, result).Text);
    }

    [Fact]
    public void DeAndDollars_UseInclusiveMotionRanges()
    {
        var deEngine = new VimEngine();
        var deState = new EditorState("one two", 0);
        deEngine.Process(VimInput.Printable('d'), deState.Snapshot);
        var de = deEngine.Process(VimInput.Printable('e'), deState.Snapshot);
        Assert.Equal(" two", Apply(deState, de).Text);

        var dollarEngine = new VimEngine();
        var dollarState = new EditorState("one two\nnext", 4);
        dollarEngine.Process(VimInput.Printable('d'), dollarState.Snapshot);
        var dollar = dollarEngine.Process(VimInput.Printable('$'), dollarState.Snapshot);
        Assert.Equal("one \nnext", Apply(dollarState, dollar).Text);
    }

    [Fact]
    public void Dd_DeletesMiddleLineAndStoresLinewiseRegister()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 5);

        engine.Process(VimInput.Printable('d'), state.Snapshot);
        var result = engine.Process(VimInput.Printable('d'), state.Snapshot);

        Assert.Equal("two\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
        Assert.Equal("one\nthree", Apply(state, result).Text);
    }

    [Fact]
    public void Dd_OnFinalUnterminatedLine_RemovesPrecedingLineBreak()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo", 5);

        engine.Process(VimInput.Printable('d'), state.Snapshot);
        var result = engine.Process(VimInput.Printable('d'), state.Snapshot);

        Assert.Equal("two\n", engine.Register.Text);
        Assert.Equal("one", Apply(state, result).Text);
    }

    [Fact]
    public void CountedDd_PreservesCrLfInLinewiseRegisterAndEdit()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\r\ntwo\r\nthree", 0);
        engine.Process(VimInput.Printable('2'), state.Snapshot);
        engine.Process(VimInput.Printable('d'), state.Snapshot);

        var result = engine.Process(VimInput.Printable('d'), state.Snapshot);

        Assert.Equal("one\r\ntwo\r\n", engine.Register.Text);
        Assert.Equal("three", Apply(state, result).Text);
    }

    [Fact]
    public void Yy_YanksLinesWithoutEditingDocument()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo", 0);

        engine.Process(VimInput.Printable('y'), state.Snapshot);
        var result = engine.Process(VimInput.Printable('y'), state.Snapshot);

        Assert.Equal("one\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
        Assert.Empty(result.Operations.OfType<VimTextEditOperation>());
    }

    [Fact]
    public void Yj_UsesLinewiseRegisterKind()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 0);

        engine.Process(VimInput.Printable('y'), state.Snapshot);
        engine.Process(VimInput.Printable('j'), state.Snapshot);

        Assert.Equal("one\ntwo\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
    }

    [Fact]
    public void CountedYy_YanksRequestedNumberOfLines()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 0);
        engine.Process(VimInput.Printable('2'), state.Snapshot);
        engine.Process(VimInput.Printable('y'), state.Snapshot);
        engine.Process(VimInput.Printable('y'), state.Snapshot);

        Assert.Equal("one\ntwo\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
    }

    [Fact]
    public void OperatorsWithGgAndG_SelectLinewiseRanges()
    {
        var deleteEngine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 5);
        deleteEngine.Process(VimInput.Printable('d'), state.Snapshot);
        var delete = deleteEngine.Process(VimInput.Printable('G'), state.Snapshot);
        Assert.Equal("one", Apply(state, delete).Text);
        Assert.Equal("two\nthree\n", deleteEngine.Register.Text);

        var yankEngine = new VimEngine();
        var lastLine = new EditorState("one\ntwo\nthree", 9);
        yankEngine.Process(VimInput.Printable('y'), lastLine.Snapshot);
        yankEngine.Process(VimInput.Printable('g'), lastLine.Snapshot);
        yankEngine.Process(VimInput.Printable('g'), lastLine.Snapshot);
        Assert.Equal("one\ntwo\nthree\n", yankEngine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, yankEngine.Register.Kind);
    }

    [Fact]
    public void CharacterwisePAndP_PasteAfterAndBeforeCaret()
    {
        var afterEngine = EngineWithCharacterRegister("abc", 1);
        var after = Send(afterEngine, "ac", 0, 'p');
        Assert.Equal("abc", Apply(new EditorState("ac", 0), after).Text);
        Assert.Equal(1, Edit(after).NewCaretOffset);

        var beforeEngine = EngineWithCharacterRegister("abc", 1);
        var before = Send(beforeEngine, "ac", 1, 'P');
        Assert.Equal("abc", Apply(new EditorState("ac", 1), before).Text);
    }

    [Fact]
    public void LinewiseP_PastesBelowCurrentLine()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo", 0);
        engine.Process(VimInput.Printable('y'), state.Snapshot);
        engine.Process(VimInput.Printable('y'), state.Snapshot);

        var paste = engine.Process(VimInput.Printable('p'), state.Snapshot);

        Assert.Equal("one\none\ntwo", Apply(state, paste).Text);
        Assert.Equal(4, Edit(paste).NewCaretOffset);
    }

    [Fact]
    public void CountedLinewisePaste_PreservesCrLf()
    {
        var engine = new VimEngine();
        var source = new EditorState("one\r\ntwo", 0);
        engine.Process(VimInput.Printable('y'), source.Snapshot);
        engine.Process(VimInput.Printable('y'), source.Snapshot);

        var target = new EditorState("one\r\ntwo", 6);
        engine.Process(VimInput.Printable('2'), target.Snapshot);
        var paste = engine.Process(VimInput.Printable('P'), target.Snapshot);

        Assert.Equal("one\r\none\r\none\r\ntwo", Apply(target, paste).Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
    }

    [Fact]
    public void V_EntersVisualWithInclusiveSelectionAndEscapeClearsIt()
    {
        var engine = new VimEngine();
        var state = new EditorState("abcd", 1);

        var enter = Send(engine, state, 'v');

        Assert.Equal(VimMode.Visual, engine.Mode);
        Assert.Equal(new VimSetSelectionOperation(1, 1), Selection(enter));

        var escape = engine.Process(VimInput.Special(VimKey.Escape), state.Snapshot);
        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.IsType<VimClearSelectionOperation>(Assert.Single(escape.Operations));
    }

    [Fact]
    public void UppercaseV_SelectsWholeCrLfLine()
    {
        var engine = new VimEngine();

        var result = Send(engine, "one\r\ntwo", 1, 'V');

        Assert.Equal(VimMode.VisualLine, engine.Mode);
        Assert.Equal(new VimSetSelectionOperation(0, 3), Selection(result));
    }

    [Fact]
    public void VisualLineYank_HighlightsOnlyContentButKeepsLineDelimiterInRegister()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\r\ntwo", 1);

        var enter = Send(engine, state, 'V');
        var yank = Send(engine, state, 'y');

        Assert.Equal(new VimSetSelectionOperation(0, 3), Selection(enter));
        Assert.Equal("one\r\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
        Assert.Empty(yank.Operations.OfType<VimTextEditOperation>());
    }

    [Fact]
    public void VisualMotionAndCount_UpdateCaretAndSelection()
    {
        var engine = new VimEngine();
        var state = new EditorState("abcdef", 1);
        _ = Send(engine, state, 'v');
        engine.Process(VimInput.Printable('3'), state.Snapshot);

        var result = engine.Process(VimInput.Printable('l'), state.Snapshot);

        Assert.Equal(4, Assert.Single(result.Operations.OfType<VimMoveCaretOperation>()).Offset);
        Assert.Equal(new VimSetSelectionOperation(1, 4), Selection(result));
        Assert.Null(engine.PendingCount);
    }

    [Fact]
    public void CountedVisualEntry_SelectsCharactersOrLines()
    {
        var characterEngine = new VimEngine();
        var characterState = new EditorState("abcdef", 1);
        characterEngine.Process(VimInput.Printable('3'), characterState.Snapshot);
        var characters = characterEngine.Process(VimInput.Printable('v'), characterState.Snapshot);
        Assert.Equal(3, Assert.Single(characters.Operations.OfType<VimMoveCaretOperation>()).Offset);
        Assert.Equal(new VimSetSelectionOperation(1, 3), Selection(characters));

        var lineEngine = new VimEngine();
        var lineState = new EditorState("one\ntwo\nthree", 0);
        lineEngine.Process(VimInput.Printable('2'), lineState.Snapshot);
        var lines = lineEngine.Process(VimInput.Printable('V'), lineState.Snapshot);
        Assert.Equal(new VimSetSelectionOperation(0, 7), Selection(lines));
    }

    [Fact]
    public void VisualLineMotion_ExtendsSelectionByWholeLines()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 0);
        _ = Send(engine, state, 'V');

        var result = Send(engine, state, 'j');

        Assert.Equal(4, Assert.Single(result.Operations.OfType<VimMoveCaretOperation>()).Offset);
        Assert.Equal(new VimSetSelectionOperation(0, 7), Selection(result));
    }

    [Fact]
    public void VisualMode_CanSwitchToVisualLineAndToggleOff()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo", 1);
        _ = Send(engine, state, 'v');

        var linewise = Send(engine, state, 'V');
        Assert.Equal(VimMode.VisualLine, engine.Mode);
        Assert.Equal(new VimSetSelectionOperation(0, 3), Selection(linewise));

        var exit = Send(engine, state, 'V');
        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.IsType<VimClearSelectionOperation>(Assert.Single(exit.Operations));
    }

    [Fact]
    public void VisualD_DeletesInclusiveSelectionAndClearsSelection()
    {
        var engine = new VimEngine();
        var state = new EditorState("abcdef", 1);
        _ = Send(engine, state, 'v');
        engine.Process(VimInput.Printable('2'), state.Snapshot);
        var motion = engine.Process(VimInput.Printable('l'), state.Snapshot);
        state = Apply(state, motion);

        var result = Send(engine, state, 'd');

        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Equal("bcd", engine.Register.Text);
        Assert.Equal(VimRegisterKind.CharacterWise, engine.Register.Kind);
        Assert.Equal("aef", Apply(state, result).Text);
        Assert.Single(result.Operations.OfType<VimClearSelectionOperation>());
    }

    [Fact]
    public void VisualY_YanksSelectionAndReturnsCaretToSelectionStart()
    {
        var engine = new VimEngine();
        var state = new EditorState("abcdef", 4);
        _ = Send(engine, state, 'v');
        engine.Process(VimInput.Printable('2'), state.Snapshot);
        state = Apply(state, engine.Process(VimInput.Printable('h'), state.Snapshot));

        var result = Send(engine, state, 'y');

        Assert.Equal("cde", engine.Register.Text);
        Assert.Empty(result.Operations.OfType<VimTextEditOperation>());
        Assert.Equal(2, Assert.Single(result.Operations.OfType<VimMoveCaretOperation>()).Offset);
        Assert.Single(result.Operations.OfType<VimClearSelectionOperation>());
    }

    [Fact]
    public void VisualC_DeletesSelectionAndEntersInsert()
    {
        var engine = new VimEngine();
        var state = new EditorState("one two", 4);
        _ = Send(engine, state, 'v');
        engine.Process(VimInput.Printable('2'), state.Snapshot);
        state = Apply(state, engine.Process(VimInput.Printable('l'), state.Snapshot));

        var result = Send(engine, state, 'c');

        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.Equal("one ", Apply(state, result).Text);
        Assert.Equal("two", engine.Register.Text);
        Assert.Single(result.Operations.OfType<VimClearSelectionOperation>());
    }

    [Fact]
    public void VisualLineD_UsesLinewiseRegisterAndPreservesCrLf()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\r\ntwo\r\nthree", 0);
        _ = Send(engine, state, 'V');
        state = Apply(state, Send(engine, state, 'j'));

        var result = Send(engine, state, 'd');

        Assert.Equal("one\r\ntwo\r\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
        Assert.Equal("three", Apply(state, result).Text);
    }

    [Fact]
    public void VisualUppercaseC_ChangesWholeTouchedLines()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 1);
        _ = Send(engine, state, 'v');
        state = Apply(state, Send(engine, state, 'j'));

        var result = Send(engine, state, 'C');

        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.Equal("\nthree", Apply(state, result).Text);
        Assert.Equal("one\ntwo\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
    }

    [Fact]
    public void ChangeWithMotion_DeletesMotionRangeAndEntersInsert()
    {
        var engine = new VimEngine();
        var state = new EditorState("one two", 0);
        engine.Process(VimInput.Printable('c'), state.Snapshot);

        var result = engine.Process(VimInput.Printable('e'), state.Snapshot);

        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.Equal(" two", Apply(state, result).Text);
        Assert.Equal("one", engine.Register.Text);
        Assert.Equal(0, Edit(result).NewCaretOffset);
    }

    [Fact]
    public void Cw_ChangesThroughWordEndWithoutDeletingFollowingSpace()
    {
        var engine = new VimEngine();
        var state = new EditorState("one two", 0);
        engine.Process(VimInput.Printable('c'), state.Snapshot);

        var result = engine.Process(VimInput.Printable('w'), state.Snapshot);

        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.Equal(" two", Apply(state, result).Text);
        Assert.Equal("one", engine.Register.Text);
    }

    [Fact]
    public void CountedCc_ChangesLinesAndLeavesOneInsertLine()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 0);
        engine.Process(VimInput.Printable('2'), state.Snapshot);
        engine.Process(VimInput.Printable('c'), state.Snapshot);

        var result = engine.Process(VimInput.Printable('c'), state.Snapshot);

        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.Equal("\nthree", Apply(state, result).Text);
        Assert.Equal("one\ntwo\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
    }

    [Theory]
    [InlineData("diw", "one  three", "two")]
    [InlineData("daw", "one three", "two ")]
    [InlineData("d2iw", "one ", "two three")]
    [InlineData("2daw", "one", " two three")]
    public void WordTextObjects_DeleteExpectedRange(
        string keys,
        string expectedText,
        string expectedRegister)
    {
        var engine = new VimEngine();
        var state = new EditorState("one two three", 5);
        VimCommandResult? result = null;
        foreach (var key in keys)
        {
            result = engine.Process(VimInput.Printable(key), state.Snapshot);
        }

        Assert.NotNull(result);
        Assert.Equal(expectedText, Apply(state, result).Text);
        Assert.Equal(expectedRegister, engine.Register.Text);
        Assert.Equal(VimRegisterKind.CharacterWise, engine.Register.Kind);
    }

    [Fact]
    public void Ciw_ChangesInnerWordAndYiwOnlyUpdatesRegister()
    {
        var changeEngine = new VimEngine();
        var state = new EditorState("one two", 5);
        changeEngine.Process(VimInput.Printable('c'), state.Snapshot);
        changeEngine.Process(VimInput.Printable('i'), state.Snapshot);
        var change = changeEngine.Process(VimInput.Printable('w'), state.Snapshot);
        Assert.Equal(VimMode.Insert, changeEngine.Mode);
        Assert.Equal("one ", Apply(state, change).Text);

        var yankEngine = new VimEngine();
        yankEngine.Process(VimInput.Printable('y'), state.Snapshot);
        yankEngine.Process(VimInput.Printable('i'), state.Snapshot);
        var yank = yankEngine.Process(VimInput.Printable('w'), state.Snapshot);
        Assert.Equal(VimMode.Normal, yankEngine.Mode);
        Assert.Equal("two", yankEngine.Register.Text);
        Assert.Empty(yank.Operations.OfType<VimTextEditOperation>());
    }

    [Fact]
    public void UppercaseAliases_MapToLineEndOrWholeLineOperators()
    {
        var deleteState = new EditorState("one two\nthree", 4);
        var deleteEngine = new VimEngine();
        var delete = Send(deleteEngine, deleteState, 'D');
        Assert.Equal("one \nthree", Apply(deleteState, delete).Text);

        var changeEngine = new VimEngine();
        var change = Send(changeEngine, deleteState, 'C');
        Assert.Equal(VimMode.Insert, changeEngine.Mode);
        Assert.Equal("one \nthree", Apply(deleteState, change).Text);

        var yankEngine = new VimEngine();
        var yank = Send(yankEngine, deleteState, 'Y');
        Assert.Equal("one two\n", yankEngine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, yankEngine.Register.Kind);
        Assert.Empty(yank.Operations.OfType<VimTextEditOperation>());
    }

    [Fact]
    public void CountedYAlias_YanksRequestedLines()
    {
        var engine = new VimEngine();
        var state = new EditorState("one\ntwo\nthree", 0);
        engine.Process(VimInput.Printable('2'), state.Snapshot);

        _ = engine.Process(VimInput.Printable('Y'), state.Snapshot);

        Assert.Equal("one\ntwo\n", engine.Register.Text);
        Assert.Equal(VimRegisterKind.LineWise, engine.Register.Kind);
    }

    [Fact]
    public void SAndCountedS_SubstituteCharactersAndEnterInsert()
    {
        var characterEngine = new VimEngine();
        var state = new EditorState("abcdef", 1);
        characterEngine.Process(VimInput.Printable('3'), state.Snapshot);
        var substitute = characterEngine.Process(VimInput.Printable('s'), state.Snapshot);
        Assert.Equal(VimMode.Insert, characterEngine.Mode);
        Assert.Equal("aef", Apply(state, substitute).Text);
        Assert.Equal("bcd", characterEngine.Register.Text);

        var lineEngine = new VimEngine();
        var lines = new EditorState("one\ntwo", 0);
        var lineSubstitute = Send(lineEngine, lines, 'S');
        Assert.Equal(VimMode.Insert, lineEngine.Mode);
        Assert.Equal("\ntwo", Apply(lines, lineSubstitute).Text);
        Assert.Equal(VimRegisterKind.LineWise, lineEngine.Register.Kind);
    }

    [Fact]
    public void VisualModes_AreSafeOnEmptyDocument()
    {
        var characterEngine = new VimEngine();
        var state = new EditorState(string.Empty, 0);
        var visual = Send(characterEngine, state, 'v');
        Assert.Equal(new VimSetSelectionOperation(0, 0), Selection(visual));
        var change = Send(characterEngine, state, 'c');
        Assert.Equal(VimMode.Insert, characterEngine.Mode);
        Assert.Single(change.Operations.OfType<VimClearSelectionOperation>());

        var lineEngine = new VimEngine();
        var visualLine = Send(lineEngine, state, 'V');
        Assert.Equal(new VimSetSelectionOperation(0, 0), Selection(visualLine));
    }

    [Fact]
    public void UndoAndCtrlR_AreNeutralHistoryIntentsWithCounts()
    {
        var engine = new VimEngine();
        var document = new VimDocumentSnapshot("text", 0);
        engine.Process(VimInput.Printable('3'), document);
        var undo = engine.Process(VimInput.Printable('u'), document);

        var undoIntent = Assert.IsType<VimHistoryOperation>(Assert.Single(undo.Operations));
        Assert.Equal(VimHistoryAction.Undo, undoIntent.Action);
        Assert.Equal(3, undoIntent.Count);

        engine.Process(VimInput.Printable('2'), document);
        var redo = engine.Process(VimInput.Special(VimKey.CtrlR), document);
        var redoIntent = Assert.IsType<VimHistoryOperation>(Assert.Single(redo.Operations));
        Assert.Equal(VimHistoryAction.Redo, redoIntent.Action);
        Assert.Equal(2, redoIntent.Count);
    }

    [Fact]
    public void Escape_CancelsPendingOperatorAndCount()
    {
        var engine = new VimEngine();
        var document = new VimDocumentSnapshot("text", 0);
        engine.Process(VimInput.Printable('2'), document);
        engine.Process(VimInput.Printable('d'), document);

        var result = engine.Process(VimInput.Special(VimKey.Escape), document);

        Assert.True(result.IsHandled);
        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Null(engine.PendingCount);
        Assert.Empty(result.Operations);
    }

    [Fact]
    public void EmptyDocument_AllRequiredMotionsAndDeleteAreSafe()
    {
        foreach (var command in new[] { 'h', 'j', 'k', 'l', '0', '^', '$', 'G', 'w', 'b', 'e', 'x' })
        {
            var result = Send(new VimEngine(), string.Empty, 0, command);
            Assert.True(result.IsHandled);
        }

        var ggEngine = new VimEngine();
        ggEngine.Process(VimInput.Printable('g'), new VimDocumentSnapshot(string.Empty, 0));
        var gg = ggEngine.Process(VimInput.Printable('g'), new VimDocumentSnapshot(string.Empty, 0));
        Assert.Equal(0, Move(gg).Offset);
    }

    private static VimEngine EngineWithCharacterRegister(string text, int caret)
    {
        var engine = new VimEngine();
        var state = new EditorState(text, caret);
        var delete = Send(engine, state, 'x');
        _ = Apply(state, delete);
        return engine;
    }

    private static VimCommandResult Send(VimEngine engine, string text, int caret, char command)
        => engine.Process(VimInput.Printable(command), new VimDocumentSnapshot(text, caret));

    private static VimCommandResult Send(VimEngine engine, EditorState state, char command)
        => engine.Process(VimInput.Printable(command), state.Snapshot);

    private static VimMoveCaretOperation Move(VimCommandResult result)
        => Assert.IsType<VimMoveCaretOperation>(Assert.Single(result.Operations));

    private static VimTextEditOperation Edit(VimCommandResult result)
        => Assert.Single(result.Operations.OfType<VimTextEditOperation>());

    private static VimSetSelectionOperation Selection(VimCommandResult result)
        => Assert.Single(result.Operations.OfType<VimSetSelectionOperation>());

    private static EditorState Apply(EditorState state, VimCommandResult result)
    {
        var text = state.Text;
        var caret = state.Caret;
        foreach (var operation in result.Operations)
        {
            switch (operation)
            {
                case VimMoveCaretOperation move:
                    caret = move.Offset;
                    break;
                case VimTextEditOperation edit:
                    text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText);
                    caret = edit.NewCaretOffset;
                    break;
            }
        }

        return new EditorState(text, caret);
    }

    private sealed record EditorState(string Text, int Caret)
    {
        public VimDocumentSnapshot Snapshot => new(Text, Caret);
    }
}
