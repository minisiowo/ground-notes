using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Editors;

internal sealed partial class MarkdownColorizingTransformer : DocumentColorizingTransformer
{
    private static readonly Regex s_headingRegex = HeadingRegex();
    private static readonly Regex s_fenceRegex = FenceRegex();
    private static readonly Regex s_blockquoteRegex = BlockquoteRegex();
    private static readonly Regex s_listMarkerRegex = ListMarkerRegex();
    private static readonly Regex s_inlineCodeRegex = InlineCodeRegex();
    private static readonly Regex s_boldRegex = BoldRegex();
    private static readonly Regex s_italicRegex = ItalicRegex();

    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line.Offset, line.Length);
        if (string.IsNullOrEmpty(lineText))
        {
            return;
        }

        if (TryApplyFencedCodeBlock(line, lineText))
        {
            return;
        }

        ApplyHeading(line, lineText);
        ApplyBlockquote(line, lineText);
        ApplyListMarker(line, lineText);
        ApplyEmphasis(line, lineText);
        ApplyInlineCode(line, lineText);
    }

    private void ApplyHeading(DocumentLine line, string lineText)
    {
        var match = s_headingRegex.Match(lineText);
        if (!match.Success)
        {
            return;
        }

        var markerBrush = GetBrush(ThemeKeys.MutedTextBrush);
        var textBrush = match.Groups["marker"].Length switch
        {
            1 => GetBrush(ThemeKeys.MarkdownHeading1Brush),
            2 => GetBrush(ThemeKeys.MarkdownHeading2Brush),
            _ => GetBrush(ThemeKeys.MarkdownHeading3Brush)
        };

        ApplySpan(line.Offset + match.Groups["marker"].Index, line.Offset + match.Groups["marker"].Index + match.Groups["marker"].Length, markerBrush);
        ApplySpan(line.Offset + match.Groups["text"].Index, line.Offset + match.Groups["text"].Index + match.Groups["text"].Length, textBrush, FontWeight.SemiBold);
    }

    private void ApplyBlockquote(DocumentLine line, string lineText)
    {
        var match = s_blockquoteRegex.Match(lineText);
        if (!match.Success)
        {
            return;
        }

        ApplySpan(line.Offset + match.Groups["marker"].Index, line.Offset + match.Groups["marker"].Index + match.Groups["marker"].Length, GetBrush(ThemeKeys.SecondaryTextBrush));
        ApplySpan(line.Offset + match.Groups["text"].Index, line.Offset + match.Groups["text"].Index + match.Groups["text"].Length, GetBrush(ThemeKeys.PrimaryTextBrush));
    }

    private void ApplyListMarker(DocumentLine line, string lineText)
    {
        var match = s_listMarkerRegex.Match(lineText);
        if (!match.Success)
        {
            return;
        }

        ApplySpan(line.Offset + match.Groups["marker"].Index, line.Offset + match.Groups["marker"].Index + match.Groups["marker"].Length, GetBrush(ThemeKeys.MutedTextBrush));
    }

    private void ApplyEmphasis(DocumentLine line, string lineText)
    {
        foreach (Match match in s_boldRegex.Matches(lineText))
        {
            if (!match.Success)
            {
                continue;
            }

            ApplySpan(line.Offset + match.Groups["markerStart"].Index, line.Offset + match.Groups["markerStart"].Index + match.Groups["markerStart"].Length, GetBrush(ThemeKeys.MutedTextBrush));
            ApplySpan(line.Offset + match.Groups["text"].Index, line.Offset + match.Groups["text"].Index + match.Groups["text"].Length, GetBrush(ThemeKeys.PrimaryTextBrush), FontWeight.SemiBold);
            ApplySpan(line.Offset + match.Groups["markerEnd"].Index, line.Offset + match.Groups["markerEnd"].Index + match.Groups["markerEnd"].Length, GetBrush(ThemeKeys.MutedTextBrush));
        }

        foreach (Match match in s_italicRegex.Matches(lineText))
        {
            if (!match.Success)
            {
                continue;
            }

            ApplySpan(line.Offset + match.Groups["markerStart"].Index, line.Offset + match.Groups["markerStart"].Index + match.Groups["markerStart"].Length, GetBrush(ThemeKeys.MutedTextBrush));
            ApplySpan(line.Offset + match.Groups["text"].Index, line.Offset + match.Groups["text"].Index + match.Groups["text"].Length, GetBrush(ThemeKeys.SecondaryTextBrush), fontStyle: FontStyle.Italic);
            ApplySpan(line.Offset + match.Groups["markerEnd"].Index, line.Offset + match.Groups["markerEnd"].Index + match.Groups["markerEnd"].Length, GetBrush(ThemeKeys.MutedTextBrush));
        }
    }

    private void ApplyInlineCode(DocumentLine line, string lineText)
    {
        foreach (Match match in s_inlineCodeRegex.Matches(lineText))
        {
            if (!match.Success)
            {
                continue;
            }

            ApplySpan(line.Offset + match.Groups["markerStart"].Index, line.Offset + match.Groups["markerStart"].Index + match.Groups["markerStart"].Length, GetBrush(ThemeKeys.MutedTextBrush));
            ApplySpan(
                line.Offset + match.Groups["code"].Index,
                line.Offset + match.Groups["code"].Index + match.Groups["code"].Length,
                GetBrush(ThemeKeys.MarkdownInlineCodeForegroundBrush),
                backgroundBrush: GetBrush(ThemeKeys.MarkdownInlineCodeBackgroundBrush),
                fontWeight: GetCodeFontWeight(),
                fontStyle: GetCodeFontStyle(),
                fontFamily: GetCodeFont());
            ApplySpan(line.Offset + match.Groups["markerEnd"].Index, line.Offset + match.Groups["markerEnd"].Index + match.Groups["markerEnd"].Length, GetBrush(ThemeKeys.MutedTextBrush));
        }
    }

    private bool TryApplyFencedCodeBlock(DocumentLine line, string lineText)
    {
        var isInsideFence = IsInsideFence(line);
        var fenceMatch = s_fenceRegex.Match(lineText);
        if (!isInsideFence && !fenceMatch.Success)
        {
            return false;
        }

        ApplySpan(
            line.Offset,
            line.Offset + line.Length,
            GetBrush(ThemeKeys.MarkdownCodeBlockForegroundBrush),
            backgroundBrush: GetBrush(ThemeKeys.MarkdownCodeBlockBackgroundBrush),
            fontWeight: GetCodeFontWeight(),
            fontStyle: GetCodeFontStyle(),
            fontFamily: GetCodeFont());

        if (fenceMatch.Success)
        {
            ApplySpan(
                line.Offset + fenceMatch.Groups["fence"].Index,
                line.Offset + fenceMatch.Groups["fence"].Index + fenceMatch.Groups["fence"].Length,
                GetBrush(ThemeKeys.MutedTextBrush),
                backgroundBrush: GetBrush(ThemeKeys.MarkdownCodeBlockBackgroundBrush),
                fontWeight: GetCodeFontWeight(),
                fontStyle: GetCodeFontStyle(),
                fontFamily: GetCodeFont());
        }

        return true;
    }

    private bool IsInsideFence(DocumentLine line)
    {
        var document = CurrentContext.Document;
        var current = document.GetLineByNumber(1);
        var isInsideFence = false;
        char currentMarker = '\0';
        var currentMarkerLength = 0;

        while (current is not null && current.LineNumber < line.LineNumber)
        {
            var text = document.GetText(current.Offset, current.Length);
            var match = s_fenceRegex.Match(text);
            if (match.Success)
            {
                var marker = match.Groups["fence"].ValueSpan[0];
                var markerLength = match.Groups["fence"].Length;
                if (!isInsideFence)
                {
                    isInsideFence = true;
                    currentMarker = marker;
                    currentMarkerLength = markerLength;
                }
                else if (marker == currentMarker && markerLength >= currentMarkerLength)
                {
                    isInsideFence = false;
                    currentMarker = '\0';
                    currentMarkerLength = 0;
                }
            }

            current = current.NextLine;
        }

        return isInsideFence;
    }

    private void ApplySpan(int startOffset, int endOffset, IBrush? foregroundBrush, FontWeight? fontWeight = null, FontStyle? fontStyle = null, IBrush? backgroundBrush = null, FontFamily? fontFamily = null)
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

            if (fontWeight is null && fontStyle is null && fontFamily is null)
            {
                return;
            }

            var currentTypeface = element.TextRunProperties.Typeface;
            var updatedTypeface = new Typeface(
                fontFamily ?? currentTypeface.FontFamily,
                fontStyle ?? currentTypeface.Style,
                fontWeight ?? currentTypeface.Weight,
                currentTypeface.Stretch);

            element.TextRunProperties.SetTypeface(updatedTypeface);
        });
    }

    private static IBrush? GetBrush(string resourceKey)
    {
        var app = Application.Current;
        return app?.Resources[resourceKey] as IBrush;
    }

    private static FontFamily? GetCodeFont()
    {
        var app = Application.Current;
        return app?.Resources[ThemeKeys.CodeFont] as FontFamily;
    }

    private static FontWeight? GetCodeFontWeight()
    {
        var app = Application.Current;
        return app?.Resources[ThemeKeys.CodeFontWeight] is FontWeight fontWeight ? fontWeight : null;
    }

    private static FontStyle? GetCodeFontStyle()
    {
        var app = Application.Current;
        return app?.Resources[ThemeKeys.CodeFontStyle] is FontStyle fontStyle ? fontStyle : null;
    }

    [GeneratedRegex("^(?<indent>\\s*)(?<fence>(?:`{3,}|~{3,})).*$", RegexOptions.Compiled)]
    private static partial Regex FenceRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>#{1,6})(?<spacing>\\s+)(?<text>.+)$", RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>>+)(?<spacing>\\s*)(?<text>.+)$", RegexOptions.Compiled)]
    private static partial Regex BlockquoteRegex();

    [GeneratedRegex("^(?<indent>\\s*)(?<marker>(?:[-+*]|\\d+[.)])(?:\\s+\\[(?: |x|X)\\])?)(?=\\s)", RegexOptions.Compiled)]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex("(?<markerStart>`)(?<code>[^`\\n]+)(?<markerEnd>`)", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex("(?<markerStart>\\*\\*|__)(?<text>.+?)(?<markerEnd>\\*\\*|__)", RegexOptions.Compiled)]
    private static partial Regex BoldRegex();

    [GeneratedRegex("(?<!\\*|_)(?<markerStart>\\*|_)(?!\\1)(?<text>[^*_\\n]+)(?<markerEnd>\\*|_)(?!\\*|_)", RegexOptions.Compiled)]
    private static partial Regex ItalicRegex();
}
