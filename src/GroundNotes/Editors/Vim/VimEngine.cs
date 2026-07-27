using System;
using System.Collections.Generic;
using System.Text;

namespace GroundNotes.Editors.Vim;

public sealed class VimEngine
{
    private readonly VimCountParser _count = new();
    private PendingOperator _pendingOperator;
    private int _operatorCount = 1;
    private bool _awaitingSecondG;
    private int? _preferredColumn;
    private int _insertEntryOffset;
    private int _insertFallbackOffset;
    private int _visualAnchor;
    private char? _pendingTextObjectPrefix;

    public VimMode Mode { get; private set; } = VimMode.Normal;

    public int? PendingCount => _count.HasValue ? _count.Value : null;

    public VimRegister Register { get; private set; } = VimRegister.Empty;

    public void ImportRegister(VimRegister register)
    {
        ArgumentNullException.ThrowIfNull(register);
        Register = register;
    }

    public VimCommandResult Process(VimInput input, VimDocumentSnapshot document)
    {
        var previousMode = Mode;
        return Mode switch
        {
            VimMode.Insert => ProcessInsert(input, document, previousMode),
            VimMode.OperatorPending => ProcessOperatorPending(input, document, previousMode),
            VimMode.Visual or VimMode.VisualLine => ProcessVisual(input, document, previousMode),
            _ => ProcessNormal(input, document, previousMode)
        };
    }

    public void Reset()
    {
        Mode = VimMode.Normal;
        Register = VimRegister.Empty;
        ResetPendingCommand();
        _preferredColumn = null;
        _insertEntryOffset = 0;
        _insertFallbackOffset = 0;
        _visualAnchor = 0;
        _pendingTextObjectPrefix = null;
    }

    private VimCommandResult ProcessInsert(
        VimInput input,
        VimDocumentSnapshot document,
        VimMode previousMode)
    {
        if (input.Key != VimKey.Escape)
        {
            return Result(false, previousMode);
        }

        var buffer = new VimTextBuffer(document.Text);
        var target = document.CaretOffset switch
        {
            var offset when offset > _insertEntryOffset => buffer.NormalizeNormalOffset(offset - 1),
            var offset when offset == _insertEntryOffset => buffer.NormalizeNormalOffset(_insertFallbackOffset),
            _ => buffer.NormalizeNormalOffset(document.CaretOffset)
        };

        Mode = VimMode.Normal;
        ResetPendingCommand();
        _preferredColumn = null;
        return Result(true, previousMode, new VimMoveCaretOperation(target));
    }

    private VimCommandResult ProcessNormal(
        VimInput input,
        VimDocumentSnapshot document,
        VimMode previousMode)
    {
        if (input.Key == VimKey.Escape)
        {
            ResetPendingCommand();
            return Result(true, previousMode);
        }

        if (input.Key == VimKey.CtrlR)
        {
            var count = _count.Consume();
            _awaitingSecondG = false;
            _preferredColumn = null;
            return Result(true, previousMode, new VimHistoryOperation(VimHistoryAction.Redo, count));
        }

        if (!input.IsPrintable)
        {
            ResetPendingCommand();
            return Result(false, previousMode);
        }

        var character = input.Character;
        if (_awaitingSecondG)
        {
            if (character == 'g')
            {
                var countSpecified = _count.HasValue;
                var count = _count.Consume();
                _awaitingSecondG = false;
                return ExecuteMotion(document, previousMode, 'g', count, countSpecified, isGg: true);
            }

            ResetPendingCommand();
            return Result(true, previousMode);
        }

        if (_count.TryAppend(character))
        {
            return Result(true, previousMode);
        }

        if (character == 'g')
        {
            _awaitingSecondG = true;
            return Result(true, previousMode);
        }

        if (character is 'v' or 'V')
        {
            var count = _count.Consume();
            _preferredColumn = null;
            return EnterVisual(document, previousMode, linewise: character == 'V', count);
        }

        if (character is 'd' or 'y' or 'c')
        {
            _operatorCount = _count.Consume();
            _pendingOperator = GetOperator(character);
            Mode = VimMode.OperatorPending;
            _preferredColumn = null;
            return Result(true, previousMode);
        }

        if (character is 'D' or 'C' or 'Y')
        {
            var count = _count.Consume();
            _preferredColumn = null;
            return ExecuteAliasOperator(character, document, previousMode, count);
        }

        if (character is 's' or 'S')
        {
            var count = _count.Consume();
            _preferredColumn = null;
            return ExecuteSubstitute(document, previousMode, count, linewise: character == 'S');
        }

        if (character is 'i' or 'a' or 'I' or 'A' or 'o' or 'O')
        {
            _count.Reset();
            _preferredColumn = null;
            return EnterInsert(character, document, previousMode);
        }

        if (character == 'x')
        {
            var count = _count.Consume();
            _preferredColumn = null;
            return DeleteCharacters(document, previousMode, count);
        }

        if (character is 'p' or 'P')
        {
            var count = _count.Consume();
            _preferredColumn = null;
            return Paste(document, previousMode, after: character == 'p', count);
        }

        if (character == 'u')
        {
            var count = _count.Consume();
            _preferredColumn = null;
            return Result(true, previousMode, new VimHistoryOperation(VimHistoryAction.Undo, count));
        }

        var motionCountSpecified = _count.HasValue;
        var motionCount = _count.Consume();
        return ExecuteMotion(document, previousMode, character, motionCount, motionCountSpecified, isGg: false);
    }

