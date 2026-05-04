using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownVisualLineIndentationProviderTests
{
    [Fact]
    public void GetVisualIndentationColumns_ReturnsIndentForFencedLinesOnly()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(1)));
        Assert.Equal(2, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(2)));
        Assert.Equal(2, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(3)));
        Assert.Equal(2, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(4)));
        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(5)));
    }

    [Fact]
    public void GetTrailingVisualInsetColumns_ReturnsInsetForFencedLinesOnly()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Equal(0, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(1)));
        Assert.Equal(2, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(2)));
        Assert.Equal(2, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(3)));
        Assert.Equal(2, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(4)));
        Assert.Equal(0, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(5)));
    }

    [Fact]
    public void GetWrappedLineContinuationStartColumn_ReturnsHangingIndentForLists()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateListSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Null(provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(1)));
        Assert.Equal(2, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(2)));
        Assert.Equal(3, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(3)));
        Assert.Equal(6, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(4)));

        Assert.Equal(0, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(2)));
        Assert.Equal(0, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(3)));
        Assert.Equal(0, provider.GetTrailingVisualInsetColumns(textView, document.GetLineByNumber(4)));
    }

    [Fact]
    public void GetWrappedLineContinuationStartColumn_InheritsIndentForMarkerlessListContinuationLines()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateSoftBreakListSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Equal(2, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(2)));
        Assert.Equal(2, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(3)));
        Assert.Equal(6, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(4)));
        Assert.Equal(6, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(5)));
        Assert.Null(provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(6)));
    }

    [Fact]
    public void GetVisualIndentationColumns_TopsUpMarkerlessListContinuationLinesToOwnerTextColumn()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateSoftBreakListSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(2)));
        Assert.Equal(2, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(3)));
        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(4)));
        Assert.Equal(6, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(5)));
        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(6)));
    }

    [Fact]
    public void GetVisualIndentationColumns_DoesNotTopUpWhitespaceOnlyMarkerlessContinuationLines()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateWhitespaceOnlyContinuationSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(3)));
        Assert.Equal(2, provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(3)));
    }

    [Fact]
    public void SuppressedMarkerlessLine_DoesNotReceiveListContinuationIndentation()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        colorizer.SuppressListContinuationForLine(3);
        var provider = new MarkdownVisualLineIndentationProvider(colorizer);
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateSuppressedContinuationSample())
        };
        editor.ApplyTemplate();
        var textView = editor.TextArea.TextView;
        var document = editor.Document;

        Assert.Equal(0, provider.GetVisualIndentationColumns(textView, document.GetLineByNumber(3)));
        Assert.Null(provider.GetWrappedLineContinuationStartColumn(textView, document.GetLineByNumber(3)));
    }

    private static string CreateSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "```csharp",
            "code",
            "```",
            "after");
    }

    private static string CreateListSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "- bullet item",
            "1. ordered item",
            "- [ ] task item");
    }

    private static string CreateSoftBreakListSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "- bullet item",
            "continued after soft break",
            "- [ ] task item",
            "continued task paragraph",
            "# heading");
    }

    private static string CreateWhitespaceOnlyContinuationSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "- bullet item",
            "  ");
    }

    private static string CreateSuppressedContinuationSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "- bullet item",
            "plain paragraph after explicit list exit");
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }

        GroundNotes.Program.BuildAvaloniaApp().SetupWithoutStarting();
    }
}
