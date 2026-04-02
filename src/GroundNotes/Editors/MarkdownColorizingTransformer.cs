using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using GroundNotes.Styles;

namespace GroundNotes.Editors;

internal sealed class MarkdownColorizingTransformer : DocumentColorizingTransformer, IDisposable
{
    private readonly MarkdownLineAnalysisCache _analysisCache = new();
    private readonly MarkdownFenceStateTracker _fenceStateTracker = new();
    private readonly MarkdownListContinuationTracker _listContinuationTracker;
    private readonly MarkdownStyleSpanBuffer _spanBuffer = new();
    private ResourceCache? _resourceCache;

    private readonly HashSet<int> _fencedLineNumbers = [];
    private readonly HashSet<int> _suppressedListContinuationLines = [];

    public MarkdownColorizingTransformer()
    {
        _listContinuationTracker = new MarkdownListContinuationTracker(_analysisCache, _fenceStateTracker);
    }

    public event EventHandler<int>? RedrawRequested
    {
        add
        {
            _fenceStateTracker.RedrawRequested += value;
            _listContinuationTracker.RedrawRequested += value;
        }
        remove
        {
            _fenceStateTracker.RedrawRequested -= value;
            _listContinuationTracker.RedrawRequested -= value;
        }
    }

    public bool IsFencedCodeLine(int lineNumber) => _fencedLineNumbers.Contains(lineNumber);

    internal bool QueryIsFencedCodeLine(TextDocument document, int lineNumber)
    {
        var fenceState = _fenceStateTracker.GetStateBeforeLine(document, lineNumber);
        if (fenceState.IsInsideFence)
        {
            return true;
        }

        var line = document.GetLineByNumber(lineNumber);
        var lineText = document.GetText(line.Offset, line.Length);
        if (string.IsNullOrEmpty(lineText))
        {
            return false;
        }

        var analysis = _analysisCache.GetOrAdd(document, lineNumber, lineText, fenceState);
        return analysis.IsFencedCodeLine;
    }

    internal int? QueryWrappedLineContinuationStartColumn(TextDocument document, int lineNumber)
    {
        if (IsListContinuationSuppressed(document, lineNumber))
        {
            return null;
        }

        return _listContinuationTracker.GetContinuationStartColumn(document, lineNumber);
    }

    internal int? QueryInheritedListContinuationStartColumn(TextDocument document, int lineNumber)
    {
        if (_suppressedListContinuationLines.Contains(lineNumber))
        {
            return null;
        }

        var fenceState = _fenceStateTracker.GetStateBeforeLine(document, lineNumber);
        if (fenceState.IsInsideFence)
        {
            return null;
        }

        var line = document.GetLineByNumber(lineNumber);
        var lineText = document.GetText(line.Offset, line.Length);
        if (string.IsNullOrWhiteSpace(lineText))
        {
            return null;
        }

        var analysis = _analysisCache.GetOrAdd(document, lineNumber, lineText, fenceState);
        if (analysis.IsFencedCodeLine || analysis.TaskList is not null || analysis.ListMarker is not null)
        {
            return null;
        }

        return _listContinuationTracker.GetContinuationStartColumn(document, lineNumber);
    }

