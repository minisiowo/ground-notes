using Avalonia.Media;
using AvaloniaEdit.Document;

namespace AvaloniaEdit.Rendering;

/// <summary>
/// Provides an optional text-wrapping override for a document line.
/// </summary>
public interface IVisualLineWrappingProvider
{
    /// <summary>
    /// Returns the text-wrapping mode for the specified line, or <c>null</c>
    /// to use the text view's default wrapping mode.
    /// </summary>
    TextWrapping? GetTextWrapping(TextView textView, DocumentLine documentLine);
}
