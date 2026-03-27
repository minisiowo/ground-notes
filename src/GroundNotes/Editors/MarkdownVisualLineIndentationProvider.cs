using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class MarkdownVisualLineIndentationProvider : IVisualLineIndentationProvider
{
    private readonly MarkdownColorizingTransformer _colorizer;
    private readonly ILeadingIndentationRule[] _leadingIndentationRules;
    private readonly IContinuationIndentationRule[] _continuationIndentationRules;

    public MarkdownVisualLineIndentationProvider(MarkdownColorizingTransformer colorizer)
    {
        ArgumentNullException.ThrowIfNull(colorizer);

        _colorizer = colorizer;
        _leadingIndentationRules = [new FencedCodeIndentationRule(colorizer, 2), new InheritedListContinuationIndentationRule(colorizer)];
        _continuationIndentationRules = [new ListContinuationIndentationRule(colorizer)];
    }

    public int GetVisualIndentationColumns(TextView textView, DocumentLine documentLine)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(documentLine);

        if (textView.Document is null)
        {
            return 0;
        }

        foreach (var rule in _leadingIndentationRules)
        {
            var indentationColumns = rule.GetVisualIndentationColumns(textView, documentLine);
            if (indentationColumns > 0)
            {
                return indentationColumns;
            }
        }

        return 0;
    }

    public int? GetWrappedLineContinuationStartColumn(TextView textView, DocumentLine documentLine)
    {
        ArgumentNullException.ThrowIfNull(textView);
        ArgumentNullException.ThrowIfNull(documentLine);

        if (textView.Document is null || _colorizer.QueryIsFencedCodeLine(textView.Document, documentLine.LineNumber))
        {
            return null;
        }

        foreach (var rule in _continuationIndentationRules)
        {
            var continuationColumn = rule.GetWrappedLineContinuationStartColumn(textView, documentLine);
            if (continuationColumn is not null)
            {
                return continuationColumn;
            }
        }

        return null;
    }

    private interface ILeadingIndentationRule
    {
        int GetVisualIndentationColumns(TextView textView, DocumentLine documentLine);
    }

    private interface IContinuationIndentationRule
    {
        int? GetWrappedLineContinuationStartColumn(TextView textView, DocumentLine documentLine);
    }

    private sealed class FencedCodeIndentationRule : ILeadingIndentationRule
    {
        private readonly MarkdownColorizingTransformer _colorizer;
        private readonly int _indentationColumns;

        public FencedCodeIndentationRule(MarkdownColorizingTransformer colorizer, int indentationColumns)
        {
            _colorizer = colorizer;
            _indentationColumns = indentationColumns;
        }

        public int GetVisualIndentationColumns(TextView textView, DocumentLine documentLine)
        {
            return _colorizer.QueryIsFencedCodeLine(textView.Document, documentLine.LineNumber)
                ? _indentationColumns
                : 0;
        }
    }

    private sealed class ListContinuationIndentationRule : IContinuationIndentationRule
    {
        private readonly MarkdownColorizingTransformer _colorizer;

        public ListContinuationIndentationRule(MarkdownColorizingTransformer colorizer)
        {
            _colorizer = colorizer;
        }

        public int? GetWrappedLineContinuationStartColumn(TextView textView, DocumentLine documentLine)
        {
            return _colorizer.QueryWrappedLineContinuationStartColumn(textView.Document, documentLine.LineNumber);
        }
    }

    private sealed class InheritedListContinuationIndentationRule : ILeadingIndentationRule
    {
        private readonly MarkdownColorizingTransformer _colorizer;

        public InheritedListContinuationIndentationRule(MarkdownColorizingTransformer colorizer)
        {
            _colorizer = colorizer;
        }

        public int GetVisualIndentationColumns(TextView textView, DocumentLine documentLine)
        {
            var inheritedStartColumn = _colorizer.QueryInheritedListContinuationStartColumn(textView.Document, documentLine.LineNumber);
            if (inheritedStartColumn is null or <= 0)
            {
                return 0;
            }

            var lineText = textView.Document.GetText(documentLine.Offset, documentLine.Length);
            var leadingWhitespaceColumns = CountLeadingWhitespaceColumns(lineText, textView.Options.IndentationSize);
            return Math.Max(inheritedStartColumn.Value - leadingWhitespaceColumns, 0);
        }

        private static int CountLeadingWhitespaceColumns(string lineText, int indentationSize)
        {
            var columns = 0;

            foreach (var ch in lineText)
            {
                if (ch == ' ')
                {
                    columns++;
                    continue;
                }

                if (ch == '\t')
                {
                    columns += Math.Max(indentationSize, 1);
                    continue;
                }

                break;
            }

            return columns;
        }
    }
}
