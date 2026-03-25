using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using GroundNotes.Editors;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class EditorThemeControllerTests
{
    [Fact]
    public void ConfigureEditorOptions_DisablesBuiltInHyperlinkRendering()
    {
        var options = new TextEditorOptions();

        EditorThemeController.ConfigureEditorOptions(options);

        Assert.False(options.ConvertTabsToSpaces);
        Assert.False(options.EnableRectangularSelection);
        Assert.False(options.EnableHyperlinks);
        Assert.False(options.EnableEmailHyperlinks);
        Assert.False(options.RequireControlModifierForHyperlinkClick);
        Assert.False(options.WordWrapIndentation > 0);
    }

    [Fact]
    public void CodeBlockIndent_PersistsAcrossWrappedSegments()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateWrappedCodeSample()),
            WordWrap = true,
            Width = 100,
            Height = 480
        };
        using var controller = new EditorThemeController(editor, colorizer);
        var window = new Window
        {
            Width = editor.Width,
            Height = editor.Height,
            Content = editor
        };

        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.ApplyTemplate();
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));

        var textView = editor.TextArea.TextView;
        textView.Measure(new Size(editor.Width, editor.Height));
        textView.Arrange(new Rect(0, 0, editor.Width, editor.Height));
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        textView.Redraw();
        textView.EnsureVisualLines();

        AssertIndentedWrappedCode(textView, editor.Document);
    }

    [Fact]
    public void CodeBlockIndent_PersistsAfterEditorResize()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateWrappedCodeSample()),
            WordWrap = true,
            Width = 160,
            Height = 480
        };
        using var controller = new EditorThemeController(editor, colorizer);
        var window = new Window
        {
            Width = editor.Width,
            Height = editor.Height,
            Content = editor
        };

        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.ApplyTemplate();
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));

        var textView = editor.TextArea.TextView;
        ApplyTextViewLayout(textView, editor.Width, editor.Height);
        AssertIndentedWrappedCode(textView, editor.Document);

        editor.Width = 100;
        window.Width = editor.Width;
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
        ApplyTextViewLayout(textView, editor.Width, editor.Height);

        AssertIndentedWrappedCode(textView, editor.Document);
    }

    [Fact]
    public void CodeBlockIndent_ReformatsOnResizeWithoutManualTextViewRedraw()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateWrappedCodeSample()),
            WordWrap = true,
            Width = 160,
            Height = 480
        };
        using var controller = new EditorThemeController(editor, colorizer);
        var window = new Window
        {
            Width = editor.Width,
            Height = editor.Height,
            Content = editor
        };

        ApplyHostLayout(window, editor);

        var textView = editor.TextArea.TextView;
        ApplyTextViewLayout(textView, editor.Width, editor.Height);
        AssertIndentedWrappedCode(textView, editor.Document);

        editor.Width = 100;
        window.Width = editor.Width;
        ApplyHostLayout(window, editor);
        FlushUiDispatcher();

        Assert.True(textView.VisualLinesValid);
        AssertIndentedWrappedCode(textView, editor.Document);
    }

    [Fact]
    public void PlainLine_DoesNotReceiveCodeBlockIndentFromStaleFenceSnapshot()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        SeedStaleFencedLine(colorizer, 5);

        var editor = new TextEditor
        {
            Document = new TextDocument(CreateWrappedCodeSample()),
            WordWrap = true,
            Width = 100,
            Height = 480
        };
        using var controller = new EditorThemeController(editor, colorizer);
        var window = new Window
        {
            Width = editor.Width,
            Height = editor.Height,
            Content = editor
        };

        ApplyHostLayout(window, editor);

        var textView = editor.TextArea.TextView;
        ApplyTextViewLayout(textView, editor.Width, editor.Height);

        var plainLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(5));
        var plainStarts = plainLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(plainLine, textLine)).ToArray();

        Assert.True(plainStarts.Length > 1, $"Plain starts: {string.Join(", ", plainStarts)}");
        Assert.All(plainStarts.Skip(1), start => Assert.True(start < 1.0, $"Plain starts: {string.Join(", ", plainStarts)}; Elements: {DescribeElements(plainLine)}"));
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }

        GroundNotes.Program.BuildAvaloniaApp().SetupWithoutStarting();
    }

    private static string CreateWrappedCodeSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "```",
            "This fenced code line should wrap several times in a narrow editor so every continuation keeps the code block indentation.",
            "```",
            "This plain line should also wrap in the same narrow editor but its continuations must return to the baseline.");
    }

    private static void ApplyTextViewLayout(AvaloniaEdit.Rendering.TextView textView, double width, double height)
    {
        textView.Measure(new Size(width, height));
        textView.Arrange(new Rect(0, 0, width, height));
        textView.InvalidateMeasure();
        textView.InvalidateArrange();
        textView.InvalidateVisual();
        textView.Redraw();
        textView.EnsureVisualLines();
    }

    private static void ApplyHostLayout(Window window, TextEditor editor)
    {
        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.ApplyTemplate();
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
    }

    private static void FlushUiDispatcher()
    {
        Dispatcher.UIThread.RunJobs(DispatcherPriority.Render);
    }

    private static void AssertIndentedWrappedCode(AvaloniaEdit.Rendering.TextView textView, TextDocument document)
    {
        var codeLine = textView.GetOrConstructVisualLine(document.GetLineByNumber(3));
        var plainLine = textView.GetOrConstructVisualLine(document.GetLineByNumber(5));

        var codeStarts = codeLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(codeLine, textLine)).ToArray();
        var plainStarts = plainLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(plainLine, textLine)).ToArray();

        var codeDebug = $"Code starts: {string.Join(", ", codeStarts)}; Elements: {DescribeElements(codeLine)}";
        Assert.True(codeStarts.Length > 1, codeDebug);
        Assert.All(codeStarts, start => Assert.True(start > 0, codeDebug));
        Assert.True(codeStarts.Skip(1).All(start => Math.Abs(start - codeStarts[0]) < 1.0), codeDebug);

        Assert.True(plainStarts.Length > 1, $"Plain starts: {string.Join(", ", plainStarts)}");
        Assert.All(plainStarts, start => Assert.True(start >= 0));
        Assert.All(plainStarts.Skip(1), start => Assert.True(start < 1.0, $"Plain starts: {string.Join(", ", plainStarts)}"));
    }

    private static double GetFirstNonWhitespaceX(AvaloniaEdit.Rendering.VisualLine visualLine, Avalonia.Media.TextFormatting.TextLine textLine)
    {
        var startColumn = visualLine.GetTextLineVisualStartColumn(textLine);
        var endColumn = startColumn + textLine.Length;

        for (var visualColumn = startColumn; visualColumn < endColumn; visualColumn++)
        {
            if (IsWhitespace(visualLine, visualColumn))
            {
                continue;
            }

            return visualLine.GetVisualPosition(visualColumn, AvaloniaEdit.Rendering.VisualYPosition.LineTop).X;
        }

        return visualLine.GetVisualPosition(startColumn, AvaloniaEdit.Rendering.VisualYPosition.LineTop).X;
    }

    private static bool IsWhitespace(AvaloniaEdit.Rendering.VisualLine visualLine, int visualColumn)
    {
        foreach (var element in visualLine.Elements)
        {
            var elementStart = element.VisualColumn;
            var elementEnd = elementStart + element.VisualLength;
            if (visualColumn < elementStart || visualColumn >= elementEnd)
            {
                continue;
            }

            return element.IsWhitespace(visualColumn);
        }

        return false;
    }

    private static string DescribeElements(AvaloniaEdit.Rendering.VisualLine visualLine)
    {
        return string.Join(
            " | ",
            visualLine.Elements.Select(element =>
                $"{element.GetType().Name}[vc={element.VisualColumn},len={element.VisualLength},doc={element.DocumentLength}]"));
    }

    private static void SeedStaleFencedLine(MarkdownColorizingTransformer colorizer, int lineNumber)
    {
        var field = typeof(MarkdownColorizingTransformer).GetField("_fencedLineNumbers", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Fenced line numbers field not found.");
        var fencedLineNumbers = (HashSet<int>)(field.GetValue(colorizer)
            ?? throw new InvalidOperationException("Fenced line numbers field is null."));
        fencedLineNumbers.Add(lineNumber);
    }
}
