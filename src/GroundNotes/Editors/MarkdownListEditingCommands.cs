namespace GroundNotes.Editors;

internal static class MarkdownListEditingCommands
{
    public static MarkdownEditResult ChangeIndentation(string text, int selectionStart, int selectionLength, int indentationSize, bool unindent)
    {
        var indentSize = Math.Max(1, indentationSize);
        var start = Clamp(selectionStart, 0, text.Length);
        var length = Clamp(selectionLength, 0, text.Length - start);
        var end = start + length;

        if (start > end)
        {
            (start, end) = (end, start);
            length = end - start;
        }

        if (length == 0)
        {
            var lineBounds = GetLineBounds(text, start);
            var lineText = text[lineBounds.Start..lineBounds.End];
            if (TryParseListLine(lineText, out var listLine))
            {
                var adjustedIndent = unindent
                    ? Math.Max(0, listLine.IndentLength - indentSize)
                    : listLine.IndentLength + indentSize;
                var replacement = RebuildListLine(listLine, adjustedIndent, listLine.TextContent);
                var caretOffset = lineBounds.Start + Math.Min(start - lineBounds.Start + (adjustedIndent - listLine.IndentLength), replacement.Length);
                return new MarkdownEditResult(lineBounds.Start, lineBounds.End - lineBounds.Start, replacement, Math.Max(lineBounds.Start, caretOffset), 0);
            }

            var lineReplacement = TransformLineIndentation(lineText, indentSize, unindent);
            var prefixDelta = GetLeadingWhitespaceColumns(lineReplacement, indentSize) - GetLeadingWhitespaceColumns(lineText, indentSize);
            var caretInLine = start - lineBounds.Start;
            var newCaretOffset = lineBounds.Start + Math.Max(0, caretInLine + prefixDelta);
            return new MarkdownEditResult(
                lineBounds.Start,
                lineBounds.End - lineBounds.Start,
                lineReplacement,
                Math.Min(lineBounds.Start + lineReplacement.Length, newCaretOffset),
                0);
        }

        var blockBounds = GetBlockBounds(text, start, end);
        var rawLines = text[blockBounds.Start..blockBounds.End].Split('\n');
        var transformedLines = new string[rawLines.Length];

        for (var i = 0; i < rawLines.Length; i++)
        {
            transformedLines[i] = TransformLineIndentation(rawLines[i], indentSize, unindent);
        }

        var replacementText = string.Join('\n', transformedLines);
        var delta = replacementText.Length - (blockBounds.End - blockBounds.Start);
        var newSelectionStart = Math.Max(blockBounds.Start, start + GetFirstLineSelectionDelta(rawLines[0], transformedLines[0], start - blockBounds.Start));
        var newSelectionLength = Math.Max(0, length + delta);
        return new MarkdownEditResult(blockBounds.Start, blockBounds.End - blockBounds.Start, replacementText, newSelectionStart, newSelectionLength);
    }

    public static bool TryInsertListItemBreak(string text, int caretOffset, int selectionLength, int indentationSize, out MarkdownEditResult result)
    {
        result = default;

        if (selectionLength != 0)
        {
            return false;
        }

        var caret = Clamp(caretOffset, 0, text.Length);
        var lineBounds = GetLineBounds(text, caret);
        var lineText = text[lineBounds.Start..lineBounds.End];
        if (!TryParseListLine(lineText, out var listLine))
        {
            return false;
        }

        var caretInLine = caret - lineBounds.Start;
        var splitColumn = Math.Max(caretInLine, listLine.ContentStartColumn);
        var beforeText = splitColumn <= listLine.ContentStartColumn ? string.Empty : lineText[listLine.ContentStartColumn..splitColumn];
        var afterText = splitColumn >= lineText.Length ? string.Empty : lineText[splitColumn..];
        if (afterText.Length > 0 && afterText[0] == ' ')
        {
            afterText = afterText[1..];
        }

        var currentLine = RebuildListLine(listLine, listLine.IndentLength, beforeText);
        var nextLine = RebuildNextListLine(listLine, afterText);
        var replacement = currentLine + "\n" + nextLine;
        var nextCaretOffset = lineBounds.Start + currentLine.Length + 1 + GetPrefixLength(nextLine);

        result = new MarkdownEditResult(
            lineBounds.Start,
            lineBounds.End - lineBounds.Start,
            replacement,
            nextCaretOffset,
            0);
        return true;
    }

