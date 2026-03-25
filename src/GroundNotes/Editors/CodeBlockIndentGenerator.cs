using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class CodeBlockIndentGenerator : VisualLineElementGenerator
{
    private readonly MarkdownColorizingTransformer _colorizer;

    public CodeBlockIndentGenerator(MarkdownColorizingTransformer colorizer)
    {
        _colorizer = colorizer;
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var document = CurrentContext.Document;
        var line = document.GetLineByOffset(startOffset);

        if (startOffset != line.Offset)
        {
            return -1;
        }

        return _colorizer.QueryIsFencedCodeLine(document, line.LineNumber)
            ? startOffset
            : -1;
    }

    public override VisualLineElement ConstructElement(int offset)
    {
        return new SpacerElement();
    }

    private sealed class SpacerElement : FormattedTextElement
    {
        public SpacerElement()
            : base("  ", 0)
        {
        }

        public override bool IsWhitespace(int visualColumn) => true;
    }
}
