using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class MarkdownTableWrappingProvider : IVisualLineWrappingProvider
{
    private readonly MarkdownTablePresentationIndex _tableIndex;

    public MarkdownTableWrappingProvider(MarkdownTablePresentationIndex tableIndex)
    {
        _tableIndex = tableIndex;
    }

    public TextWrapping? GetTextWrapping(TextView textView, DocumentLine documentLine)
    {
        var document = textView.Document;
        if (document is null)
        {
            return null;
        }

        return _tableIndex.GetTableForLine(document, documentLine.LineNumber) is not null
            ? TextWrapping.NoWrap
            : null;
    }
}
