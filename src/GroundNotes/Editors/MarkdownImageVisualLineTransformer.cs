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
        var preview = _previewProvider.GetPreview(context.Document, context.VisualLine.FirstDocumentLine);
        if (preview is null)
        {
            return;
        }

        context.VisualLine.SetAdditionalVisualHeight(preview.Value.Height + VerticalSpacing * 2);
    }
}