    public static bool TryBackspaceListIndentation(string text, int caretOffset, int selectionLength, int indentationSize, out MarkdownEditResult result)
    {
        result = default;

        if (selectionLength != 0)
        {
            return false;
        }

        var caret = Clamp(caretOffset, 0, text.Length);
        var lineBounds = GetLineBounds(text, caret);
        var lineText = text[lineBounds.Start..lineBounds.End];
        if (!TryParseListLine(lineText, out var listLine))
        {
            return false;
        }

        var caretInLine = caret - lineBounds.Start;
        if (string.IsNullOrEmpty(listLine.TextContent) && caretInLine == listLine.ContentStartColumn)
        {
            if (listLine.IndentLength > 0)
            {
                var nestedReplacement = new string(' ', listLine.IndentLength);
                var nestedCaretOffset = lineBounds.Start + nestedReplacement.Length;
                result = new MarkdownEditResult(
                    lineBounds.Start,
                    lineBounds.End - lineBounds.Start,
                    nestedReplacement,
                    nestedCaretOffset,
                    0);
                return true;
            }

            var emptyItemReplacement = string.Empty;
            var emptyItemCaretOffset = lineBounds.Start;
            result = new MarkdownEditResult(
                lineBounds.Start,
                lineBounds.End - lineBounds.Start,
                emptyItemReplacement,
                emptyItemCaretOffset,
                0);
            return true;
        }

        return false;
    }

    public static bool ShouldSuppressBackspaceInListPrefix(string text, int caretOffset, int selectionLength)
    {
        if (selectionLength != 0)
        {
            return false;
        }

        var caret = Clamp(caretOffset, 0, text.Length);
        var lineBounds = GetLineBounds(text, caret);
        var lineText = text[lineBounds.Start..lineBounds.End];
        if (!TryParseListLine(lineText, out var listLine) || string.IsNullOrEmpty(listLine.TextContent))
        {
            return false;
        }

        var caretInLine = caret - lineBounds.Start;
        return caretInLine <= listLine.ContentStartColumn;
    }

    private static string TransformLineIndentation(string lineText, int indentationSize, bool unindent)
    {
        if (TryParseListLine(lineText, out var listLine))
        {
            var adjustedIndent = unindent
                ? Math.Max(0, listLine.IndentLength - indentationSize)
                : listLine.IndentLength + indentationSize;
            return RebuildListLine(listLine, adjustedIndent, listLine.TextContent);
        }

        if (unindent)
        {
            var removed = 0;
            while (removed < indentationSize && removed < lineText.Length && lineText[removed] == ' ')
            {
                removed++;
            }

            return removed == 0 ? lineText : lineText[removed..];
        }

        return new string(' ', indentationSize) + lineText;
    }

    private static int GetFirstLineSelectionDelta(string originalLine, string transformedLine, int offsetWithinBlock)
    {
        var originalPrefix = GetLeadingWhitespaceColumns(originalLine, indentationSize: 4);
        var transformedPrefix = GetLeadingWhitespaceColumns(transformedLine, indentationSize: 4);
        return transformedPrefix - originalPrefix;
    }

    private static int GetLeadingWhitespaceColumns(string lineText, int indentationSize)
    {
        var columns = 0;

        foreach (var ch in lineText)
        {
            if (ch == ' ')
            {
                columns++;
                continue;
            }

            if (ch == '\t')
            {
                columns += Math.Max(indentationSize, 1);
                continue;
            }

            break;
        }

        return columns;
    }

    private static (int Start, int End) GetLineBounds(string text, int offset)
    {
        var clampedOffset = Clamp(offset, 0, text.Length);
        var lineStart = text.LastIndexOf('\n', Math.Max(clampedOffset - 1, 0));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var lineEnd = clampedOffset < text.Length ? text.IndexOf('\n', clampedOffset) : -1;
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return (lineStart, lineEnd);
    }

    private static (int Start, int End) GetBlockBounds(string text, int start, int end)
    {
        var blockStart = text.LastIndexOf('\n', Math.Max(start - 1, 0));
        blockStart = blockStart < 0 ? 0 : blockStart + 1;

        var blockEnd = end < text.Length ? text.IndexOf('\n', end) : -1;
        if (blockEnd < 0)
        {
            blockEnd = text.Length;
        }

        return (blockStart, blockEnd);
    }

