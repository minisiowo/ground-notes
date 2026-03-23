using Avalonia.Media;
using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownStyleSpanBufferTests
{
    [Fact]
    public void Add_MergesAdjacentSpansWithSameStyle()
    {
        var buffer = new MarkdownStyleSpanBuffer();
        var brush = new SolidColorBrush(Colors.Red);

        buffer.Add(0, 3, brush, fontWeight: FontWeight.SemiBold);
        buffer.Add(3, 6, brush, fontWeight: FontWeight.SemiBold);

        var spans = buffer.Snapshot();
        var span = Assert.Single(spans);
        Assert.Equal(0, span.StartOffset);
        Assert.Equal(6, span.EndOffset);
    }

    [Fact]
    public void Add_DoesNotMergeSpansWithDifferentStyle()
    {
        var buffer = new MarkdownStyleSpanBuffer();

        buffer.Add(0, 3, new SolidColorBrush(Colors.Red));
        buffer.Add(3, 6, new SolidColorBrush(Colors.Blue));

        Assert.Equal(2, buffer.Snapshot().Count);
    }
}
