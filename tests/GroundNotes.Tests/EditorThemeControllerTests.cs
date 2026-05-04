using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private static readonly Lock ApplicationLock = new();
    private static bool _applicationInitialized;

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
    public void SetMarkdownFormattingEnabled_DisablesMarkdownPresentationButKeepsWordWrap()
    {
        EnsureApplication();
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument("# heading\n\n![](photo.png)"),
            WordWrap = true,
            Width = 320,
            Height = 200
        };

        using var controller = new EditorThemeController(editor, colorizer);

        controller.SetMarkdownFormattingEnabled(false);

        Assert.True(editor.WordWrap);
        Assert.Null(editor.TextArea.TextView.VisualLineIndentationProvider);
        Assert.DoesNotContain(colorizer, editor.TextArea.TextView.LineTransformers);
        Assert.DoesNotContain(editor.TextArea.TextView.Layers, layer => layer is MarkdownCodeBlockCopyLayer);

        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();
    }

    [Fact]
    public void SetMarkdownFormattingEnabled_ReEnablesMarkdownPresentation()
    {
        EnsureApplication();
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument("# heading"),
            WordWrap = true,
            Width = 320,
            Height = 200
        };

        using var controller = new EditorThemeController(editor, colorizer);

        controller.SetMarkdownFormattingEnabled(false);
        controller.SetMarkdownFormattingEnabled(true);

        Assert.True(controller.IsMarkdownFormattingEnabled);
        Assert.Same(colorizer, Assert.Single(editor.TextArea.TextView.LineTransformers.OfType<MarkdownColorizingTransformer>()));
        Assert.NotNull(editor.TextArea.TextView.VisualLineIndentationProvider);
        Assert.Single(editor.TextArea.TextView.Layers.OfType<MarkdownCodeBlockCopyLayer>());

        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();
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
    public void ImagePreviewLayer_SkipsIdenticalRefreshes()
    {
        EnsureApplication();

        using var tempDirectory = new TempDirectory();
        var imagePath = CreateImageAsset(tempDirectory.DirectoryPath, "photo.png", 6, 3);
        var document = new TextDocument($"![]({Path.GetRelativePath(tempDirectory.DirectoryPath, imagePath).Replace('\\', '/')})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = document,
            WordWrap = true,
            Width = 240,
            Height = 200
        };
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

        using var previewProvider = new MarkdownImagePreviewProvider(colorizer, new Services.NoteAssetService());
        previewProvider.SetBaseDirectoryPath(tempDirectory.DirectoryPath);
        previewProvider.SetAvailableWidth(editor.Width);
        using var previewLayer = new MarkdownImagePreviewLayer(textView, previewProvider, subscribeToTextViewEvents: false);
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        previewLayer.Refresh();
        previewLayer.Refresh();
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Equal(2, diagnostics.PreviewLayerRefreshes);
        Assert.Equal(1, diagnostics.PreviewLayerRefreshSkips);
        Assert.Equal(1, diagnostics.ImagePreviewRequests);
    }

    [Fact]
    public void ImagePreviewLayer_ReusesImageControlsAfterRefreshStateInvalidation()
    {
        EnsureApplication();

        using var tempDirectory = new TempDirectory();
        var imagePath = CreateImageAsset(tempDirectory.DirectoryPath, "photo.png", 6, 3);
        var document = new TextDocument($"![]({Path.GetRelativePath(tempDirectory.DirectoryPath, imagePath).Replace('\\', '/')})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = document,
            WordWrap = true,
            Width = 240,
            Height = 200
        };
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

        using var previewProvider = new MarkdownImagePreviewProvider(colorizer, new Services.NoteAssetService());
        previewProvider.SetBaseDirectoryPath(tempDirectory.DirectoryPath);
        previewProvider.SetAvailableWidth(editor.Width);
        using var previewLayer = new MarkdownImagePreviewLayer(textView, previewProvider, subscribeToTextViewEvents: false);
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        previewLayer.Refresh();
        previewLayer.InvalidateRefreshState();
        previewLayer.Refresh();
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Equal(2, diagnostics.PreviewLayerRefreshes);
        Assert.Equal(0, diagnostics.PreviewLayerRefreshSkips);
        Assert.Equal(2, diagnostics.ImagePreviewRequests);
    }

    [Fact]
    public void ImagePreviewLayer_ReusesRenderedLineStateDuringForcedRefresh()
    {
        EnsureApplication();

        using var tempDirectory = new TempDirectory();
        var imagePath = CreateImageAsset(tempDirectory.DirectoryPath, "photo.png", 6, 3);
        var document = new TextDocument($"![]({Path.GetRelativePath(tempDirectory.DirectoryPath, imagePath).Replace('\\', '/')})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = document,
            WordWrap = true,
            Width = 240,
            Height = 200
        };
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

        using var previewProvider = new MarkdownImagePreviewProvider(colorizer, new Services.NoteAssetService());
        previewProvider.SetBaseDirectoryPath(tempDirectory.DirectoryPath);
        previewProvider.SetAvailableWidth(editor.Width);
        using var previewLayer = new MarkdownImagePreviewLayer(textView, previewProvider, subscribeToTextViewEvents: false);
        previewLayer.Refresh();
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        previewLayer.InvalidateRefreshState(clearRenderedLineStates: false);
        previewLayer.Refresh();
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Equal(1, diagnostics.PreviewLayerRefreshes);
        Assert.Equal(1, diagnostics.PreviewLayerLineStateReuses);
        Assert.Equal(0, diagnostics.ImagePreviewRequests);
    }

    [Fact]
    public void RefreshAfterDocumentReplace_DefersColdImageBitmapDecodeUntilAfterInitialRefresh()
    {
        EnsureApplication();

        using var tempDirectory = new TempDirectory();
        var imagePath = CreateImageAsset(tempDirectory.DirectoryPath, "photo.png", 6, 3);
        var document = new TextDocument($"![]({Path.GetRelativePath(tempDirectory.DirectoryPath, imagePath).Replace('\\', '/')})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = document,
            WordWrap = true,
            Width = 240,
            Height = 200
        };
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

        using var controller = new EditorThemeController(editor, colorizer);
        controller.SetBaseDirectoryPath(tempDirectory.DirectoryPath);
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        controller.RefreshAfterDocumentReplace();
        textView.EnsureVisualLines();
        FlushUiDispatcher();

        var diagnostics = MarkdownDiagnostics.Snapshot();
        Assert.True(diagnostics.DeferredBitmapLoadRequests > 0, "Expected deferred bitmap load requests after document replace.");
        Assert.Equal(0, diagnostics.BitmapCacheMisses);

        Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
        FlushUiDispatcher();

        var finalDiagnostics = MarkdownDiagnostics.Snapshot();
        Assert.True(finalDiagnostics.DeferredBitmapLoads > 0, "Expected deferred bitmap loads to complete.");
    }

    [Fact]
    public void ResetViewportToDocumentStart_AfterReplacementWithMarkdownImageLine_ClearsCaretSelectionAndScrollOffset()
    {
        EnsureApplication();

        using var tempDirectory = new TempDirectory();
        var imagePath = CreateImageAsset(tempDirectory.DirectoryPath, "photo.png", 1, 1);
        var relativeImagePath = Path.GetRelativePath(tempDirectory.DirectoryPath, imagePath).Replace('\\', '/');
        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = new TextDocument(CreateLongScrollSample()),
            WordWrap = true,
            Width = 320,
            Height = 180
        };
        using var host = new EditorHostController(editor, colorizer);
        host.SetBaseDirectoryPath(tempDirectory.DirectoryPath);
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

        ((IScrollable)textView).Offset = new Vector(24, 220);
        editor.Select(12, 6);
        editor.CaretOffset = 18;

        Assert.True(textView.ScrollOffset.Y > 0, $"Expected a positive setup scroll offset, got {textView.ScrollOffset}.");

        var changed = host.SyncFromViewModel(CreateImageReplacementSample(relativeImagePath), appendSuffixWhenPossible: false, out var appendedOnly);

        Assert.True(changed);
        Assert.False(appendedOnly);

        host.RefreshLayoutAfterDocumentReplace();
        host.ResetViewportToDocumentStart();

        Assert.Equal(0, editor.CaretOffset);
        Assert.Equal(0, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal(0, textView.ScrollOffset.X);
        Assert.Equal(0, textView.ScrollOffset.Y);
    }

    [Fact]
    public void ImagePreviewLayer_CoalescesQueuedRefreshRequests()
    {
        EnsureApplication();

        using var tempDirectory = new TempDirectory();
        var imagePath = CreateImageAsset(tempDirectory.DirectoryPath, "photo.png", 6, 3);
        var document = new TextDocument($"![]({Path.GetRelativePath(tempDirectory.DirectoryPath, imagePath).Replace('\\', '/')})|100");
        using var colorizer = new MarkdownColorizingTransformer();
        var editor = new TextEditor
        {
            Document = document,
            WordWrap = true,
            Width = 240,
            Height = 200
        };
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

        using var previewProvider = new MarkdownImagePreviewProvider(colorizer, new Services.NoteAssetService());
        previewProvider.SetBaseDirectoryPath(tempDirectory.DirectoryPath);
        previewProvider.SetAvailableWidth(editor.Width);
        using var previewLayer = new MarkdownImagePreviewLayer(textView, previewProvider, subscribeToTextViewEvents: false);
        FlushUiDispatcher();
        MarkdownDiagnostics.Reset();

        previewLayer.RequestRefresh();
        previewLayer.RequestRefresh();
        var requestDiagnostics = MarkdownDiagnostics.Snapshot();

        Assert.Equal(2, requestDiagnostics.PreviewLayerRefreshRequests);
        Assert.Equal(1, requestDiagnostics.PreviewLayerRefreshPosts);
        Assert.Equal(0, requestDiagnostics.PreviewLayerRefreshes);

        FlushUiDispatcher();
        var diagnostics = MarkdownDiagnostics.Snapshot();

        Assert.True(diagnostics.PreviewLayerRefreshes >= 1);
        Assert.Equal(1, diagnostics.ImagePreviewRequests);
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
    public void WrappedCodeBlock_ReservesRightInsetWhenWrapping()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var longText = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma tau";
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                longText,
                "```",
                longText)),
            140,
            480,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var codeLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));
        var plainLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(5));
        var trailingInset = GetFirstNonWhitespaceX(codeLine, codeLine.TextLines[0]);
        var rightLimit = textView.Bounds.Width - trailingInset;
        var codeRightEdges = codeLine.TextLines.Select(textLine => GetTextLineRightEdge(codeLine, textLine)).ToArray();
        var plainRightEdges = plainLine.TextLines.Select(textLine => GetTextLineRightEdge(plainLine, textLine)).ToArray();

        Assert.True(codeLine.TextLines.Count > 1, $"Expected wrapped code line. Edges: {string.Join(", ", codeRightEdges)}; Elements: {DescribeElements(codeLine)}");
        Assert.True(plainLine.TextLines.Count > 1, $"Expected wrapped plain line. Edges: {string.Join(", ", plainRightEdges)}; Elements: {DescribeElements(plainLine)}");
        Assert.All(
            codeRightEdges,
            rightEdge => Assert.True(
                rightEdge <= rightLimit + 2.0,
                $"Code line right edge {rightEdge} should stay within trailing inset limit {rightLimit}. Edges: {string.Join(", ", codeRightEdges)}; Elements: {DescribeElements(codeLine)}"));
        Assert.Contains(
            plainRightEdges,
            rightEdge => rightEdge > rightLimit + 1.0);
    }

    [Fact]
    public void NarrowCodeBlock_KeepsTextAfterVisualIndentation()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                "abc def ghi",
                "```",
                "after")),
            28,
            240,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var visualLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));
        var firstTextLine = visualLine.TextLines[0];
        var firstTextX = GetFirstNonWhitespaceX(visualLine, firstTextLine);
        var firstDocumentColumn = visualLine.GetVisualColumn(0);

        Assert.True(visualLine.TextLines.Count > 1, $"Expected narrow code line to wrap. Elements: {DescribeElements(visualLine)}");
        Assert.Same(firstTextLine, visualLine.GetTextLine(firstDocumentColumn));
        Assert.True(
            firstTextX >= visualLine.GetVisualPosition(firstDocumentColumn, AvaloniaEdit.Rendering.VisualYPosition.LineTop).X - 0.5,
            $"First text X {firstTextX} should remain at or after code text start. Elements: {DescribeElements(visualLine)}");
    }

    [Fact]
    public void CodeBlockSelection_DoesNotExtendIntoTrailingInsetForLineEndSelection()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma tau",
                "```",
                "after")),
            140,
            480,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var contentLine = editor.Document.GetLineByNumber(3);
        var visualLine = textView.GetOrConstructVisualLine(contentLine);
        var trailingInset = GetFirstNonWhitespaceX(visualLine, visualLine.TextLines[0]);
        var rightLimit = textView.Bounds.Width - trailingInset;
        var segment = new SelectionSegment(contentLine.Offset, 0, contentLine.Offset + contentLine.Length, -1);
        var selectionRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, segment)
            .ToArray();
        var lineTops = visualLine.TextLines
            .Select(textLine => visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop) - textView.ScrollOffset.Y)
            .ToArray();
        var codeLineSelectionRects = selectionRects
            .Where(rect => lineTops.Any(top => Math.Abs(rect.Top - top) < 1.0))
            .ToArray();

        Assert.True(visualLine.TextLines.Count > 1, $"Expected wrapped code line. Elements: {DescribeElements(visualLine)}");
        Assert.NotEmpty(codeLineSelectionRects);
        Assert.All(
            codeLineSelectionRects,
            rect => Assert.True(
                rect.Right <= rightLimit + 2.0,
                $"Selection right {rect.Right} should stay within trailing inset limit {rightLimit}. Rects: {string.Join(", ", codeLineSelectionRects)}; Elements: {DescribeElements(visualLine)}"));
    }

    [Fact]
    public void CodeBlockHitTesting_RightInsetMapsToWrappedRowEnd()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma tau",
                "```",
                "after")),
            140,
            480,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var visualLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(3));
        var firstTextLine = visualLine.TextLines[0];
        var trailingInset = GetFirstNonWhitespaceX(visualLine, firstTextLine);
        var pointInsideTrailingInset = new Point(
            textView.Bounds.Width - trailingInset / 2,
            visualLine.GetTextLineVisualYPosition(firstTextLine, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle));

        var position = Assert.IsType<TextViewPosition>(textView.GetPosition(pointInsideTrailingInset));
        var rowEndColumn = visualLine.GetTextLineVisualStartColumn(firstTextLine) + firstTextLine.Length;
        var caretPoint = visualLine.GetVisualPosition(position.VisualColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle);

        Assert.Equal(rowEndColumn, position.VisualColumn);
        Assert.True(
            caretPoint.X <= textView.Bounds.Width - trailingInset + 1.0,
            $"Hit-tested caret X {caretPoint.X} should stay within trailing inset. Elements: {DescribeElements(visualLine)}");
    }

    [Fact]
    public void CodeBlockSelection_ClampsFullSelectedLinesToVisualIndentation()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateMultiLineCodeSelectionSample()), 520, 240, colorizer, out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var firstContentLine = document.GetLineByNumber(3);
        var blankContentLine = document.GetLineByNumber(4);
        var secondContentLine = document.GetLineByNumber(5);
        var selectionStart = firstContentLine.Offset;
        var selectionEnd = secondContentLine.Offset + secondContentLine.Length;

        textView.EnsureVisualLines();

        var segment = new SelectionSegment(selectionStart, 0, selectionEnd, -1);
        var selectionRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, segment)
            .ToArray();
        var firstVisualLine = textView.GetOrConstructVisualLine(firstContentLine);
        var firstTextLine = Assert.Single(firstVisualLine.TextLines);
        var firstLineTop = firstVisualLine.GetTextLineVisualYPosition(firstTextLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var firstLineSelectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - firstLineTop) < 1.0);
        var firstExpectedLeft = GetFirstNonWhitespaceX(firstVisualLine, firstTextLine) - textView.ScrollOffset.X;
        var secondVisualLine = textView.GetOrConstructVisualLine(secondContentLine);
        var secondTextLine = Assert.Single(secondVisualLine.TextLines);
        var secondLineTop = secondVisualLine.GetTextLineVisualYPosition(secondTextLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var secondLineSelectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - secondLineTop) < 1.0);
        var expectedLeft = GetFirstNonWhitespaceX(secondVisualLine, secondTextLine) - textView.ScrollOffset.X;
        var expectedRight = secondVisualLine.GetTextLineVisualXPosition(secondTextLine, secondVisualLine.GetVisualColumn(secondContentLine.Length))
            - textView.ScrollOffset.X;
        var blankVisualLine = textView.GetOrConstructVisualLine(blankContentLine);
        var blankTextLine = Assert.Single(blankVisualLine.TextLines);
        var blankLineTop = blankVisualLine.GetTextLineVisualYPosition(blankTextLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var blankLineSelectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - blankLineTop) < 1.0);

        Assert.True(
            firstLineSelectionRect.Left >= firstExpectedLeft - 0.5,
            $"Selection left {firstLineSelectionRect.Left} should stay within first code text start {firstExpectedLeft}. Rects: {string.Join(", ", selectionRects)}; Elements: {DescribeElements(firstVisualLine)}");
        Assert.True(
            blankLineSelectionRect.Left >= firstExpectedLeft - 0.5,
            $"Blank line selection left {blankLineSelectionRect.Left} should stay within code text start {firstExpectedLeft}. Rects: {string.Join(", ", selectionRects)}; Elements: {DescribeElements(blankVisualLine)}");
        Assert.True(
            secondLineSelectionRect.Left >= expectedLeft - 0.5,
            $"Selection left {secondLineSelectionRect.Left} should stay within code text start {expectedLeft}. Rects: {string.Join(", ", selectionRects)}; Elements: {DescribeElements(secondVisualLine)}");
        Assert.True(
            secondLineSelectionRect.Right >= expectedRight - 0.5,
            $"Selection right {secondLineSelectionRect.Right} should include code text end {expectedRight}. Rects: {string.Join(", ", selectionRects)}; Elements: {DescribeElements(secondVisualLine)}");

        var indentOnlySegment = new SelectionSegment(firstContentLine.Offset, 0, firstContentLine.Offset, 1);
        var indentOnlyRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, indentOnlySegment)
            .ToArray();

        Assert.All(
            indentOnlyRects,
            rect => Assert.True(
                rect.Left >= firstExpectedLeft - 0.5,
                $"Indent-only selection left {rect.Left} should clamp to code text start {firstExpectedLeft}. Rects: {string.Join(", ", indentOnlyRects)}; Elements: {DescribeElements(firstVisualLine)}"));
    }

    [Fact]
    public void CodeBlockCaret_ClampsToVisualIndentationOnContentAndBlankLines()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateMultiLineCodeSelectionSample()), 520, 240, colorizer, out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var firstContentLine = document.GetLineByNumber(3);
        var blankContentLine = document.GetLineByNumber(4);
        var firstVisualLine = textView.GetOrConstructVisualLine(firstContentLine);
        var firstTextLine = Assert.Single(firstVisualLine.TextLines);
        var blankVisualLine = textView.GetOrConstructVisualLine(blankContentLine);
        var blankTextLine = Assert.Single(blankVisualLine.TextLines);
        var expectedLeft = GetFirstNonWhitespaceX(firstVisualLine, firstTextLine);

        editor.CaretOffset = firstContentLine.Offset;
        var firstCaretRect = editor.TextArea.Caret.CalculateCaretRectangle();

        editor.CaretOffset = blankContentLine.Offset;
        var blankCaretRect = editor.TextArea.Caret.CalculateCaretRectangle();
        var blankTextTop = blankVisualLine.GetTextLineVisualYPosition(blankTextLine, AvaloniaEdit.Rendering.VisualYPosition.TextTop);

        Assert.True(
            firstCaretRect.Left >= expectedLeft - 0.5,
            $"Caret left {firstCaretRect.Left} should stay within code text start {expectedLeft}. Elements: {DescribeElements(firstVisualLine)}");
        Assert.True(
            blankCaretRect.Left >= expectedLeft - 0.5,
            $"Blank line caret left {blankCaretRect.Left} should stay within code text start {expectedLeft}. Elements: {DescribeElements(blankVisualLine)}");
        Assert.True(Math.Abs(blankCaretRect.Top - blankTextTop) < 1.0);
    }

    [Fact]
    public void CodeBlockHitTesting_NormalizesLeadingVisualIndentation()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateMultiLineCodeSelectionSample()), 520, 240, colorizer, out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var firstContentLine = document.GetLineByNumber(3);
        var visualLine = textView.GetOrConstructVisualLine(firstContentLine);
        var textLine = Assert.Single(visualLine.TextLines);
        var firstSelectableColumn = visualLine.GetVisualColumn(0);
        var indentationStartX = visualLine.GetVisualPosition(0, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle).X;
        var selectableStartX = visualLine.GetVisualPosition(firstSelectableColumn, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle).X;
        var pointInsideVisualIndentation = new Point(
            indentationStartX + (selectableStartX - indentationStartX) / 2,
            visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle));

        var roundedPosition = Assert.IsType<TextViewPosition>(textView.GetPosition(pointInsideVisualIndentation));
        var floorPosition = Assert.IsType<TextViewPosition>(textView.GetPositionFloor(pointInsideVisualIndentation));

        Assert.Equal(firstContentLine.Offset, document.GetOffset(roundedPosition.Location));
        Assert.Equal(firstSelectableColumn, roundedPosition.VisualColumn);
        Assert.Equal(firstContentLine.Offset, document.GetOffset(floorPosition.Location));
        Assert.Equal(firstSelectableColumn, floorPosition.VisualColumn);
    }

    [Fact]
    public void CodeBlockCaretAndSelection_AreCoincidentAtLineStart()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(new TextDocument(CreateMultiLineCodeSelectionSample()), 520, 240, colorizer, out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var firstContentLine = document.GetLineByNumber(3);
        var firstVisualLine = textView.GetOrConstructVisualLine(firstContentLine);
        var firstTextLine = Assert.Single(firstVisualLine.TextLines);
        var expectedLeft = GetFirstNonWhitespaceX(firstVisualLine, firstTextLine) - textView.ScrollOffset.X;

        // Place caret at document offset 0 of the code line.
        editor.CaretOffset = firstContentLine.Offset;
        var caretRect = editor.TextArea.Caret.CalculateCaretRectangle();

        // Create a selection segment that starts at the same document offset with raw visual column 0.
        // This mimics what SelectionMouseHandler does when the user clicks inside visual indentation.
        var selectionStart = firstContentLine.Offset;
        var selectionEnd = firstContentLine.Offset + 10;
        var segment = new SelectionSegment(selectionStart, 0, selectionEnd, -1);
        var selectionRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, segment)
            .ToArray();
        var firstLineTop = firstVisualLine.GetTextLineVisualYPosition(firstTextLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var firstSelectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - firstLineTop) < 1.0);

        // The caret and the selection highlight must start at exactly the same x-position.
        Assert.True(
            Math.Abs(caretRect.Left - firstSelectionRect.Left) < 0.5,
            $"Caret left {caretRect.Left} and selection left {firstSelectionRect.Left} should coincide. " +
            $"Expected near {expectedLeft}. Caret elements: {DescribeElements(firstVisualLine)}");

        // Both must also be strictly to the right of raw column 0 (the indentation gap).
        Assert.True(caretRect.Left >= expectedLeft - 0.5,
            $"Caret left {caretRect.Left} should be within code text start {expectedLeft}.");
        Assert.True(firstSelectionRect.Left >= expectedLeft - 0.5,
            $"Selection left {firstSelectionRect.Left} should be within code text start {expectedLeft}.");
    }

    [Fact]
    public void CodeBlockSelection_BackwardWordSelectionDoesNotShiftByVisualIndentation()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                "tonalne na obiekt, twarde przejscia",
                "```",
                "after")),
            520,
            240,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var contentLine = document.GetLineByNumber(3);
        var contentText = document.GetText(contentLine);
        var wordStartInLine = contentText.IndexOf("obiekt", StringComparison.Ordinal);
        Assert.True(wordStartInLine >= 0, contentText);
        var wordEndInLine = wordStartInLine + "obiekt".Length;
        var wordStartOffset = contentLine.Offset + wordStartInLine;
        var wordEndOffset = contentLine.Offset + wordEndInLine;
        var visualLine = textView.GetOrConstructVisualLine(contentLine);
        var textLine = Assert.Single(visualLine.TextLines);
        var wordStartVisualColumn = visualLine.GetVisualColumn(wordStartInLine);
        var wordEndVisualColumn = visualLine.GetVisualColumn(wordEndInLine);

        var segment = new SelectionSegment(
            wordEndOffset,
            wordEndVisualColumn,
            wordStartOffset,
            wordStartVisualColumn);
        var selectionRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, segment)
            .ToArray();
        var lineTop = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var selectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - lineTop) < 1.0);
        var expectedLeft = visualLine.GetTextLineVisualXPosition(textLine, wordStartVisualColumn) - textView.ScrollOffset.X;
        var expectedRight = visualLine.GetTextLineVisualXPosition(textLine, wordEndVisualColumn) - textView.ScrollOffset.X;

        Assert.Equal("obiekt", document.GetText(segment));
        Assert.True(
            Math.Abs(selectionRect.Left - expectedLeft) < 0.5,
            $"Selection left {selectionRect.Left} should match word start {expectedLeft}, not shift by visual indentation. " +
            $"Rect: {selectionRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");
        Assert.True(
            Math.Abs(selectionRect.Right - expectedRight) < 0.5,
            $"Selection right {selectionRect.Right} should match word end {expectedRight}, not include trailing characters. " +
            $"Rect: {selectionRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");
    }

    [Fact]
    public void CodeBlockSelection_ForwardWordSelectionCaretMatchesSelectionEnd()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                "tonalne na obiekt, twarde przejscia",
                "```",
                "after")),
            520,
            240,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var contentLine = document.GetLineByNumber(3);
        var contentText = document.GetText(contentLine);
        var wordStartInLine = contentText.IndexOf("obiekt", StringComparison.Ordinal);
        Assert.True(wordStartInLine >= 0, contentText);
        var wordEndInLine = wordStartInLine + "obiekt".Length;
        var wordStartOffset = contentLine.Offset + wordStartInLine;
        var wordEndOffset = contentLine.Offset + wordEndInLine;
        var visualLine = textView.GetOrConstructVisualLine(contentLine);
        var textLine = Assert.Single(visualLine.TextLines);
        var wordStartVisualColumn = visualLine.GetVisualColumn(wordStartInLine);
        var wordEndVisualColumn = visualLine.GetVisualColumn(wordEndInLine);
        var y = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.TextMiddle);
        var startX = visualLine.GetTextLineVisualXPosition(textLine, wordStartVisualColumn);
        var endX = visualLine.GetTextLineVisualXPosition(textLine, wordEndVisualColumn);
        var startPosition = Assert.IsType<TextViewPosition>(textView.GetPosition(new Point(startX, y)));
        var endPosition = Assert.IsType<TextViewPosition>(textView.GetPosition(new Point(endX, y)));

        Assert.Equal(wordStartOffset, document.GetOffset(startPosition.Location));
        Assert.Equal(wordEndOffset, document.GetOffset(endPosition.Location));

        editor.TextArea.Caret.Position = startPosition;
        var oldPosition = editor.TextArea.Caret.Position;
        editor.TextArea.Caret.Position = endPosition;
        editor.TextArea.Selection = editor.TextArea.Selection.StartSelectionOrSetEndpoint(oldPosition, editor.TextArea.Caret.Position);
        textView.EnsureVisualLines();

        var segment = Assert.Single(editor.TextArea.Selection.Segments);
        var selectionRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, segment)
            .ToArray();
        var lineTop = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var selectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - lineTop) < 1.0);
        var caretRect = editor.TextArea.Caret.CalculateCaretRectangle();
        var expectedLeft = visualLine.GetTextLineVisualXPosition(textLine, wordStartVisualColumn) - textView.ScrollOffset.X;
        var expectedRight = visualLine.GetTextLineVisualXPosition(textLine, wordEndVisualColumn) - textView.ScrollOffset.X;

        Assert.Equal("obiekt", editor.TextArea.Selection.GetText());
        Assert.True(
            Math.Abs(selectionRect.Left - expectedLeft) < 0.5,
            $"Selection left {selectionRect.Left} should match word start {expectedLeft}. " +
            $"Rect: {selectionRect}; Caret: {caretRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");
        Assert.True(
            Math.Abs(selectionRect.Right - expectedRight) < 0.5,
            $"Selection right {selectionRect.Right} should match word end {expectedRight}. " +
            $"Rect: {selectionRect}; Caret: {caretRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");
        Assert.True(
            Math.Abs(selectionRect.Right - caretRect.Left) < 0.5,
            $"Forward selection end {selectionRect.Right} should match caret left {caretRect.Left}. " +
            $"Rect: {selectionRect}; Caret: {caretRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");

        editor.TextArea.ClearSelection();
        textView.EnsureVisualLines();
    }

    [Fact]
    public void WrappedCodeBlockSelection_ForwardWordSelectionDoesNotShiftByVisualIndentation()
    {
        EnsureApplication();

        using var colorizer = new MarkdownColorizingTransformer();
        var codeText = "aaaa aaaa aaaa aaaa aaaa aaaa aaaa aaaa obiekt, dalej";
        var editor = CreateEditor(
            new TextDocument(string.Join(
                Environment.NewLine,
                "before",
                "```",
                codeText,
                "```",
                "after")),
            120,
            360,
            colorizer,
            out _);
        var textView = editor.TextArea.TextView;
        var document = editor.Document;
        var contentLine = document.GetLineByNumber(3);
        var wordStartInLine = codeText.IndexOf("obiekt", StringComparison.Ordinal);
        Assert.True(wordStartInLine >= 0, codeText);
        var wordEndInLine = wordStartInLine + "obiekt".Length;
        var wordStartOffset = contentLine.Offset + wordStartInLine;
        var wordEndOffset = contentLine.Offset + wordEndInLine;
        var visualLine = textView.GetOrConstructVisualLine(contentLine);
        Assert.True(visualLine.TextLines.Count > 1, $"Expected wrapped code line. Elements: {DescribeElements(visualLine)}");
        var wordStartVisualColumn = visualLine.GetVisualColumn(wordStartInLine);
        var wordEndVisualColumn = visualLine.GetVisualColumn(wordEndInLine);
        var textLine = visualLine.GetTextLine(wordStartVisualColumn);
        Assert.True(visualLine.TextLines.IndexOf(textLine) > 0, "Expected the selected word to be on a wrapped continuation line.");
        var startPosition = new TextViewPosition(document.GetLocation(wordStartOffset), wordStartVisualColumn);
        var endPosition = new TextViewPosition(document.GetLocation(wordEndOffset), wordEndVisualColumn);

        editor.TextArea.Caret.Position = startPosition;
        var oldPosition = editor.TextArea.Caret.Position;
        editor.TextArea.Caret.Position = endPosition;
        editor.TextArea.Selection = editor.TextArea.Selection.StartSelectionOrSetEndpoint(oldPosition, editor.TextArea.Caret.Position);
        textView.EnsureVisualLines();

        var segment = Assert.Single(editor.TextArea.Selection.Segments);
        var selectionRects = AvaloniaEdit.Rendering.BackgroundGeometryBuilder
            .GetRectsForSegment(textView, segment)
            .ToArray();
        var lineTop = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop)
            - textView.ScrollOffset.Y;
        var selectionRect = Assert.Single(selectionRects, rect => Math.Abs(rect.Top - lineTop) < 1.0);
        var caretRect = editor.TextArea.Caret.CalculateCaretRectangle();
        var expectedLeft = visualLine.GetTextLineVisualXPosition(textLine, wordStartVisualColumn) - textView.ScrollOffset.X;
        var expectedRight = visualLine.GetTextLineVisualXPosition(textLine, wordEndVisualColumn) - textView.ScrollOffset.X;

        Assert.Equal("obiekt", editor.TextArea.Selection.GetText());
        Assert.True(
            Math.Abs(selectionRect.Left - expectedLeft) < 0.5,
            $"Wrapped selection left {selectionRect.Left} should match word start {expectedLeft}. " +
            $"Rect: {selectionRect}; Caret: {caretRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");
        Assert.True(
            Math.Abs(selectionRect.Right - expectedRight) < 0.5,
            $"Wrapped selection right {selectionRect.Right} should match word end {expectedRight}. " +
            $"Rect: {selectionRect}; Caret: {caretRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");
        Assert.True(
            Math.Abs(selectionRect.Right - caretRect.Left) < 0.5,
            $"Wrapped forward selection end {selectionRect.Right} should match caret left {caretRect.Left}. " +
            $"Rect: {selectionRect}; Caret: {caretRect}; Segment: {segment}; Elements: {DescribeElements(visualLine)}");

        editor.TextArea.ClearSelection();
        textView.EnsureVisualLines();
    }

    [Fact]
    public void EditorCaret_UsesTextHeightInsteadOfLineHeight()
    {
        EnsureApplication();

        var editor = new TextEditor
        {
            Document = new TextDocument("hello"),
            WordWrap = true,
            Width = 320,
            Height = 200
        };
        editor.Options.LineHeightFactor = 2.0;
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

        var textView = editor.TextArea.TextView;
        var visualLine = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(1));
        var textLine = Assert.Single(visualLine.TextLines);
        var lineTop = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.LineTop);
        var lineBottom = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.LineBottom);
        var textTop = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.TextTop);
        var textBottom = visualLine.GetTextLineVisualYPosition(textLine, AvaloniaEdit.Rendering.VisualYPosition.TextBottom);

        editor.CaretOffset = 2;
        var caretRect = editor.TextArea.Caret.CalculateCaretRectangle();

        var lineHeight = lineBottom - lineTop;
        var textHeight = textBottom - textTop;
        Assert.True(lineHeight > textHeight + 1.0, $"Line height {lineHeight} should exceed text height {textHeight}.");
        Assert.True(Math.Abs(caretRect.Top - textTop) < 1.0, $"Caret top {caretRect.Top} should match text top {textTop}.");
        Assert.True(Math.Abs(caretRect.Height - textHeight) < 1.0, $"Caret height {caretRect.Height} should match text height {textHeight}.");
        Assert.True(caretRect.Height < lineHeight - 1.0, $"Caret height {caretRect.Height} should be less than line height {lineHeight}.");
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
        lock (ApplicationLock)
        {
            if (_applicationInitialized || Application.Current is not null)
            {
                _applicationInitialized = true;
                return;
            }

            try
            {
                GroundNotes.Program.BuildAvaloniaApp().SetupWithoutStarting();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Setup was already called", StringComparison.Ordinal))
            {
            }

            _applicationInitialized = true;
        }
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

    private static string CreateMultiLineCodeSelectionSample()
    {
        return string.Join(
            Environment.NewLine,
            "before",
            "```",
            "first content line",
            string.Empty,
            "second content line",
            "```",
            "after");
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

    private static string CreateLongScrollSample()
    {
        return string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 80).Select(index => $"Line {index}: scrolled source note body."));
    }

    private static string CreateImageReplacementSample(string relativeImagePath)
    {
        return string.Join(
            Environment.NewLine,
            "# Replacement note",
            string.Empty,
            $"![]({relativeImagePath})|100",
            string.Empty,
            "The replacement note should open at the document start.");
    }

    private static string CreateImageAsset(string baseDirectory, string fileName, int width, int height)
    {
        var imagePath = Path.Combine(baseDirectory, "assets", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, CreatePngBytes(width, height));
        return imagePath;
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

    private static byte[] CreatePngBytes(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        WriteChunk(output, "IHDR", CreateHeaderData(width, height));
        WriteChunk(output, "IDAT", CreateImageData(width, height));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] CreateHeaderData(int width, int height)
    {
        var header = new byte[13];
        WriteInt32BigEndian(header.AsSpan(0, 4), width);
        WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        return header;
    }

    private static byte[] CreateImageData(int width, int height)
    {
        var stride = width * 4 + 1;
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * stride;
            raw[rowStart] = 0;
            for (var x = 0; x < width; x++)
            {
                var pixelStart = rowStart + 1 + x * 4;
                raw[pixelStart] = 255;
                raw[pixelStart + 1] = 255;
                raw[pixelStart + 2] = 255;
                raw[pixelStart + 3] = 255;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string chunkType, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(chunkType);
        var lengthBuffer = new byte[4];
        WriteInt32BigEndian(lengthBuffer, data.Length);
        output.Write(lengthBuffer);
        output.Write(typeBytes);
        output.Write(data);

        var crcBuffer = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcBuffer, 0);
        data.CopyTo(crcBuffer, typeBytes.Length);

        var crcBytes = new byte[4];
        WriteUInt32BigEndian(crcBytes, ComputeCrc32(crcBuffer));
        output.Write(crcBytes);
    }

    private static uint ComputeCrc32(byte[] bytes)
    {
        const uint polynomial = 0xEDB88320u;
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ polynomial;
            }
        }

        return ~crc;
    }

    private static void WriteInt32BigEndian(Span<byte> destination, int value)
    {
        destination[0] = (byte)((value >> 24) & 0xFF);
        destination[1] = (byte)((value >> 16) & 0xFF);
        destination[2] = (byte)((value >> 8) & 0xFF);
        destination[3] = (byte)(value & 0xFF);
    }

    private static void WriteUInt32BigEndian(Span<byte> destination, uint value)
    {
        destination[0] = (byte)((value >> 24) & 0xFF);
        destination[1] = (byte)((value >> 16) & 0xFF);
        destination[2] = (byte)((value >> 8) & 0xFF);
        destination[3] = (byte)(value & 0xFF);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            DirectoryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
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

    private static double GetTextLineRightEdge(AvaloniaEdit.Rendering.VisualLine visualLine, Avalonia.Media.TextFormatting.TextLine textLine)
    {
        var endColumn = visualLine.GetTextLineVisualStartColumn(textLine) + textLine.Length;
        return visualLine.GetTextLineVisualXPosition(textLine, endColumn);
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