    private VimCommandResult ProcessOperatorPending(
        VimInput input,
        VimDocumentSnapshot document,
        VimMode previousMode)
    {
        if (input.Key == VimKey.Escape)
        {
            CancelOperator();
            return Result(true, previousMode);
        }

        if (!input.IsPrintable)
        {
            CancelOperator();
            return Result(false, previousMode);
        }

        var character = input.Character;
        if (_pendingTextObjectPrefix is not null)
        {
            if (character == 'w')
            {
                var textObjectCount = CombineOperatorCount(_count.Consume());
                return ExecuteTextObject(
                    document,
                    previousMode,
                    around: _pendingTextObjectPrefix == 'a',
                    textObjectCount);
            }

            CancelOperator();
            return Result(true, previousMode);
        }

        if (_awaitingSecondG)
        {
            if (character == 'g')
            {
                var motionCountSpecified = _count.HasValue;
                var motionCount = _count.Consume();
                _awaitingSecondG = false;
                return ExecuteOperatorMotion(
                    document,
                    previousMode,
                    'g',
                    CombineOperatorCount(motionCount),
                    motionCountSpecified || _operatorCount != 1,
                    isGg: true);
            }

            CancelOperator();
            return Result(true, previousMode);
        }

        if (_count.TryAppend(character))
        {
            return Result(true, previousMode);
        }

        if (character == 'g')
        {
            _awaitingSecondG = true;
            return Result(true, previousMode);
        }

        if (character is 'i' or 'a')
        {
            _pendingTextObjectPrefix = character;
            return Result(true, previousMode);
        }

        var pendingCharacter = _pendingOperator switch
        {
            PendingOperator.Delete => 'd',
            PendingOperator.Yank => 'y',
            PendingOperator.Change => 'c',
            _ => default
        };
        if (character == pendingCharacter)
        {
            var lineCount = CombineOperatorCount(_count.Consume());
            return ExecuteLinewiseOperator(document, previousMode, lineCount);
        }

        var countSpecified = _count.HasValue || _operatorCount != 1;
        var count = CombineOperatorCount(_count.Consume());
        return ExecuteOperatorMotion(document, previousMode, character, count, countSpecified, isGg: false);
    }

    private VimCommandResult ProcessVisual(
        VimInput input,
        VimDocumentSnapshot document,
        VimMode previousMode)
    {
        if (input.Key == VimKey.Escape)
        {
            return ExitVisual(previousMode);
        }

        if (!input.IsPrintable)
        {
            return Result(false, previousMode);
        }

        var character = input.Character;
        if (_awaitingSecondG)
        {
            if (character == 'g')
            {
                var prefixCountSpecified = _count.HasValue;
                var prefixCount = _count.Consume();
                _awaitingSecondG = false;
                return ExecuteVisualMotion(
                    document,
                    previousMode,
                    'g',
                    prefixCount,
                    prefixCountSpecified,
                    isGg: true);
            }

            _awaitingSecondG = false;
            _count.Reset();
            return Result(true, previousMode);
        }

        if (_count.TryAppend(character))
        {
            return Result(true, previousMode);
        }

        if (character == 'g')
        {
            _awaitingSecondG = true;
            return Result(true, previousMode);
        }

        if (character is 'v' or 'V')
        {
            var requestedMode = character == 'V' ? VimMode.VisualLine : VimMode.Visual;
            _count.Reset();
            if (Mode == requestedMode)
            {
                return ExitVisual(previousMode);
            }

            Mode = requestedMode;
            return Result(true, previousMode, CreateSelectionOperation(document, document.CaretOffset));
        }

        if (character is 'd' or 'y' or 'c' or 's' or 'D' or 'C' or 'Y' or 'S')
        {
            _count.Reset();
            var linewise = Mode == VimMode.VisualLine || character is 'D' or 'C' or 'Y' or 'S';
            var operatorCharacter = character switch
            {
                's' or 'S' => 'c',
                'D' => 'd',
                'C' => 'c',
                'Y' => 'y',
                _ => character
            };
            return ExecuteVisualOperator(
                document,
                previousMode,
                GetOperator(operatorCharacter),
                linewise);
        }

        var countSpecified = _count.HasValue;
        var count = _count.Consume();
        return ExecuteVisualMotion(document, previousMode, character, count, countSpecified, isGg: false);
    }

