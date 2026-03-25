using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class NotePreviewFormatterTests
{
    [Fact]
    public void Build_ReturnsEmptyForBlankBody()
    {
        Assert.Equal(string.Empty, NotePreviewFormatter.Build(""));
        Assert.Equal(string.Empty, NotePreviewFormatter.Build("   "));
    }

    [Fact]
    public void Build_StripsMarkdownHeadings()
    {
        var result = NotePreviewFormatter.Build("# Hello World");
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Build_StripsEmphasis()
    {
        Assert.Equal("bold text", NotePreviewFormatter.Build("**bold text**"));
        Assert.Equal("italic", NotePreviewFormatter.Build("*italic*"));
    }

    [Fact]
    public void Build_StripsInlineCode()
    {
        Assert.Equal("code", NotePreviewFormatter.Build("`code`"));
    }

    [Fact]
    public void Build_StripsLinks()
    {
        Assert.Equal("click here", NotePreviewFormatter.Build("[click here](https://example.com)"));
    }

    [Fact]
    public void Build_StripsImages()
    {
        Assert.Equal("alt text", NotePreviewFormatter.Build("![alt text](image.png)"));
    }

    [Fact]
    public void Build_StripsFenceMarkers()
    {
        var input = "```csharp\nvar x = 1;\n```";
        var result = NotePreviewFormatter.Build(input);
        Assert.Contains("var x = 1;", result);
        Assert.DoesNotContain("```", result);
    }

    [Fact]
    public void Build_StripsHorizontalRules()
    {
        var result = NotePreviewFormatter.Build("above\n\n---\n\nbelow");
        Assert.DoesNotContain("---", result);
        Assert.Contains("above", result);
        Assert.Contains("below", result);
    }

    [Fact]
    public void Build_TruncatesAtMaxLength()
    {
        var longText = new string('a', 200);
        var result = NotePreviewFormatter.Build(longText, maxLength: 10);
        Assert.Equal("aaaaaaaaaa...", result);
    }

    [Fact]
    public void Build_CollapsesWhitespace()
    {
        Assert.Equal("a b c", NotePreviewFormatter.Build("a   b\n\nc"));
    }
}
