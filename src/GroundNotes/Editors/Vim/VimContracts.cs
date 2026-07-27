using System;
using System.Collections.Generic;

namespace GroundNotes.Editors.Vim;

public enum VimMode
{
    Normal,
    Insert,
    OperatorPending,
    Visual,
    VisualLine
}

public enum VimKey
{
    Printable,
    Escape,
    Enter,
    Tab,
    Backspace,
    Delete,
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    CtrlR
}

public readonly record struct VimInput
{
    private VimInput(VimKey key, char character)
    {
        Key = key;
        Character = character;
    }

    public VimKey Key { get; }

    public char Character { get; }

    public bool IsPrintable => Key == VimKey.Printable;

    public static VimInput Printable(char character)
    {
        if (char.IsControl(character))
        {
            throw new ArgumentOutOfRangeException(nameof(character), "A printable Vim input cannot be a control character.");
        }

        return new VimInput(VimKey.Printable, character);
    }

    public static VimInput Special(VimKey key)
    {
        if (key == VimKey.Printable)
        {
            throw new ArgumentException("Use Printable to construct character input.", nameof(key));
        }

        return new VimInput(key, default);
    }
}

public readonly record struct VimDocumentSnapshot
{
    public VimDocumentSnapshot(string text, int caretOffset)
    {
        ArgumentNullException.ThrowIfNull(text);

        if ((uint)caretOffset > (uint)text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(caretOffset));
        }

        Text = text;
        CaretOffset = caretOffset;
    }

    public string Text { get; }

    public int CaretOffset { get; }
}

public enum VimRegisterKind
{
    CharacterWise,
    LineWise
}

public sealed record VimRegister(string Text, VimRegisterKind Kind)
{
    public static VimRegister Empty { get; } = new(string.Empty, VimRegisterKind.CharacterWise);
}

public abstract record VimOperation;

public sealed record VimMoveCaretOperation(int Offset) : VimOperation;

public sealed record VimSetSelectionOperation(int Start, int Length) : VimOperation;

public sealed record VimClearSelectionOperation : VimOperation;

public sealed record VimTextEditOperation(
    int Start,
    int Length,
    string NewText,
    int NewCaretOffset) : VimOperation;

public sealed record VimSetRegisterOperation(VimRegister Register) : VimOperation;

public enum VimHistoryAction
{
    Undo,
    Redo
}

public sealed record VimHistoryOperation(VimHistoryAction Action, int Count = 1) : VimOperation;

public sealed record VimCommandResult(
    bool IsHandled,
    VimMode PreviousMode,
    VimMode Mode,
    IReadOnlyList<VimOperation> Operations)
{
    public bool ModeChanged => PreviousMode != Mode;
}
