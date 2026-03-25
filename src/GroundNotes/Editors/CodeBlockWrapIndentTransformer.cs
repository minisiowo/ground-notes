using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class CodeBlockWrapIndentTransformer : IVisualLineTransformer
{
    private static readonly MethodInfo? TextRunPropertiesSetter = typeof(VisualLineElement)
        .GetMethod("set_TextRunProperties", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? LastAvailableSizeField = typeof(TextView)
        .GetField("_lastAvailableSize", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly MarkdownColorizingTransformer _colorizer;

    public CodeBlockWrapIndentTransformer(MarkdownColorizingTransformer colorizer)
    {
        _colorizer = colorizer;
    }

    public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
    {
        var visualLine = context.VisualLine;
        var lineNumber = visualLine.FirstDocumentLine.LineNumber;
        if (!_colorizer.QueryIsFencedCodeLine(context.Document, lineNumber))
        {
            return;
        }

        if (elements.Count < 2 || elements[0] is not FormattedTextElement)
        {
            return;
        }

        var contentElement = elements[1];
        if (contentElement.DocumentLength <= 0)
        {
            return;
        }

        var availableWidth = GetAvailableWidth(context.TextView);
        var indentWidth = GetSpacerWidth(context.GlobalTextRunProperties);
        if (availableWidth <= indentWidth)
        {
            return;
        }

        var content = context.Document.GetText(visualLine.FirstDocumentLine.Offset, contentElement.DocumentLength);
        var segmentLengths = SplitIntoWrappedSegments(content, contentElement.TextRunProperties, availableWidth - indentWidth);
        if (segmentLengths.Count <= 1)
        {
            return;
        }

        var replacement = new List<VisualLineElement>(segmentLengths.Count * 2 - 1);
        var consumed = 0;
        foreach (var segmentLength in segmentLengths)
        {
            if (replacement.Count > 0)
            {
                replacement.Add(CloneSpacerElement(elements[0]));
            }

            var segmentText = content.Substring(consumed, segmentLength);
            var segment = new DocumentTextSegmentElement(segmentText, segmentLength);
            CopyStyle(contentElement, segment);
            replacement.Add(segment);
            consumed += segmentLength;
        }

        visualLine.ReplaceElement(1, 1, [.. replacement]);
    }

    private static double GetSpacerWidth(TextRunProperties properties)
    {
        var formattedText = new FormattedText(
            "  ",
            properties.CultureInfo ?? System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            properties.Typeface,
            properties.FontRenderingEmSize,
            properties.ForegroundBrush);

        return formattedText.WidthIncludingTrailingWhitespace;
    }

    private static List<int> SplitIntoWrappedSegments(string content, TextRunProperties properties, double maxWidth)
    {
        var segments = new List<int>();
        var source = new SimpleTextSource(content, properties);
        var formatter = TextFormatter.Current
            ?? throw new InvalidOperationException("TextFormatter.Current is unavailable.");
        var paragraphProperties = new GenericTextParagraphProperties(
            FlowDirection.LeftToRight,
            TextAlignment.Left,
            firstLineInParagraph: false,
            alwaysCollapsible: false,
            properties,
            TextWrapping.Wrap,
            lineHeight: 0.0,
            indent: 0.0,
            letterSpacing: 0.0);

        var offset = 0;
        TextLineBreak? previousLineBreak = null;
        while (offset < content.Length)
        {
            var textLine = formatter.FormatLine(source, offset, maxWidth, paragraphProperties, previousLineBreak)
                ?? throw new InvalidOperationException("TextFormatter returned null.");
            var remaining = content.Length - offset;
            var segmentLength = Math.Max(1, Math.Min(remaining, textLine.Length));
            segments.Add(segmentLength);
            offset += segmentLength;
            previousLineBreak = textLine.TextLineBreak;
        }

        return segments;
    }

    private static double GetAvailableWidth(TextView textView)
    {
        if (LastAvailableSizeField?.GetValue(textView) is Size size && size.Width > 0)
        {
            return size.Width;
        }

        return textView.Bounds.Width;
    }

    private static VisualLineElement CloneSpacerElement(VisualLineElement original)
    {
        var spacer = new SpacerCloneElement();
        CopyStyle(original, spacer);
        return spacer;
    }

    private static void CopyStyle(VisualLineElement source, VisualLineElement target)
    {
        TextRunPropertiesSetter?.Invoke(target, [source.TextRunProperties]);
        target.BackgroundBrush = source.BackgroundBrush;
    }

    private sealed class SpacerCloneElement : FormattedTextElement
    {
        public SpacerCloneElement()
            : base("  ", 0)
        {
        }

        public override bool IsWhitespace(int visualColumn) => true;
    }

    private sealed class DocumentTextSegmentElement : VisualLineElement
    {
        private readonly string _text;

        public DocumentTextSegmentElement(string text, int documentLength)
            : base(text.Length, documentLength)
        {
            _text = text;
        }

        public override TextRun CreateTextRun(int visualColumn, ITextRunConstructionContext context)
        {
            var localVisualColumn = Math.Clamp(visualColumn - VisualColumn, 0, _text.Length);
            return new TextCharacters(_text.AsMemory(localVisualColumn), TextRunProperties);
        }

        public override ReadOnlyMemory<char> GetPrecedingText(int visualColumn, ITextRunConstructionContext context)
        {
            var localLength = Math.Clamp(visualColumn - VisualColumn, 0, _text.Length);
            return _text.AsMemory(0, localLength);
        }

        public override int GetVisualColumn(int relativeTextOffset)
        {
            return VisualColumn + Math.Clamp(relativeTextOffset - RelativeTextOffset, 0, DocumentLength);
        }

        public override int GetRelativeOffset(int visualColumn)
        {
            return RelativeTextOffset + Math.Clamp(visualColumn - VisualColumn, 0, DocumentLength);
        }

        public override bool IsWhitespace(int visualColumn)
        {
            var localVisualColumn = visualColumn - VisualColumn;
            return localVisualColumn >= 0
                && localVisualColumn < _text.Length
                && char.IsWhiteSpace(_text[localVisualColumn]);
        }
    }

    private sealed class SimpleTextSource : ITextSource
    {
        private readonly string _text;
        private readonly TextRunProperties _properties;

        public SimpleTextSource(string text, TextRunProperties properties)
        {
            _text = text;
            _properties = properties;
        }

        public TextRun GetTextRun(int textSourceCharacterIndex)
        {
            if (textSourceCharacterIndex >= _text.Length)
            {
                return new TextEndOfParagraph(1);
            }

            return new TextCharacters(_text.AsMemory(textSourceCharacterIndex), _properties);
        }

        public ReadOnlySpan<char> GetText(int textSourceCharacterIndex)
        {
            return _text.AsSpan(textSourceCharacterIndex);
        }

        public int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex)
        {
            return textSourceCharacterIndex;
        }
    }
}
