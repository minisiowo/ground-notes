using AvaloniaEdit.Document;
using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownCodeBlockCopyHelperTests
{
    [Fact]
    public void TryResolve_CopiesOnlyContentBetweenFences()
    {
        var document = new TextDocument("before\n```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```\nafter");

        var resolved = MarkdownCodeBlockCopyHelper.TryResolve(document, 3, out var info);

        Assert.True(resolved);
        Assert.Equal(2, info.StartLineNumber);
        Assert.Equal(5, info.EndLineNumber);
        Assert.Equal("var x = 1;\nConsole.WriteLine(x);", info.Text);
    }

    [Fact]
    public void TryResolve_PreservesBlankLinesAndIndentation()
    {
        var document = new TextDocument("```\n  first\n\n    second\n```\n");

        var resolved = MarkdownCodeBlockCopyHelper.TryResolve(document, 4, out var info);

        Assert.True(resolved);
        Assert.Equal("  first\n\n    second", info.Text);
    }

    [Fact]
    public void TryResolve_SupportsTildeFences()
    {
        var document = new TextDocument("~~~text\nhello\n~~~");

        var resolved = MarkdownCodeBlockCopyHelper.TryResolve(document, 2, out var info);

        Assert.True(resolved);
        Assert.Equal("hello", info.Text);
    }

    [Fact]
    public void TryResolve_CopiesIncompleteFenceToDocumentEnd()
    {
        var document = new TextDocument("intro\n```\nline one\nline two");

        var resolved = MarkdownCodeBlockCopyHelper.TryResolve(document, 4, out var info);

        Assert.True(resolved);
        Assert.Equal(2, info.StartLineNumber);
        Assert.Equal(4, info.EndLineNumber);
        Assert.Equal("line one\nline two", info.Text);
    }

    [Fact]
    public void TryResolve_ReturnsFalseForEmptyBlock()
    {
        var document = new TextDocument("```\n```");

        var resolved = MarkdownCodeBlockCopyHelper.TryResolve(document, 1, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void TryResolve_ReturnsFalseOutsideFencedCode()
    {
        var document = new TextDocument("plain\n```\ncode\n```");

        var resolved = MarkdownCodeBlockCopyHelper.TryResolve(document, 1, out _);

        Assert.False(resolved);
    }
}
