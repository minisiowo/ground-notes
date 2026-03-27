using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
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
    public void WrappedCodeBlock_PreservesHookIndentAcrossSegments()
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
    public void WrappedCodeBlock_PreservesHookIndentAfterResize()
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
    public void WrappedCodeBlock_ReflowsWithHookOnResizeWithoutManualTextViewRedraw()
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
        FlushUiDispatcher();

        Assert.True(textView.VisualLinesValid);
        AssertIndentedWrappedCode(textView, editor.Document);
    }

    [Fact]
    public void PlainLine_DoesNotReceiveHookIndentFromStaleFenceSnapshot()
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

        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.ApplyTemplate();
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));

        var textView = editor.TextArea.TextView;
        ApplyTextViewLayout(textView, editor.Width, editor.Height);

        var plainLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(5));
        var plainStarts = plainLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(plainLine, textLine)).ToArray();

        Assert.True(plainStarts.Length > 1, $"Plain starts: {string.Join(", ", plainStarts)}");
        Assert.All(plainStarts.Skip(1), start => Assert.True(start < 1.0, $"Plain starts: {string.Join(", ", plainStarts)}; Elements: {DescribeElements(plainLine)}"));
    }

    [Fact]
    public void WrappedCodeBlock_ContinuationCaretRoundTripsThroughVisualPosition()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateWrappedEditor(colorizer, 100, 480);
        var textView = editor.TextArea.TextView;
        var codeLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));
        var continuationTextLine = Assert.Single(codeLine.TextLines.Skip(1).Take(1));
        var targetVisualColumn = GetContinuationVisualColumn(codeLine, continuationTextLine, 8);
        var expectedPosition = codeLine.GetTextViewPosition(targetVisualColumn);
        var targetPoint = codeLine.GetVisualPosition(targetVisualColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle) + new Vector(0.1, 0);

        var hitPosition = Assert.IsType<TextViewPosition>(textView.GetPosition(targetPoint));

        Assert.Equal(expectedPosition.Location, hitPosition.Location);
        Assert.Equal(expectedPosition.VisualColumn, hitPosition.VisualColumn);

        editor.TextArea.Caret.Position = hitPosition;
        FlushUiDispatcher();

        var caretPoint = textView.GetVisualPosition(editor.TextArea.Caret.Position, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle);
        Assert.True(Math.Abs(caretPoint.X - codeLine.GetVisualPosition(targetVisualColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle).X) < 1.0);
    }

    [Fact]
    public void WrappedCodeBlock_ContinuationHitTestingProducesCopyableSelection()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateWrappedEditor(colorizer, 100, 480);
        var textView = editor.TextArea.TextView;
        var codeLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));
        var continuationTextLine = Assert.Single(codeLine.TextLines.Skip(1).Take(1));
        var startVisualColumn = GetContinuationVisualColumn(codeLine, continuationTextLine, 4);
        var endVisualColumn = GetContinuationVisualColumn(codeLine, continuationTextLine, 18);
        var startPoint = codeLine.GetVisualPosition(startVisualColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle) + new Vector(0.1, 0);
        var endPoint = codeLine.GetVisualPosition(endVisualColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle) + new Vector(0.1, 0);

        var startPosition = Assert.IsType<TextViewPosition>(textView.GetPosition(startPoint));
        var endPosition = Assert.IsType<TextViewPosition>(textView.GetPosition(endPoint));
        var startOffset = editor.Document.GetOffset(startPosition.Location);
        var endOffset = editor.Document.GetOffset(endPosition.Location);

        editor.TextArea.Selection = Selection.Create(editor.TextArea, startOffset, endOffset);

        Assert.False(editor.TextArea.Selection.IsEmpty);
        Assert.True(endOffset > startOffset);
        Assert.Equal(editor.Document.GetText(startOffset, endOffset - startOffset), editor.SelectedText);
    }

    [Fact]
    public void OrdinaryIndentedLine_PreservesWhitespaceIndentAcrossSegments()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateWrappedIndentedPlainSample()), 110, 480, colorizer, out _);
        var textView = editor.TextArea.TextView;
        var indentedLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(2));

        AssertWrappedLinePreservesIndent(indentedLine);
    }

    [Fact]
    public void OrdinaryIndentedLine_PreservesWhitespaceIndentAfterFullDocumentReplace()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument("before"),
            WordWrap = true,
            Width = 110,
            Height = 480
        };
        using var host = new EditorHostController(editor, colorizer);
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
        ApplyTextViewLayout(editor.TextArea.TextView, editor.Width, editor.Height);

        var changed = host.SyncFromViewModel(CreateWrappedIndentedPlainSample(), appendSuffixWhenPossible: false, out var appendedOnly);

        Assert.True(changed);
        Assert.False(appendedOnly);

        ApplyTextViewLayout(editor.TextArea.TextView, editor.Width, editor.Height);

        var indentedLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(2));
        AssertWrappedLinePreservesIndent(indentedLine);
    }

    [Fact]
    public void WrappedCodeBlock_PreservesHookIndentAtNarrowWidth()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateWrappedEditor(colorizer, 40, 480);
        var textView = editor.TextArea.TextView;

        var codeLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));

        AssertWrappedLinePreservesIndent(codeLine);
    }

    [Fact]
    public void BulletList_WrappedContinuationAlignsToListText()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateWrappedListSample()), 120, 480, colorizer, out _);
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(2));

        AssertWrappedLineAlignsToMarkdownTextStart(visualLine, editor.Document.GetText(editor.Document.GetLineByNumber(2)));
    }

    [Fact]
    public void OrderedList_WrappedContinuationAlignsToListText()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateWrappedListSample()), 120, 480, colorizer, out _);
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));

        AssertWrappedLineAlignsToMarkdownTextStart(visualLine, editor.Document.GetText(editor.Document.GetLineByNumber(3)));
    }

    [Fact]
    public void TaskList_WrappedContinuationAlignsAfterCheckbox()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateWrappedListSample()), 120, 480, colorizer, out _);
        var visualLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(4));

        AssertWrappedLineAlignsToMarkdownTextStart(visualLine, editor.Document.GetText(editor.Document.GetLineByNumber(4)));
    }

    [Fact]
    public void MarkerlessListContinuationLine_AlignsToOwningListText()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateSoftBreakListSample()), 120, 480, colorizer, out _);
        var bulletLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));
        var taskLine = editor.TextArea.TextView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(5));

        AssertWrappedContinuationLineAlignsToPreviousListText(
            bulletLine,
            editor.Document.GetText(editor.Document.GetLineByNumber(2)));
        AssertWrappedContinuationLineAlignsToPreviousListText(
            taskLine,
            editor.Document.GetText(editor.Document.GetLineByNumber(4)));
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

    private static string CreateWrappedIndentedPlainSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "    This plain line starts with real document indentation and should wrap several times so every continuation keeps that indentation.");
    }

    private static string CreateWrappedListSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "- This bullet list item should wrap several times so the continuation aligns with the text instead of the dash.",
            "12. This ordered list item should wrap several times so the continuation aligns with the text instead of the number marker.",
            "- [ ] This task list item should wrap several times so the continuation aligns after the checkbox.");
    }

    private static string CreateSoftBreakListSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "- This bullet list item owns the indentation for the next markerless line.",
            "This markerless continuation line should render under the bullet text even though it has no list marker of its own.",
            "- [ ] This task list item also owns the indentation for the next markerless line.",
            "This markerless task continuation line should render after the checkbox alignment.");
    }

    private static TextEditor CreateWrappedEditor(MarkdownColorizingTransformer colorizer, double width, double height)
    {
        return CreateEditor(new TextDocument(CreateWrappedCodeSample()), width, height, colorizer, out _);
    }

    private static TextEditor CreateEditor(TextDocument document, double width, double height, MarkdownColorizingTransformer colorizer, out Window window)
    {
        var editor = new TextEditor
        {
            Document = document,
            WordWrap = true,
            Width = width,
            Height = height
        };
        _ = new EditorThemeController(editor, colorizer);
        window = new Window
        {
            Width = width,
            Height = height,
            Content = editor
        };

        window.ApplyTemplate();
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        editor.ApplyTemplate();
        editor.Measure(new Size(editor.Width, editor.Height));
        editor.Arrange(new Rect(0, 0, editor.Width, editor.Height));
        ApplyTextViewLayout(editor.TextArea.TextView, width, height);

        return editor;
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

    private static int GetContinuationVisualColumn(AvaloniaEdit.Rendering.VisualLine visualLine, Avalonia.Media.TextFormatting.TextLine continuationTextLine, int columnOffset)
    {
        var startColumn = visualLine.GetTextLineVisualStartColumn(continuationTextLine);
        var localColumn = Math.Min(Math.Max(columnOffset, 0), Math.Max(continuationTextLine.Length - 1, 0));
        return startColumn + localColumn;
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
        var codeTextLineStarts = codeLine.TextLines.Select(textLine => textLine.Start).ToArray();

        var codeDebug = $"Code starts: {string.Join(", ", codeStarts)}; TextLine starts: {string.Join(", ", codeTextLineStarts)}; Elements: {DescribeElements(codeLine)}";
        Assert.True(codeStarts.Length > 1, codeDebug);
        Assert.All(codeStarts, start => Assert.True(start > 0, codeDebug));
        Assert.True(codeStarts.Skip(1).All(start => Math.Abs(start - codeStarts[0]) < 1.0), codeDebug);

        Assert.True(plainStarts.Length > 1, $"Plain starts: {string.Join(", ", plainStarts)}");
        Assert.All(plainStarts, start => Assert.True(start >= 0));
        Assert.All(plainStarts.Skip(1), start => Assert.True(start < 1.0, $"Plain starts: {string.Join(", ", plainStarts)}"));
    }

    private static void AssertWrappedLinePreservesIndent(AvaloniaEdit.Rendering.VisualLine visualLine)
    {
        var starts = visualLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(visualLine, textLine)).ToArray();
        var textLineStarts = visualLine.TextLines.Select(textLine => textLine.Start).ToArray();
        var debug = $"Starts: {string.Join(", ", starts)}; TextLine starts: {string.Join(", ", textLineStarts)}; Elements: {DescribeElements(visualLine)}";

        Assert.True(starts.Length > 1, debug);
        Assert.All(starts, start => Assert.True(start > 0, debug));
        Assert.True(starts.Skip(1).All(start => Math.Abs(start - starts[0]) < 1.0), debug);
    }

    private static void AssertWrappedLineAlignsToMarkdownTextStart(AvaloniaEdit.Rendering.VisualLine visualLine, string lineText)
    {
        var analysis = MarkdownLineParser.Analyze(lineText, MarkdownFenceState.None);
        var textRange = analysis.TaskList?.Text ?? analysis.ListMarker?.Text;
        Assert.True(textRange.HasValue, $"No list text range for line: {lineText}");

        var expectedStart = visualLine.GetVisualPosition(textRange.Value.Start, AvaloniaEdit.Rendering.VisualYPosition.LineTop).X;
        var starts = visualLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(visualLine, textLine)).ToArray();
        var textLineStarts = visualLine.TextLines.Select(textLine => textLine.Start).ToArray();
        var debug = $"Expected: {expectedStart}; Starts: {string.Join(", ", starts)}; TextLine starts: {string.Join(", ", textLineStarts)}; Elements: {DescribeElements(visualLine)}";

        Assert.True(starts.Length > 1, debug);
        Assert.True(Math.Abs(starts[0] - expectedStart) > 1.0, debug);
        Assert.True(starts.Skip(1).All(start => Math.Abs(start - expectedStart) < 1.0), debug);
    }

    private static void AssertWrappedContinuationLineAlignsToPreviousListText(AvaloniaEdit.Rendering.VisualLine visualLine, string ownerLineText)
    {
        var ownerAnalysis = MarkdownLineParser.Analyze(ownerLineText, MarkdownFenceState.None);
        var ownerTextRange = ownerAnalysis.TaskList?.Text ?? ownerAnalysis.ListMarker?.Text;
        Assert.True(ownerTextRange.HasValue, $"No list text range for owner line: {ownerLineText}");

        var expectedStart = visualLine.GetVisualPosition(ownerTextRange.Value.Start, AvaloniaEdit.Rendering.VisualYPosition.LineTop).X;
        var starts = visualLine.TextLines.Select(textLine => GetFirstNonWhitespaceX(visualLine, textLine)).ToArray();
        var debug = $"Expected: {expectedStart}; Starts: {string.Join(", ", starts)}; Elements: {DescribeElements(visualLine)}";

        Assert.NotEmpty(starts);
        Assert.All(starts, start => Assert.True(Math.Abs(start - expectedStart) < 1.0, debug));
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