    internal void SuppressListContinuationForLine(int lineNumber)
    {
        if (lineNumber <= 0)
        {
            return;
        }

        _suppressedListContinuationLines.Add(lineNumber);
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        _spanBuffer.Clear();

        var document = CurrentContext.Document;
        var lineText = document.GetText(line.Offset, line.Length);

        var fenceState = _fenceStateTracker.GetStateBeforeLine(document, line.LineNumber);
        if (string.IsNullOrEmpty(lineText))
        {
            if (fenceState.IsInsideFence)
            {
                _fencedLineNumbers.Add(line.LineNumber);
            }
            else
            {
                _fencedLineNumbers.Remove(line.LineNumber);
            }

            return;
        }

        var analysis = _analysisCache.GetOrAdd(document, line.LineNumber, lineText, fenceState);
        if (analysis.IsFencedCodeLine)
        {
            _fencedLineNumbers.Add(line.LineNumber);
            ApplyFencedCodeBlock(line, analysis);
            FlushSpans();
            return;
        }

        _fencedLineNumbers.Remove(line.LineNumber);

        ApplyHeading(line, analysis.Heading);
        if (ApplyHorizontalRule(line, analysis.HorizontalRule))
        {
            return;
        }

        ApplyBlockquote(line, analysis.Blockquote);
        ApplyTaskList(line, analysis.TaskList);
        ApplyListMarker(line, analysis.ListMarker);
        ApplyImages(line, analysis.Images);
        ApplyLinks(line, analysis.Links);
        ApplyBareUrls(line, analysis.BareUrls);
        var resources = GetResources();
        ApplyDelimitedSpans(line, analysis.StrikethroughSpans, resources.MarkdownStrikethroughBrush, markerBrush: resources.MutedTextBrush, textDecorations: TextDecorations.Strikethrough);
        ApplyDelimitedSpans(line, analysis.BoldSpans, resources.PrimaryTextBrush, markerBrush: resources.MutedTextBrush, fontWeight: FontWeight.SemiBold);
        ApplyDelimitedSpans(line, analysis.ItalicSpans, resources.SecondaryTextBrush, markerBrush: resources.MutedTextBrush, fontStyle: FontStyle.Italic);
        ApplyInlineCode(line, analysis.InlineCodeSpans);
        FlushSpans();
    }

    public void Dispose()
    {
        _listContinuationTracker.Dispose();
        _analysisCache.Dispose();
        _fenceStateTracker.Dispose();
    }

    public void InvalidateResourceCache()
    {
        _resourceCache = null;
    }

    private bool IsListContinuationSuppressed(TextDocument document, int lineNumber)
    {
        if (!_suppressedListContinuationLines.Contains(lineNumber))
        {
            return false;
        }

        var fenceState = _fenceStateTracker.GetStateBeforeLine(document, lineNumber);
        if (fenceState.IsInsideFence)
        {
            return true;
        }

        var line = document.GetLineByNumber(lineNumber);
        var lineText = document.GetText(line.Offset, line.Length);
        if (string.IsNullOrEmpty(lineText))
        {
            return true;
        }

        var analysis = _analysisCache.GetOrAdd(document, lineNumber, lineText, fenceState);
        return analysis.TaskList is null && analysis.ListMarker is null;
    }

    private bool ApplyHorizontalRule(DocumentLine line, MarkdownRange? rule)
    {
        if (rule is null)
        {
            return false;
        }

        var resources = GetResources();
        QueueSpan(line.Offset + rule.Value.Start, line.Offset + rule.Value.End, resources.MarkdownRuleBrush, fontWeight: FontWeight.SemiBold);
        return true;
    }

    private void ApplyHeading(DocumentLine line, MarkdownHeadingMatch? heading)
    {
        if (heading is null)
        {
            return;
        }

        var resources = GetResources();
        var markerBrush = resources.MutedTextBrush;
        var textBrush = heading.Value.Level switch
        {
            1 => resources.MarkdownHeading1Brush,
            2 => resources.MarkdownHeading2Brush,
            _ => resources.MarkdownHeading3Brush
        };
        var fontWeight = heading.Value.Level switch
        {
            1 => FontWeight.Bold,
            2 => FontWeight.SemiBold,
            3 => FontWeight.Medium,
            _ => FontWeight.Normal
        };

        QueueSpan(line.Offset + heading.Value.Marker.Start, line.Offset + heading.Value.Marker.End, markerBrush);
        QueueSpan(line.Offset + heading.Value.Text.Start, line.Offset + heading.Value.Text.End, textBrush, fontWeight: fontWeight);

        if (heading.Value.Closing is { } closing)
        {
            QueueSpan(line.Offset + closing.Start, line.Offset + closing.End, markerBrush);
        }
    }