    private VimCommandResult ExecuteVisualMotion(
        VimDocumentSnapshot document,
        VimMode previousMode,
        char character,
        int count,
        bool countSpecified,
        bool isGg)
    {
        if (!TryGetMotion(document, character, count, countSpecified, isGg, out var motion))
        {
            _preferredColumn = null;
            return Result(true, previousMode);
        }

        _preferredColumn = motion.PreservesPreferredColumn ? motion.PreferredColumn : null;
        return Result(
            true,
            previousMode,
            new VimMoveCaretOperation(motion.CaretTarget),
            CreateSelectionOperation(document, motion.CaretTarget));
    }

    private VimCommandResult ExecuteVisualOperator(
        VimDocumentSnapshot document,
        VimMode previousMode,
        PendingOperator pendingOperator,
        bool linewise)
    {
        var selection = GetVisualSelection(document, document.CaretOffset, linewise);
        _pendingOperator = pendingOperator;
        VimCommandResult result;
        if (linewise)
        {
            result = ApplyLinewiseOperator(document, previousMode, selection.FirstLine, selection.LastLine);
        }
        else
        {
            result = ApplyCharacterwiseRange(document, previousMode, selection.Start, selection.End);
        }

        var yank = pendingOperator == PendingOperator.Yank;
        FinishOperator();
        var operations = new List<VimOperation>(result.Operations);
        if (yank)
        {
            operations.Add(new VimMoveCaretOperation(selection.Start));
        }

        operations.Add(new VimClearSelectionOperation());
        return result with { Mode = Mode, Operations = operations };
    }

    private VimCommandResult ExecuteMotion(
        VimDocumentSnapshot document,
        VimMode previousMode,
        char character,
        int count,
        bool countSpecified,
        bool isGg)
    {
        if (!TryGetMotion(document, character, count, countSpecified, isGg, out var motion))
        {
            _preferredColumn = null;
            return Result(true, previousMode);
        }

        _preferredColumn = motion.PreservesPreferredColumn ? motion.PreferredColumn : null;
        return Result(true, previousMode, new VimMoveCaretOperation(motion.CaretTarget));
    }

    private VimCommandResult ExecuteOperatorMotion(
        VimDocumentSnapshot document,
        VimMode previousMode,
        char character,
        int count,
        bool countSpecified,
        bool isGg)
    {
        var motionCharacter = character;
        if (_pendingOperator == PendingOperator.Change && character == 'w' && document.Text.Length > 0)
        {
            var buffer = new VimTextBuffer(document.Text);
            var origin = buffer.NormalizeNormalOffset(document.CaretOffset);
            if (GetCharacterClass(document.Text[origin]) != CharacterClass.WhiteSpace)
            {
                motionCharacter = 'e';
            }
        }

        if (!TryGetMotion(document, motionCharacter, count, countSpecified, isGg, out var motion))
        {
            CancelOperator();
            return Result(true, previousMode);
        }

        VimCommandResult result;
        if (motion.Shape == MotionShape.LineWise)
        {
            var buffer = new VimTextBuffer(document.Text);
            var firstLine = Math.Min(buffer.GetLineIndex(document.CaretOffset), motion.TargetLine);
            var lastLine = Math.Max(buffer.GetLineIndex(document.CaretOffset), motion.TargetLine);
            result = ApplyLinewiseOperator(document, previousMode, firstLine, lastLine);
        }
        else
        {
            result = ApplyCharacterwiseOperator(document, previousMode, motion);
        }

        FinishOperator();
        return result with { Mode = Mode };
    }

    private VimCommandResult ExecuteLinewiseOperator(
        VimDocumentSnapshot document,
        VimMode previousMode,
        int lineCount)
    {
        var buffer = new VimTextBuffer(document.Text);
        var firstLine = buffer.GetLineIndex(document.CaretOffset);
        var lastLine = Math.Min(buffer.LineCount - 1, AddClamped(firstLine, lineCount - 1));
        var result = ApplyLinewiseOperator(document, previousMode, firstLine, lastLine);
        FinishOperator();
        return result with { Mode = Mode };
    }

    private VimCommandResult ApplyCharacterwiseOperator(
        VimDocumentSnapshot document,
        VimMode previousMode,
        Motion motion)
    {
        var buffer = new VimTextBuffer(document.Text);
        var origin = buffer.NormalizeNormalOffset(document.CaretOffset);
        int start;
        int end;

        if (motion.RawTarget >= origin)
        {
            start = origin;
            end = motion.Shape == MotionShape.CharacterInclusive
                ? Math.Min(document.Text.Length, motion.RawTarget + 1)
                : Math.Min(document.Text.Length, motion.RawTarget);
        }
        else
        {
            start = Math.Max(0, motion.RawTarget);
            end = origin;
        }

        return ApplyCharacterwiseRange(document, previousMode, start, end);
    }

