using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using GroundNotes.Styles;

namespace GroundNotes.Editors;

internal sealed class MarkdownTableColorizingTransformer : DocumentColorizingTransformer
{
    private readonly MarkdownTablePresentationIndex _tableIndex;

    public MarkdownTableColorizingTransformer(MarkdownTablePresentationIndex tableIndex)
    {
        _tableIndex = tableIndex;
    }



    protected override void ColorizeLine(DocumentLine line)
    {
        var document = CurrentContext.Document;
        var table = _tableIndex.GetTableForLine(document, line.LineNumber);
        if (table is null)
        {
            return;
        }

        var row = table.Rows.FirstOrDefault(candidate => candidate.Start == line.Offset);
        if (row is null)
        {
            return;
        }

        var tableFontFamily = GetTableFontFamily();
        ApplyStyle(line.Offset, line.EndOffset, null, null, tableFontFamily);

        if (row.IsDelimiter)
        {
            ApplyStyle(line.Offset, line.EndOffset, GetBrush(ThemeKeys.MarkdownRuleBrush), FontWeight.SemiBold, tableFontFamily);
            return;
        }

        if (row.Index == 0)
        {
            ApplyStyle(line.Offset, line.EndOffset, null, FontWeight.SemiBold, tableFontFamily);
        }

        var lineText = document.GetText(line.Offset, line.Length);
        var mutedBrush = GetBrush(ThemeKeys.MutedTextBrush);
        for (var i = 0; i < lineText.Length; i++)
        {
            if (lineText[i] == '|' && !IsEscaped(lineText, i))
            {
                ApplyStyle(line.Offset + i, line.Offset + i + 1, mutedBrush, null);
            }
        }
    }



    private void ApplyStyle(int start, int end, IBrush? foreground, FontWeight? weight, FontFamily? fontFamily = null)
    {
        if (end <= start)
        {
            return;
        }

        ChangeLinePart(start, end, element =>
        {
            if (foreground is not null)
            {
                element.TextRunProperties.SetForegroundBrush(foreground);
            }

            if (fontFamily is null && weight is null)
            {
                return;
            }

            var current = element.TextRunProperties.Typeface;
            element.TextRunProperties.SetTypeface(new Typeface(
                fontFamily ?? current.FontFamily,
                current.Style,
                weight ?? current.Weight,
                current.Stretch));
        });
    }

    private static FontFamily? GetTableFontFamily()
        => Application.Current?.Resources[ThemeKeys.TerminalFont] as FontFamily;

    private static bool IsEscaped(string text, int index)
    {
        var backslashes = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 != 0;
    }

    private static IBrush? GetBrush(string key) => Application.Current?.Resources[key] as IBrush;
}