    private void ApplyBlockquote(DocumentLine line, MarkdownBlockquoteMatch? blockquote)
    {
        if (blockquote is null)
        {
            return;
        }

        var resources = GetResources();
        QueueSpan(line.Offset + blockquote.Value.Marker.Start, line.Offset + blockquote.Value.Marker.End, GetBlockquoteMarkerBrush(resources, blockquote.Value.Depth));
        QueueSpan(line.Offset + blockquote.Value.Text.Start, line.Offset + blockquote.Value.Text.End, GetBlockquoteTextBrush(resources, blockquote.Value.Depth));
    }

    private void ApplyTaskList(DocumentLine line, MarkdownListMatch? taskList)
    {
        if (taskList is null || taskList.Value.Checkbox is null)
        {
            return;
        }

        var resources = GetResources();
        var checkboxBrush = taskList.Value.IsChecked ? resources.MarkdownTaskDoneBrush : resources.MarkdownTaskPendingBrush;
        QueueSpan(line.Offset + taskList.Value.Marker.Start, line.Offset + taskList.Value.Marker.End, GetListMarkerBrush(resources, taskList.Value.IndentLength), fontWeight: GetListMarkerWeight(taskList.Value.IndentLength));

        var checkbox = taskList.Value.Checkbox.Value;
        QueueSpan(line.Offset + checkbox.Start, line.Offset + checkbox.End, checkboxBrush, fontWeight: GetListMarkerWeight(taskList.Value.IndentLength));

        if (taskList.Value.IsChecked && taskList.Value.Text is { } text)
        {
            QueueSpan(line.Offset + text.Start, line.Offset + text.End, resources.MarkdownTaskDoneBrush);
        }
    }

    private void ApplyListMarker(DocumentLine line, MarkdownListMatch? listMarker)
    {
        if (listMarker is null)
        {
            return;
        }

        var resources = GetResources();
        QueueSpan(line.Offset + listMarker.Value.Marker.Start, line.Offset + listMarker.Value.Marker.End, GetListMarkerBrush(resources, listMarker.Value.IndentLength), fontWeight: GetListMarkerWeight(listMarker.Value.IndentLength));
    }

    private void ApplyLinks(DocumentLine line, IReadOnlyList<MarkdownLinkMatch> links)
    {
        var resources = GetResources();

        foreach (var link in links)
        {
            QueueSpan(line.Offset + link.OpenBracket.Start, line.Offset + link.OpenBracket.End, resources.MutedTextBrush);
            QueueSpan(line.Offset + link.Label.Start, line.Offset + link.Label.End, resources.MarkdownLinkLabelBrush);
            QueueSpan(line.Offset + link.CloseBracket.Start, line.Offset + link.CloseBracket.End, resources.MutedTextBrush);
            QueueSpan(line.Offset + link.OpenParen.Start, line.Offset + link.OpenParen.End, resources.MutedTextBrush);
            QueueSpan(line.Offset + link.Url.Start, line.Offset + link.Url.End, resources.MarkdownLinkUrlBrush);
            QueueSpan(line.Offset + link.CloseParen.Start, line.Offset + link.CloseParen.End, resources.MutedTextBrush);
        }
    }

    private void ApplyImages(DocumentLine line, IReadOnlyList<MarkdownImageMatch> images)
    {
        var resources = GetResources();

        foreach (var image in images)
        {
            QueueSpan(line.Offset + image.Bang.Start, line.Offset + image.Bang.End, resources.MutedTextBrush);
            QueueSpan(line.Offset + image.OpenBracket.Start, line.Offset + image.OpenBracket.End, resources.MutedTextBrush);

            if (image.AltText.Length > 0)
            {
                QueueSpan(line.Offset + image.AltText.Start, line.Offset + image.AltText.End, resources.MarkdownLinkLabelBrush);
            }

            QueueSpan(line.Offset + image.CloseBracket.Start, line.Offset + image.CloseBracket.End, resources.MutedTextBrush);
            QueueSpan(line.Offset + image.OpenParen.Start, line.Offset + image.OpenParen.End, resources.MutedTextBrush);
            QueueSpan(line.Offset + image.Url.Start, line.Offset + image.Url.End, resources.MarkdownLinkUrlBrush);
            QueueSpan(line.Offset + image.CloseParen.Start, line.Offset + image.CloseParen.End, resources.MutedTextBrush);

            if (image.ScalePipe is { } scalePipe)
            {
                QueueSpan(line.Offset + scalePipe.Start, line.Offset + scalePipe.End, resources.MutedTextBrush);
            }

            if (image.ScaleValue is { } scaleValue)
            {
                QueueSpan(line.Offset + scaleValue.Start, line.Offset + scaleValue.End, resources.MarkdownLinkUrlBrush);
            }
        }
    }