    private VimCommandResult ApplyCharacterwiseRange(
        VimDocumentSnapshot document,
        VimMode previousMode,
        int start,
        int end)
    {
        start = Math.Clamp(start, 0, document.Text.Length);
        end = Math.Clamp(end, start, document.Text.Length);
        if (end <= start)
        {
            if (_pendingOperator == PendingOperator.Change)
            {
                SetInsertEntry(start, start);
            }

            return Result(true, previousMode);
        }

        var register = new VimRegister(document.Text[start..end], VimRegisterKind.CharacterWise);
        SetRegister(register);
        var registerOperation = new VimSetRegisterOperation(register);

        if (_pendingOperator == PendingOperator.Yank)
        {
            return Result(true, previousMode, registerOperation);
        }

        var newText = document.Text.Remove(start, end - start);
        var newCaret = _pendingOperator == PendingOperator.Change
            ? start
            : new VimTextBuffer(newText).NormalizeNormalOffset(start);
        if (_pendingOperator == PendingOperator.Change)
        {
            SetInsertEntry(newCaret, newCaret);
        }

        return Result(
            true,
            previousMode,
            registerOperation,
            new VimTextEditOperation(start, end - start, string.Empty, newCaret));
    }

    private VimCommandResult ApplyLinewiseOperator(
        VimDocumentSnapshot document,
        VimMode previousMode,
        int firstLine,
        int lastLine)
    {
        var buffer = new VimTextBuffer(document.Text);
        firstLine = Math.Clamp(firstLine, 0, buffer.LineCount - 1);
        lastLine = Math.Clamp(lastLine, firstLine, buffer.LineCount - 1);

        var selectedStart = buffer[firstLine].Start;
        var selectedEnd = buffer[lastLine].EndIncludingBreak;
        var selectedText = document.Text[selectedStart..selectedEnd];
        if (buffer[lastLine].NewLine.Length == 0)
        {
            selectedText += buffer.PreferredNewLine;
        }

        var register = new VimRegister(selectedText, VimRegisterKind.LineWise);
        SetRegister(register);
        var registerOperation = new VimSetRegisterOperation(register);

        if (_pendingOperator == PendingOperator.Yank)
        {
            return Result(true, previousMode, registerOperation);
        }

        if (_pendingOperator == PendingOperator.Change)
        {
            var replacement = buffer[lastLine].NewLine.Length > 0
                ? buffer.PreferredNewLine
                : string.Empty;
            SetInsertEntry(selectedStart, selectedStart);
            if (selectedEnd == selectedStart && replacement.Length == 0)
            {
                return Result(true, previousMode, registerOperation);
            }

            return Result(
                true,
                previousMode,
                registerOperation,
                new VimTextEditOperation(
                    selectedStart,
                    selectedEnd - selectedStart,
                    replacement,
                    selectedStart));
        }

        var editStart = selectedStart;
        var editEnd = selectedEnd;
        if (lastLine == buffer.LineCount - 1 &&
            buffer[lastLine].NewLine.Length == 0 &&
            firstLine > 0)
        {
            editStart = buffer[firstLine - 1].ContentEnd;
        }

        if (editEnd == editStart)
        {
            return Result(true, previousMode, registerOperation);
        }

        var newText = document.Text.Remove(editStart, editEnd - editStart);
        var resultBuffer = new VimTextBuffer(newText);
        var targetLine = Math.Min(firstLine, resultBuffer.LineCount - 1);
        var newCaret = resultBuffer.GetFirstNonBlankOffset(targetLine);
        return Result(
            true,
            previousMode,
            registerOperation,
            new VimTextEditOperation(editStart, editEnd - editStart, string.Empty, newCaret));
    }

    private VimCommandResult ExecuteAliasOperator(
        char command,
        VimDocumentSnapshot document,
        VimMode previousMode,
        int count)
    {
        _pendingOperator = command switch
        {
            'D' => PendingOperator.Delete,
            'C' => PendingOperator.Change,
            _ => PendingOperator.Yank
        };
        _operatorCount = 1;
        Mode = VimMode.OperatorPending;

        return command == 'Y'
            ? ExecuteLinewiseOperator(document, previousMode, count)
            : ExecuteOperatorMotion(
                document,
                previousMode,
                '$',
                count,
                countSpecified: count != 1,
                isGg: false);
    }

    private VimCommandResult ExecuteSubstitute(
        VimDocumentSnapshot document,
        VimMode previousMode,
        int count,
        bool linewise)
    {
        _pendingOperator = PendingOperator.Change;
        _operatorCount = 1;
        Mode = VimMode.OperatorPending;
        if (linewise)
        {
            return ExecuteLinewiseOperator(document, previousMode, count);
        }

        var buffer = new VimTextBuffer(document.Text);
        var start = buffer.NormalizeNormalOffset(document.CaretOffset);
        var line = buffer[buffer.GetLineIndex(start)];
        var end = Math.Min(line.ContentEnd, AddClamped(start, count));
        var result = ApplyCharacterwiseRange(document, previousMode, start, end);
        FinishOperator();
        return result with { Mode = Mode };
    }

    private VimCommandResult ExecuteTextObject(
        VimDocumentSnapshot document,
        VimMode previousMode,
        bool around,
        int count)
    {
        var range = GetWordTextObject(document, around, count);
        var result = ApplyCharacterwiseRange(document, previousMode, range.Start, range.End);
        FinishOperator();
        return result with { Mode = Mode };
    }