    private static bool TryParseListLine(string lineText, out ListLineInfo listLine)
    {
        listLine = default;
        if (string.IsNullOrEmpty(lineText))
        {
            return false;
        }

        var analysis = MarkdownLineParser.Analyze(lineText, MarkdownFenceState.None);
        if (analysis.TaskList is { } taskList)
        {
            var textStart = taskList.Text?.Start ?? lineText.Length;
            listLine = new ListLineInfo(
                ListKind.Task,
                taskList.IndentLength,
                lineText[taskList.Marker.Start..taskList.Marker.End],
                taskList.IsChecked,
                null,
                '\0',
                textStart,
                textStart < lineText.Length ? lineText[textStart..] : string.Empty);
            return true;
        }

        if (analysis.ListMarker is { } listMarker)
        {
            var markerText = lineText[listMarker.Marker.Start..listMarker.Marker.End];
            var textStart = listMarker.Text?.Start ?? lineText.Length;
            if (TryParseOrderedMarker(markerText, out var number, out var delimiter))
            {
                listLine = new ListLineInfo(
                    ListKind.Ordered,
                    listMarker.IndentLength,
                    markerText,
                    false,
                    number,
                    delimiter,
                    textStart,
                    textStart < lineText.Length ? lineText[textStart..] : string.Empty);
                return true;
            }

            listLine = new ListLineInfo(
                ListKind.Bullet,
                listMarker.IndentLength,
                markerText,
                false,
                null,
                '\0',
                textStart,
                textStart < lineText.Length ? lineText[textStart..] : string.Empty);
            return true;
        }

        return false;
    }

    private static bool TryParseOrderedMarker(string markerText, out int number, out char delimiter)
    {
        number = 0;
        delimiter = '\0';

        if (string.IsNullOrEmpty(markerText))
        {
            return false;
        }

        var delimiterIndex = markerText.Length - 1;
        if (delimiterIndex <= 0 || markerText[delimiterIndex] is not ('.' or ')'))
        {
            return false;
        }

        if (!int.TryParse(markerText[..delimiterIndex], out number))
        {
            return false;
        }

        delimiter = markerText[delimiterIndex];
        return true;
    }

    private static string RebuildListLine(ListLineInfo listLine, int indentLength, string textContent)
    {
        var indent = new string(' ', Math.Max(0, indentLength));
        var prefix = listLine.Kind switch
        {
            ListKind.Bullet => $"{indent}{listLine.MarkerText} ",
            ListKind.Task => $"{indent}{listLine.MarkerText} [{(listLine.IsChecked ? 'x' : ' ')}] ",
            ListKind.Ordered => $"{indent}{listLine.OrderedNumber.GetValueOrDefault()}{listLine.OrderedDelimiter} ",
            _ => indent
        };

        return prefix + textContent;
    }

    private static string RebuildNextListLine(ListLineInfo listLine, string textContent)
    {
        return listLine.Kind switch
        {
            ListKind.Bullet => RebuildListLine(listLine, listLine.IndentLength, textContent),
            ListKind.Task => RebuildListLine(listLine with { IsChecked = false }, listLine.IndentLength, textContent),
            ListKind.Ordered => RebuildListLine(listLine with { OrderedNumber = listLine.OrderedNumber.GetValueOrDefault() + 1 }, listLine.IndentLength, textContent),
            _ => RebuildListLine(listLine, listLine.IndentLength, textContent)
        };
    }

    private static int GetPrefixLength(string lineText)
    {
        var analysis = MarkdownLineParser.Analyze(lineText, MarkdownFenceState.None);
        return (analysis.TaskList?.Text ?? analysis.ListMarker?.Text)?.Start ?? lineText.Length;
    }

    private static int GetLeadingSpacesLength(string lineText)
    {
        var length = 0;
        while (length < lineText.Length && lineText[length] == ' ')
        {
            length++;
        }

        return length;
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private enum ListKind
    {
        Bullet,
        Ordered,
        Task
    }

    private readonly record struct ListLineInfo(
        ListKind Kind,
        int IndentLength,
        string MarkerText,
        bool IsChecked,
        int? OrderedNumber,
        char OrderedDelimiter,
        int ContentStartColumn,
        string TextContent);
}