    private void ApplyBareUrls(DocumentLine line, IReadOnlyList<MarkdownRange> bareUrls)
    {
        var resources = GetResources();

        foreach (var bareUrl in bareUrls)
        {
            QueueSpan(line.Offset + bareUrl.Start, line.Offset + bareUrl.End, resources.MarkdownLinkUrlBrush);
        }
    }

    private void ApplyDelimitedSpans(DocumentLine line, IReadOnlyList<MarkdownDelimitedSpan> spans, IBrush? textBrush, IBrush? markerBrush = null, FontWeight? fontWeight = null, FontStyle? fontStyle = null, TextDecorationCollection? textDecorations = null)
    {
        foreach (var span in spans)
        {
            QueueSpan(line.Offset + span.MarkerStart.Start, line.Offset + span.MarkerStart.End, markerBrush);
            QueueSpan(line.Offset + span.Text.Start, line.Offset + span.Text.End, textBrush, fontWeight: fontWeight, fontStyle: fontStyle, textDecorations: textDecorations);
            QueueSpan(line.Offset + span.MarkerEnd.Start, line.Offset + span.MarkerEnd.End, markerBrush);
        }
    }

    private void ApplyInlineCode(DocumentLine line, IReadOnlyList<MarkdownDelimitedSpan> inlineCodeSpans)
    {
        var resources = GetResources();

        foreach (var span in inlineCodeSpans)
        {
            QueueSpan(line.Offset + span.MarkerStart.Start, line.Offset + span.MarkerStart.End, resources.MutedTextBrush);
            QueueSpan(
                line.Offset + span.Text.Start,
                line.Offset + span.Text.End,
                resources.MarkdownInlineCodeForegroundBrush,
                backgroundBrush: resources.MarkdownInlineCodeBackgroundBrush,
                typeface: resources.CodeTypeface);
            QueueSpan(line.Offset + span.MarkerEnd.Start, line.Offset + span.MarkerEnd.End, resources.MutedTextBrush);
        }
    }

    private void ApplyFencedCodeBlock(DocumentLine line, MarkdownLineAnalysis analysis)
    {
        var resources = GetResources();
        QueueSpan(
            line.Offset,
            line.Offset + line.Length,
            resources.MarkdownCodeBlockForegroundBrush,
            typeface: resources.CodeTypeface);

        if (analysis.Fence is not { } fence)
        {
            return;
        }

        QueueSpan(
            line.Offset + fence.Fence.Start,
            line.Offset + fence.Fence.End,
            resources.MarkdownFenceMarkerBrush,
            typeface: resources.CodeTypeface);

        if (fence.Info is { } info)
        {
            QueueSpan(
                line.Offset + info.Start,
                line.Offset + info.End,
                resources.MarkdownFenceInfoBrush,
                typeface: resources.CodeTypeface);
        }
    }

    private void QueueSpan(int startOffset, int endOffset, IBrush? foregroundBrush, FontWeight? fontWeight = null, FontStyle? fontStyle = null, IBrush? backgroundBrush = null, FontFamily? fontFamily = null, TextDecorationCollection? textDecorations = null, Typeface? typeface = null)
    {
        _spanBuffer.Add(startOffset, endOffset, foregroundBrush, fontWeight, fontStyle, backgroundBrush, fontFamily, textDecorations, typeface);
    }

    private void FlushSpans()
    {
        _spanBuffer.Apply(ApplySpan);
    }