    private static TextRange GetWordTextObject(
        VimDocumentSnapshot document,
        bool around,
        int count)
    {
        var buffer = new VimTextBuffer(document.Text);
        var origin = buffer.NormalizeNormalOffset(document.CaretOffset);
        var line = buffer[buffer.GetLineIndex(origin)];
        if (line.Length == 0)
        {
            return new TextRange(line.Start, line.Start);
        }

        var position = origin;
        if (around && IsInlineWhiteSpace(document.Text[position]))
        {
            var next = position;
            while (next < line.ContentEnd && IsInlineWhiteSpace(document.Text[next]))
            {
                next++;
            }

            if (next < line.ContentEnd)
            {
                position = next;
            }
            else
            {
                var previous = position;
                while (previous > line.Start && IsInlineWhiteSpace(document.Text[previous]))
                {
                    previous--;
                }

                position = previous;
            }
        }

        var characterClass = GetCharacterClass(document.Text[position]);
        var start = position;
        var end = position + 1;
        while (start > line.Start && GetCharacterClass(document.Text[start - 1]) == characterClass)
        {
            start--;
        }

        while (end < line.ContentEnd && GetCharacterClass(document.Text[end]) == characterClass)
        {
            end++;
        }

        count = Math.Max(1, count);
        for (var iteration = 1; iteration < count; iteration++)
        {
            var next = end;
            while (next < line.ContentEnd && IsInlineWhiteSpace(document.Text[next]))
            {
                next++;
            }

            if (next >= line.ContentEnd)
            {
                break;
            }

            var nextClass = GetCharacterClass(document.Text[next]);
            next++;
            while (next < line.ContentEnd && GetCharacterClass(document.Text[next]) == nextClass)
            {
                next++;
            }

            end = next;
        }

        if (around)
        {
            var trailingEnd = end;
            while (trailingEnd < line.ContentEnd && IsInlineWhiteSpace(document.Text[trailingEnd]))
            {
                trailingEnd++;
            }

            if (trailingEnd > end)
            {
                end = trailingEnd;
            }
            else
            {
                while (start > line.Start && IsInlineWhiteSpace(document.Text[start - 1]))
                {
                    start--;
                }
            }
        }

        return new TextRange(start, end);
    }

    private VimCommandResult EnterVisual(
        VimDocumentSnapshot document,
        VimMode previousMode,
        bool linewise,
        int count)
    {
        var buffer = new VimTextBuffer(document.Text);
        _visualAnchor = buffer.NormalizeNormalOffset(document.CaretOffset);
        Mode = linewise ? VimMode.VisualLine : VimMode.Visual;
        count = Math.Max(1, count);

        var lineIndex = buffer.GetLineIndex(_visualAnchor);
        int target;
        if (linewise)
        {
            var targetLine = Math.Min(buffer.LineCount - 1, AddClamped(lineIndex, count - 1));
            target = buffer.GetNormalOffset(targetLine, buffer.GetColumn(_visualAnchor));
        }
        else
        {
            var line = buffer[lineIndex];
            var last = line.Length == 0 ? line.Start : line.ContentEnd - 1;
            target = Math.Min(last, AddClamped(_visualAnchor, count - 1));
        }

        var selection = CreateSelectionOperation(document, target);
        return target == document.CaretOffset
            ? Result(true, previousMode, selection)
            : Result(true, previousMode, new VimMoveCaretOperation(target), selection);
    }

    private VimCommandResult ExitVisual(VimMode previousMode)
    {
        Mode = VimMode.Normal;
        ResetPendingCommand();
        _preferredColumn = null;
        return Result(true, previousMode, new VimClearSelectionOperation());
    }

    private VimSetSelectionOperation CreateSelectionOperation(
        VimDocumentSnapshot document,
        int caretTarget)
    {
        var selection = GetVisualSelection(
            document,
            caretTarget,
            linewise: Mode == VimMode.VisualLine);
        return new VimSetSelectionOperation(selection.Start, selection.End - selection.Start);
    }

    private VisualSelection GetVisualSelection(
        VimDocumentSnapshot document,
        int caretTarget,
        bool linewise)
    {
        var buffer = new VimTextBuffer(document.Text);
        var anchor = buffer.NormalizeNormalOffset(_visualAnchor);
        var target = buffer.NormalizeNormalOffset(caretTarget);
        var anchorLine = buffer.GetLineIndex(anchor);
        var targetLine = buffer.GetLineIndex(target);
        var firstLine = Math.Min(anchorLine, targetLine);
        var lastLine = Math.Max(anchorLine, targetLine);

        if (linewise)
        {
            // Keep the visual highlight off the next line's first column. Linewise
            // operators use FirstLine/LastLine and still include line delimiters.
            return new VisualSelection(
                buffer[firstLine].Start,
                buffer[lastLine].ContentEnd,
                firstLine,
                lastLine);
        }

        var start = Math.Min(anchor, target);
        var end = document.Text.Length == 0
            ? 0
            : Math.Min(document.Text.Length, Math.Max(anchor, target) + 1);
        return new VisualSelection(start, end, firstLine, lastLine);
    }

