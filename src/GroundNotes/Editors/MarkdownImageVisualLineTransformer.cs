using AvaloniaEdit.Rendering;

namespace GroundNotes.Editors;

internal sealed class MarkdownImageVisualLineTransformer : IVisualLineTransformer
{
    private const double VerticalSpacing = 6;

    private readonly MarkdownImagePreviewProvider _previewProvider;

    public MarkdownImageVisualLineTransformer(MarkdownImagePreviewProvider previewProvider)
    {
        _previewProvider = previewProvider;
    }

    public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
    {
        var previews = _previewProvider.GetPreview(context.Document, context.VisualLine.FirstDocumentLine);
        if (previews.Count == 0)
        {
            return;
        }

        var maxHeight = 0.0;
        foreach (var preview in previews)
        {
            if (preview.Height > maxHeight)
            {
                maxHeight = preview.Height;
            }
        }

        context.VisualLine.SetAdditionalVisualHeight(maxHeight + VerticalSpacing * 2);
    }
}
