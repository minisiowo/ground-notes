namespace GroundNotes.Editors;

internal static class MarkdownImageEditingCommands
{
    public static MarkdownEditResult RenameImageUrl(string text, int urlStart, int urlLength, string newMarkdownPath)
    {
        var start = Math.Clamp(urlStart, 0, text.Length);
        var length = Math.Clamp(urlLength, 0, text.Length - start);
        return new MarkdownEditResult(start, length, newMarkdownPath, start + newMarkdownPath.Length, 0);
    }

    public static MarkdownEditResult DeleteImageReference(string text, int referenceStart, int referenceLength)
    {
        var start = Math.Clamp(referenceStart, 0, text.Length);
        var length = Math.Clamp(referenceLength, 0, text.Length - start);
        var lineStart = FindLineStart(text, start);
        var lineEnd = FindLineEnd(text, start + length);
        var lineText = text[lineStart..lineEnd];
        var referenceText = text[start..(start + length)];

        if (string.Equals(lineText.Trim(), referenceText, StringComparison.Ordinal))
        {
            var deleteStart = lineStart;
            var deleteEnd = lineEnd;
            if (deleteEnd < text.Length)
            {
                deleteEnd = IncludeFollowingLineBreak(text, deleteEnd);
            }
            else if (deleteStart > 0)
            {
                deleteStart = IncludePreviousLineBreak(text, deleteStart);
            }

            return new MarkdownEditResult(deleteStart, deleteEnd - deleteStart, string.Empty, deleteStart, 0);
        }

        return new MarkdownEditResult(start, length, string.Empty, start, 0);
    }

    private static int FindLineStart(string text, int offset)
    {
        var index = Math.Clamp(offset, 0, text.Length);
        while (index > 0 && text[index - 1] is not ('\n' or '\r'))
        {
            index--;
        }

        return index;
    }

    private static int FindLineEnd(string text, int offset)
    {
        var index = Math.Clamp(offset, 0, text.Length);
        while (index < text.Length && text[index] is not ('\n' or '\r'))
        {
            index++;
        }

        return index;
    }

    private static int IncludeFollowingLineBreak(string text, int offset)
    {
        if (offset < text.Length && text[offset] == '\r')
        {
            offset++;
        }

        if (offset < text.Length && text[offset] == '\n')
        {
            offset++;
        }

        return offset;
    }

    private static int IncludePreviousLineBreak(string text, int offset)
    {
        if (offset > 0 && text[offset - 1] == '\n')
        {
            offset--;
        }

        if (offset > 0 && text[offset - 1] == '\r')
        {
            offset--;
        }

        return offset;
    }
}