    private VimCommandResult DeleteCharacters(
        VimDocumentSnapshot document,
        VimMode previousMode,
        int count)
    {
        var buffer = new VimTextBuffer(document.Text);
        var start = buffer.NormalizeNormalOffset(document.CaretOffset);
        var line = buffer[buffer.GetLineIndex(start)];
        var length = Math.Min(Math.Max(0, line.ContentEnd - start), count);
        if (length == 0)
        {
            return Result(true, previousMode);
        }

        var register = new VimRegister(document.Text.Substring(start, length), VimRegisterKind.CharacterWise);
        SetRegister(register);

        var newText = document.Text.Remove(start, length);
        var newCaret = new VimTextBuffer(newText).NormalizeNormalOffset(start);
        return Result(
            true,
            previousMode,
            new VimSetRegisterOperation(register),
            new VimTextEditOperation(start, length, string.Empty, newCaret));
    }

    private VimCommandResult Paste(
        VimDocumentSnapshot document,
        VimMode previousMode,
        bool after,
        int count)
    {
        if (Register.Text.Length == 0 || count <= 0)
        {
            return Result(true, previousMode);
        }

        return Register.Kind == VimRegisterKind.LineWise
            ? PasteLinewise(document, previousMode, after, count)
            : PasteCharacterwise(document, previousMode, after, count);
    }

    private VimCommandResult PasteCharacterwise(
        VimDocumentSnapshot document,
        VimMode previousMode,
        bool after,
        int count)
    {
        var repeated = RepeatText(Register.Text, count);
        if (repeated is null)
        {
            return Result(true, previousMode);
        }

        var buffer = new VimTextBuffer(document.Text);
        var caret = buffer.NormalizeNormalOffset(document.CaretOffset);
        var line = buffer[buffer.GetLineIndex(caret)];
        var insertion = after && line.Length > 0 ? caret + 1 : caret;
        var resultingText = document.Text.Insert(insertion, repeated);
        var newCaret = new VimTextBuffer(resultingText)
            .NormalizeNormalOffset(insertion + repeated.Length - 1);
        return Result(
            true,
            previousMode,
            new VimTextEditOperation(insertion, 0, repeated, newCaret));
    }

    private VimCommandResult PasteLinewise(
        VimDocumentSnapshot document,
        VimMode previousMode,
        bool after,
        int count)
    {
        var buffer = new VimTextBuffer(document.Text);
        var newLine = buffer.PreferredNewLine;
        var canonical = NormalizeLinewiseText(Register.Text, newLine);
        var repeated = RepeatText(canonical, count);
        if (repeated is null)
        {
            return Result(true, previousMode);
        }

        int insertion;
        int caretAnchor;
        string insertedText;

        if (document.Text.Length == 0)
        {
            insertion = 0;
            insertedText = TrimOneTrailingNewLine(repeated, newLine);
            caretAnchor = 0;
        }
        else
        {
            var line = buffer[buffer.GetLineIndex(document.CaretOffset)];
            if (!after)
            {
                insertion = line.Start;
                insertedText = repeated;
                caretAnchor = insertion;
            }
            else if (line.NewLine.Length > 0)
            {
                insertion = line.EndIncludingBreak;
                insertedText = repeated;
                caretAnchor = insertion;
            }
            else
            {
                insertion = line.ContentEnd;
                insertedText = newLine + TrimOneTrailingNewLine(repeated, newLine);
                caretAnchor = insertion + newLine.Length;
            }
        }

        if (insertedText.Length == 0)
        {
            return Result(true, previousMode);
        }

        var resultingText = document.Text.Insert(insertion, insertedText);
        var resultBuffer = new VimTextBuffer(resultingText);
        var newCaret = resultBuffer.GetFirstNonBlankOffset(resultBuffer.GetLineIndex(caretAnchor));
        return Result(
            true,
            previousMode,
            new VimTextEditOperation(insertion, 0, insertedText, newCaret));
    }

    private VimCommandResult EnterInsert(
        char command,
        VimDocumentSnapshot document,
        VimMode previousMode)
    {
        var buffer = new VimTextBuffer(document.Text);
        var caret = buffer.NormalizeNormalOffset(document.CaretOffset);
        var lineIndex = buffer.GetLineIndex(caret);
        var line = buffer[lineIndex];
        Mode = VimMode.Insert;

        if (command == 'o')
        {
            var newLine = buffer.PreferredNewLine;
            var insertion = line.NewLine.Length > 0 ? line.EndIncludingBreak : line.ContentEnd;
            var newCaret = line.NewLine.Length > 0 ? insertion : insertion + newLine.Length;
            SetInsertEntry(newCaret, newCaret);
            return Result(
                true,
                previousMode,
                new VimTextEditOperation(insertion, 0, newLine, newCaret));
        }

        if (command == 'O')
        {
            SetInsertEntry(line.Start, line.Start);
            return Result(
                true,
                previousMode,
                new VimTextEditOperation(line.Start, 0, buffer.PreferredNewLine, line.Start));
        }

        var target = command switch
        {
            'a' => line.Length == 0 ? line.Start : Math.Min(line.ContentEnd, caret + 1),
            'I' => buffer.GetFirstNonBlankOffset(lineIndex),
            'A' => line.ContentEnd,
            _ => caret
        };
        var fallback = command switch
        {
            'I' => target,
            'A' => buffer.NormalizeNormalOffset(target),
            _ => caret
        };
        SetInsertEntry(target, fallback);

        return Result(true, previousMode, new VimMoveCaretOperation(target));
    }

