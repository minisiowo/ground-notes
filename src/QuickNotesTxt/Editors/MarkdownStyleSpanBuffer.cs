using Avalonia.Media;
using AvaloniaEdit.Rendering;

namespace QuickNotesTxt.Editors;

internal sealed class MarkdownStyleSpanBuffer
{
    private readonly List<MarkdownStyleSpan> _spans = [];

    public void Clear() => _spans.Clear();

    public void Add(int startOffset, int endOffset, IBrush? foregroundBrush, FontWeight? fontWeight = null, FontStyle? fontStyle = null, IBrush? backgroundBrush = null, FontFamily? fontFamily = null, TextDecorationCollection? textDecorations = null, Typeface? typeface = null)
    {
        if (endOffset <= startOffset)
        {
            return;
        }

        var span = new MarkdownStyleSpan(startOffset, endOffset, foregroundBrush, fontWeight, fontStyle, backgroundBrush, fontFamily, textDecorations, typeface);
        if (_spans.Count > 0 && _spans[^1].CanMergeWith(span))
        {
            _spans[^1] = _spans[^1] with { EndOffset = span.EndOffset };
            return;
        }

        _spans.Add(span);
    }

    public void Apply(Action<MarkdownStyleSpan> apply)
    {
        foreach (var span in _spans)
        {
            apply(span);
        }
    }

    public IReadOnlyList<MarkdownStyleSpan> Snapshot() => _spans.ToArray();
}

internal readonly record struct MarkdownStyleSpan(
    int StartOffset,
    int EndOffset,
    IBrush? ForegroundBrush,
    FontWeight? FontWeight,
    FontStyle? FontStyle,
    IBrush? BackgroundBrush,
    FontFamily? FontFamily,
    TextDecorationCollection? TextDecorations,
    Typeface? Typeface)
{
    public bool CanMergeWith(MarkdownStyleSpan other)
    {
        return EndOffset == other.StartOffset
            && EqualityComparer<IBrush?>.Default.Equals(ForegroundBrush, other.ForegroundBrush)
            && FontWeight == other.FontWeight
            && FontStyle == other.FontStyle
            && EqualityComparer<IBrush?>.Default.Equals(BackgroundBrush, other.BackgroundBrush)
            && EqualityComparer<FontFamily?>.Default.Equals(FontFamily, other.FontFamily)
            && EqualityComparer<TextDecorationCollection?>.Default.Equals(TextDecorations, other.TextDecorations)
            && EqualityComparer<Typeface?>.Default.Equals(Typeface, other.Typeface);
    }
}