    private void ApplySpan(MarkdownStyleSpan span)
    {
        ApplySpan(span.StartOffset, span.EndOffset, span.ForegroundBrush, span.FontWeight, span.FontStyle, span.BackgroundBrush, span.FontFamily, span.TextDecorations, span.Typeface);
    }

    private void ApplySpan(int startOffset, int endOffset, IBrush? foregroundBrush, FontWeight? fontWeight = null, FontStyle? fontStyle = null, IBrush? backgroundBrush = null, FontFamily? fontFamily = null, TextDecorationCollection? textDecorations = null, Typeface? typeface = null)
    {
        if (endOffset <= startOffset)
        {
            return;
        }

        ChangeLinePart(startOffset, endOffset, element =>
        {
            if (foregroundBrush is not null)
            {
                element.TextRunProperties.SetForegroundBrush(foregroundBrush);
            }

            if (backgroundBrush is not null)
            {
                element.BackgroundBrush = backgroundBrush;
            }

            if (textDecorations is not null)
            {
                element.TextRunProperties.SetTextDecorations(textDecorations);
            }

            if (fontWeight is null && fontStyle is null && fontFamily is null && typeface is null)
            {
                return;
            }

            var updatedTypeface = typeface;
            if (updatedTypeface is null)
            {
                var currentTypeface = element.TextRunProperties.Typeface;
                updatedTypeface = new Typeface(
                    fontFamily ?? currentTypeface.FontFamily,
                    fontStyle ?? currentTypeface.Style,
                    fontWeight ?? currentTypeface.Weight,
                    currentTypeface.Stretch);
            }

            if (updatedTypeface is Typeface resolvedTypeface)
            {
                element.TextRunProperties.SetTypeface(resolvedTypeface);
            }
        });
    }

    private static IBrush? GetBrush(string resourceKey)
    {
        var app = Application.Current;
        return app?.Resources[resourceKey] as IBrush;
    }

    private ResourceCache GetResources() => _resourceCache ??= ResourceCache.Create();

    private static IBrush? GetBlockquoteMarkerBrush(ResourceCache resources, int depth) => depth switch
    {
        <= 1 => resources.MarkdownBlockquoteBrush,
        2 => resources.SecondaryTextBrush,
        _ => resources.MutedTextBrush
    };

    private static IBrush? GetBlockquoteTextBrush(ResourceCache resources, int depth) => depth switch
    {
        <= 1 => resources.MarkdownBlockquoteBrush,
        2 => resources.SecondaryTextBrush,
        _ => resources.PrimaryTextBrush
    };

    private static IBrush? GetListMarkerBrush(ResourceCache resources, int indentLength) => GetListDepth(indentLength) switch
    {
        0 => resources.MutedTextBrush,
        1 => resources.SecondaryTextBrush,
        _ => resources.MarkdownBlockquoteBrush
    };

    private static FontWeight GetListMarkerWeight(int indentLength) => GetListDepth(indentLength) switch
    {
        0 => FontWeight.Normal,
        1 => FontWeight.Medium,
        _ => FontWeight.SemiBold
    };

    private static int GetListDepth(int indentLength) => indentLength switch
    {
        <= 0 => 0,
        <= 3 => 1,
        <= 7 => 2,
        _ => 3
    };

    private sealed class ResourceCache
    {
        public required IBrush? MarkdownRuleBrush { get; init; }
        public required IBrush? MutedTextBrush { get; init; }
        public required IBrush? PrimaryTextBrush { get; init; }
        public required IBrush? SecondaryTextBrush { get; init; }
        public required IBrush? MarkdownHeading1Brush { get; init; }
        public required IBrush? MarkdownHeading2Brush { get; init; }
        public required IBrush? MarkdownHeading3Brush { get; init; }
        public required IBrush? MarkdownBlockquoteBrush { get; init; }
        public required IBrush? MarkdownTaskDoneBrush { get; init; }
        public required IBrush? MarkdownTaskPendingBrush { get; init; }
        public required IBrush? MarkdownLinkLabelBrush { get; init; }
        public required IBrush? MarkdownLinkUrlBrush { get; init; }
        public required IBrush? MarkdownStrikethroughBrush { get; init; }
        public required IBrush? MarkdownInlineCodeForegroundBrush { get; init; }
        public required IBrush? MarkdownInlineCodeBackgroundBrush { get; init; }
        public required IBrush? MarkdownCodeBlockForegroundBrush { get; init; }
        public required IBrush? MarkdownCodeBlockBackgroundBrush { get; init; }
        public required IBrush? MarkdownFenceMarkerBrush { get; init; }
        public required IBrush? MarkdownFenceInfoBrush { get; init; }
        public required FontFamily? CodeFont { get; init; }
        public required FontWeight? CodeFontWeight { get; init; }
        public required FontStyle? CodeFontStyle { get; init; }
        public required Typeface? CodeTypeface { get; init; }

