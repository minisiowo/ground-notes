using System;
using Avalonia.Media.TextFormatting;

namespace AvaloniaEdit.Rendering;

internal sealed class VisualIndentationElement : VisualLineElement
{
    private readonly string _indentationText;

    public VisualIndentationElement(int visualLength)
        : base(visualLength, 0)
    {
        if (visualLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(visualLength), visualLength, "Value must be positive.");
        _indentationText = new string(' ', visualLength);
    }

    public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
    {
        var localStart = startVisualColumn - VisualColumn;
        if (localStart < 0)
            localStart = 0;
        if (localStart > _indentationText.Length)
            localStart = _indentationText.Length;
        return new TextCharacters(_indentationText.AsMemory(localStart), TextRunProperties);
    }

    public override ReadOnlyMemory<char> GetPrecedingText(int visualColumnLimit, ITextRunConstructionContext context)
    {
        var length = visualColumnLimit - VisualColumn;
        if (length < 0)
            length = 0;
        if (length > _indentationText.Length)
            length = _indentationText.Length;
        return _indentationText.AsMemory(0, length);
    }

    public override bool IsWhitespace(int visualColumn)
    {
        return visualColumn >= VisualColumn && visualColumn < VisualColumn + VisualLength;
    }
}