    private bool TryGetMotion(
        VimDocumentSnapshot document,
        char character,
        int count,
        bool countSpecified,
        bool isGg,
        out Motion motion)
    {
        var buffer = new VimTextBuffer(document.Text);
        var origin = buffer.NormalizeNormalOffset(document.CaretOffset);
        var lineIndex = buffer.GetLineIndex(origin);
        var line = buffer[lineIndex];
        count = Math.Max(1, count);

        if (isGg)
        {
            var targetLine = countSpecified ? Math.Min(buffer.LineCount - 1, count - 1) : 0;
            var target = buffer.GetFirstNonBlankOffset(targetLine);
            motion = new Motion(target, target, targetLine, MotionShape.LineWise, false, null);
            return true;
        }

        switch (character)
        {
            case 'h':
            {
                var target = Math.Max(line.Start, origin - count);
                motion = CharacterMotion(target, MotionShape.CharacterExclusive, lineIndex);
                return true;
            }
            case 'l':
            {
                var last = line.Length == 0 ? line.Start : line.ContentEnd - 1;
                var target = Math.Min(last, AddClamped(origin, count));
                motion = CharacterMotion(target, MotionShape.CharacterExclusive, lineIndex);
                return true;
            }
            case 'j':
            case 'k':
            {
                var preferredColumn = _preferredColumn ?? buffer.GetColumn(origin);
                var targetLine = character == 'j'
                    ? Math.Min(buffer.LineCount - 1, AddClamped(lineIndex, count))
                    : Math.Max(0, lineIndex - Math.Min(lineIndex, count));
                var target = buffer.GetNormalOffset(targetLine, preferredColumn);
                motion = new Motion(
                    target,
                    target,
                    targetLine,
                    MotionShape.LineWise,
                    true,
                    preferredColumn);
                return true;
            }
            case '0':
            {
                motion = CharacterMotion(line.Start, MotionShape.CharacterExclusive, lineIndex);
                return true;
            }
            case '^':
            {
                motion = CharacterMotion(
                    buffer.GetFirstNonBlankOffset(lineIndex),
                    MotionShape.CharacterExclusive,
                    lineIndex);
                return true;
            }
            case '$':
            {
                var targetLine = Math.Min(buffer.LineCount - 1, AddClamped(lineIndex, count - 1));
                var targetLineInfo = buffer[targetLine];
                var target = targetLineInfo.Length == 0
                    ? targetLineInfo.Start
                    : targetLineInfo.ContentEnd - 1;
                motion = CharacterMotion(target, MotionShape.CharacterInclusive, targetLine);
                return true;
            }
            case 'G':
            {
                var targetLine = countSpecified ? Math.Min(buffer.LineCount - 1, count - 1) : buffer.LineCount - 1;
                var target = buffer.GetFirstNonBlankOffset(targetLine);
                motion = new Motion(target, target, targetLine, MotionShape.LineWise, false, null);
                return true;
            }
            case 'w':
            {
                var rawTarget = MoveWordForward(document.Text, origin, count);
                motion = new Motion(
                    buffer.NormalizeNormalOffset(rawTarget),
                    rawTarget,
                    buffer.GetLineIndex(rawTarget),
                    MotionShape.CharacterExclusive,
                    false,
                    null);
                return true;
            }
            case 'b':
            {
                var target = MoveWordBackward(document.Text, origin, count);
                motion = CharacterMotion(target, MotionShape.CharacterExclusive, buffer.GetLineIndex(target));
                return true;
            }
            case 'e':
            {
                var target = MoveWordEnd(document.Text, origin, count);
                motion = CharacterMotion(target, MotionShape.CharacterInclusive, buffer.GetLineIndex(target));
                return true;
            }
            default:
                motion = default;
                return false;
        }
    }

    private static Motion CharacterMotion(int target, MotionShape shape, int targetLine)
        => new(target, target, targetLine, shape, false, null);

    private static int MoveWordForward(string text, int offset, int count)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var position = Math.Clamp(offset, 0, text.Length);
        for (var iteration = 0; iteration < count; iteration++)
        {
            var previous = position;
            if (position < text.Length && GetCharacterClass(text[position]) != CharacterClass.WhiteSpace)
            {
                var characterClass = GetCharacterClass(text[position]);
                while (position < text.Length && GetCharacterClass(text[position]) == characterClass)
                {
                    position++;
                }
            }

            while (position < text.Length && GetCharacterClass(text[position]) == CharacterClass.WhiteSpace)
            {
                position++;
            }

            if (position == previous || position == text.Length)
            {
                break;
            }
        }