        public static ResourceCache Create()
        {
            var app = Application.Current;
            var codeFont = app?.Resources[ThemeKeys.CodeFont] as FontFamily;
            FontWeight? codeFontWeight = app?.Resources[ThemeKeys.CodeFontWeight] is FontWeight fontWeight ? fontWeight : null;
            FontStyle? codeFontStyle = app?.Resources[ThemeKeys.CodeFontStyle] is FontStyle fontStyle ? fontStyle : null;

            return new ResourceCache
            {
                MarkdownRuleBrush = app?.Resources[ThemeKeys.MarkdownRuleBrush] as IBrush,
                MutedTextBrush = app?.Resources[ThemeKeys.MutedTextBrush] as IBrush,
                PrimaryTextBrush = app?.Resources[ThemeKeys.PrimaryTextBrush] as IBrush,
                SecondaryTextBrush = app?.Resources[ThemeKeys.SecondaryTextBrush] as IBrush,
                MarkdownHeading1Brush = app?.Resources[ThemeKeys.MarkdownHeading1Brush] as IBrush,
                MarkdownHeading2Brush = app?.Resources[ThemeKeys.MarkdownHeading2Brush] as IBrush,
                MarkdownHeading3Brush = app?.Resources[ThemeKeys.MarkdownHeading3Brush] as IBrush,
                MarkdownBlockquoteBrush = app?.Resources[ThemeKeys.MarkdownBlockquoteBrush] as IBrush,
                MarkdownTaskDoneBrush = app?.Resources[ThemeKeys.MarkdownTaskDoneBrush] as IBrush,
                MarkdownTaskPendingBrush = app?.Resources[ThemeKeys.MarkdownTaskPendingBrush] as IBrush,
                MarkdownLinkLabelBrush = app?.Resources[ThemeKeys.MarkdownLinkLabelBrush] as IBrush,
                MarkdownLinkUrlBrush = app?.Resources[ThemeKeys.MarkdownLinkUrlBrush] as IBrush,
                MarkdownStrikethroughBrush = app?.Resources[ThemeKeys.MarkdownStrikethroughBrush] as IBrush,
                MarkdownInlineCodeForegroundBrush = app?.Resources[ThemeKeys.MarkdownInlineCodeForegroundBrush] as IBrush,
                MarkdownInlineCodeBackgroundBrush = app?.Resources[ThemeKeys.MarkdownInlineCodeBackgroundBrush] as IBrush,
                MarkdownCodeBlockForegroundBrush = app?.Resources[ThemeKeys.MarkdownCodeBlockForegroundBrush] as IBrush,
                MarkdownCodeBlockBackgroundBrush = app?.Resources[ThemeKeys.MarkdownCodeBlockBackgroundBrush] as IBrush,
                MarkdownFenceMarkerBrush = app?.Resources[ThemeKeys.MarkdownFenceMarkerBrush] as IBrush,
                MarkdownFenceInfoBrush = app?.Resources[ThemeKeys.MarkdownFenceInfoBrush] as IBrush,
                CodeFont = codeFont,
                CodeFontWeight = codeFontWeight,
                CodeFontStyle = codeFontStyle,
                CodeTypeface = codeFont is null || codeFontWeight is null || codeFontStyle is null ? null : new Typeface(codeFont, codeFontStyle.Value, codeFontWeight.Value),
            };
        }
    }
}
