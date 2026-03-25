using AvaloniaEdit.Document;

namespace AvaloniaEdit.Rendering;

/// <summary>
/// Provides a visual-only leading indentation for a document line.
/// </summary>
/// <remarks>
/// The returned indentation does not modify the document text. It is applied before wrapped
/// continuation indentation is calculated, so the same inset is inherited by wrapped rows.
/// </remarks>
public interface IVisualLineIndentationProvider
{
    /// <summary>
    /// Returns the number of leading visual columns to add before the document text for the specified line.
    /// Return 0 to apply no extra indentation.
    /// </summary>
    int GetVisualIndentationColumns(TextView textView, DocumentLine documentLine);
}