        return position;
    }

    private static int MoveWordBackward(string text, int offset, int count)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var position = Math.Clamp(offset, 0, text.Length - 1);
        for (var iteration = 0; iteration < count; iteration++)
        {
            if (position > 0)
            {
                position--;
            }

            while (position > 0 && GetCharacterClass(text[position]) == CharacterClass.WhiteSpace)
            {
                position--;
            }

            var characterClass = GetCharacterClass(text[position]);
            while (position > 0 && GetCharacterClass(text[position - 1]) == characterClass)
            {
                position--;
            }

            if (position == 0)
            {
                break;
            }
        }

        return position;
    }

    private static int MoveWordEnd(string text, int offset, int count)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var position = Math.Clamp(offset, 0, text.Length - 1);
        for (var iteration = 0; iteration < Math.Max(1, count); iteration++)
        {
            var currentClass = GetCharacterClass(text[position]);
            var isAtCurrentEnd = currentClass == CharacterClass.WhiteSpace
                || position + 1 >= text.Length
                || GetCharacterClass(text[position + 1]) != currentClass;

            if (isAtCurrentEnd)
            {
                var next = position + 1;
                while (next < text.Length && GetCharacterClass(text[next]) == CharacterClass.WhiteSpace)
                {
                    next++;
                }

                if (next >= text.Length)
                {
                    break;
                }

                position = next;
            }

            var targetClass = GetCharacterClass(text[position]);
            while (position + 1 < text.Length && GetCharacterClass(text[position + 1]) == targetClass)
            {
                position++;
            }
        }

        return position;
    }

    private static CharacterClass GetCharacterClass(char character)
    {
        if (char.IsWhiteSpace(character))
        {
            return CharacterClass.WhiteSpace;
        }

        return char.IsLetterOrDigit(character) || character == '_'
            ? CharacterClass.Keyword
            : CharacterClass.Punctuation;
    }

    private void SetRegister(VimRegister register)
    {
        Register = register;
    }

    private void SetInsertEntry(int entryOffset, int fallbackOffset)
    {
        _insertEntryOffset = entryOffset;
        _insertFallbackOffset = fallbackOffset;
    }

    private int CombineOperatorCount(int motionCount)
        => VimCountParser.Multiply(_operatorCount, motionCount);

    private void CancelOperator()
    {
        Mode = VimMode.Normal;
        ResetPendingCommand();
        _preferredColumn = null;
    }

    private void FinishOperator()
    {
        Mode = _pendingOperator == PendingOperator.Change
            ? VimMode.Insert
            : VimMode.Normal;
        ResetPendingCommand();
        _preferredColumn = null;
    }

    private void ResetPendingCommand()
    {
        _count.Reset();
        _pendingOperator = PendingOperator.None;
        _operatorCount = 1;
        _awaitingSecondG = false;
        _pendingTextObjectPrefix = null;
    }

    private static PendingOperator GetOperator(char character)
        => character switch
        {
            'd' => PendingOperator.Delete,
            'y' => PendingOperator.Yank,
            'c' => PendingOperator.Change,
            _ => PendingOperator.None
        };

    private static bool IsInlineWhiteSpace(char character)
        => character is ' ' or '\t';

    private VimCommandResult Result(
        bool handled,
        VimMode previousMode,
        params VimOperation[] operations)
        => new(handled, previousMode, Mode, operations);

    private static int AddClamped(int value, int increment)
        => increment > int.MaxValue - value ? int.MaxValue : value + increment;

    private static string? RepeatText(string text, int count)
    {
        if (count <= 0 || text.Length == 0)
        {
            return string.Empty;
        }

        if ((long)text.Length * count > int.MaxValue)
        {
            return null;
        }

        var builder = new StringBuilder(text.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(text);
        }

        return builder.ToString();
    }

    private static string NormalizeLinewiseText(string text, string newLine)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = normalized.TrimEnd('\n');
        normalized = normalized.Replace("\n", newLine, StringComparison.Ordinal);
        return normalized + newLine;
    }

    private static string TrimOneTrailingNewLine(string text, string newLine)
        => text.EndsWith(newLine, StringComparison.Ordinal) ? text[..^newLine.Length] : text;

    private enum PendingOperator
    {
        None,
        Delete,
        Yank,
        Change
    }

    private enum MotionShape
    {
        CharacterExclusive,
        CharacterInclusive,
        LineWise
    }

    private enum CharacterClass
    {
        WhiteSpace,
        Keyword,
        Punctuation
    }

    private readonly record struct TextRange(int Start, int End);

    private readonly record struct VisualSelection(
        int Start,
        int End,
        int FirstLine,
        int LastLine);

    private readonly record struct Motion(
        int CaretTarget,
        int RawTarget,
        int TargetLine,
        MotionShape Shape,
        bool PreservesPreferredColumn,
        int? PreferredColumn);
}
