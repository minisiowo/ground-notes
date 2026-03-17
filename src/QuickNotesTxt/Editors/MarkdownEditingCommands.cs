namespace QuickNotesTxt.Editors;

internal static class MarkdownEditingCommands
{
    public static MarkdownEditResult ToggleWrap(string text, int selectionStart, int selectionLength, string marker)
    {
        var start = Clamp(selectionStart, 0, text.Length);
        var length = Clamp(selectionLength, 0, text.Length - start);
        var end = start + length;

        if (length == 0)
        {
            return new MarkdownEditResult(start, 0, marker + marker, start + marker.Length, 0);
        }

        if (start >= marker.Length
            && end + marker.Length <= text.Length
            && string.Equals(text[(start - marker.Length)..start], marker, StringComparison.Ordinal)
            && string.Equals(text[end..(end + marker.Length)], marker, StringComparison.Ordinal))
        {
            return new MarkdownEditResult(start - marker.Length, length + (marker.Length * 2), text[start..end], start - marker.Length, length);
        }

        return new MarkdownEditResult(start, length, marker + text[start..end] + marker, start + marker.Length, length);
    }

    public static MarkdownEditResult ToggleHeading(string text, int selectionStart, int selectionLength, int level)
    {
        var marker = new string('#', Math.Clamp(level, 1, 6)) + " ";
        return TransformSelectedLines(text, selectionStart, selectionLength, lines =>
        {
            var meaningful = lines.Where(static line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
            if (meaningful.Count == 0)
            {
                lines[0].Content = marker;
                return;
            }

            var allAtLevel = meaningful.All(line => line.HeadingLevel == level);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                var content = RemoveHeading(line.Content);
                line.Content = allAtLevel ? content : marker + content;
            }
        });
    }

    public static MarkdownEditResult ToggleTaskList(string text, int selectionStart, int selectionLength)
    {
        return TransformSelectedLines(text, selectionStart, selectionLength, lines =>
        {
            var meaningful = lines.Where(static line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
            if (meaningful.Count == 0)
            {
                lines[0].Content = "- [ ] ";
                return;
            }

            var allTasks = meaningful.Count > 0 && meaningful.All(static line => line.IsTaskList);

            foreach (var line in meaningful)
            {
                var content = StripListPrefix(line.Content);
                line.Content = allTasks ? content : "- [ ] " + content;
            }
        });
    }

    public static MarkdownEditResult ToggleBulletList(string text, int selectionStart, int selectionLength)
    {
        return TransformSelectedLines(text, selectionStart, selectionLength, lines =>
        {
            var meaningful = lines.Where(static line => !string.IsNullOrWhiteSpace(line.Text)).ToList();
            if (meaningful.Count == 0)
            {
                lines[0].Content = "- ";
                return;
            }

            var allBullets = meaningful.Count > 0 && meaningful.All(static line => line.IsBulletList && !line.IsTaskList);

            foreach (var line in meaningful)
            {
                var content = StripListPrefix(line.Content);
                line.Content = allBullets ? content : "- " + content;
            }
        });
    }

    public static MarkdownEditResult ToggleCodeBlock(string text, int selectionStart, int selectionLength)
    {
        var start = Clamp(selectionStart, 0, text.Length);
        var length = Clamp(selectionLength, 0, text.Length - start);
        var end = start + length;

        if (length == 0)
        {
            return new MarkdownEditResult(start, 0, "```\n\n```", start + 4, 0);
        }

        var selectedText = text[start..end];
        if (selectedText.StartsWith("```\n", StringComparison.Ordinal) && selectedText.EndsWith("\n```", StringComparison.Ordinal))
        {
            var unwrapped = selectedText[4..^4];
            return new MarkdownEditResult(start, length, unwrapped, start, unwrapped.Length);
        }

        var wrapped = $"```\n{selectedText}\n```";
        return new MarkdownEditResult(start, length, wrapped, start + 4, selectedText.Length);
    }

    public static MarkdownEditResult InsertLineBelow(string text, int caretOffset)
    {
        var caret = Clamp(caretOffset, 0, text.Length);
        var lineEnd = caret < text.Length ? text.IndexOf('\n', caret) : -1;
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return new MarkdownEditResult(lineEnd, 0, "\n", lineEnd + 1, 0);
    }

    public static MarkdownEditResult DeleteCurrentLine(string text, int selectionStart, int selectionLength)
    {
        var start = Clamp(selectionStart, 0, text.Length);
        var length = Clamp(selectionLength, 0, text.Length - start);
        var end = start + length;

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(start - 1, 0));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var lineEnd = end < text.Length ? text.IndexOf('\n', end) : -1;
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }
        else if (lineStart > 0)
        {
            lineStart--;
        }
        else
        {
            lineEnd++;
        }

        if (lineStart == 0 && lineEnd == text.Length)
        {
            return new MarkdownEditResult(0, text.Length, string.Empty, 0, 0);
        }

        return new MarkdownEditResult(lineStart, lineEnd - lineStart, string.Empty, lineStart, 0);
    }

    private static MarkdownEditResult TransformSelectedLines(string text, int selectionStart, int selectionLength, Action<List<EditableLine>> transform)
    {
        if (string.IsNullOrEmpty(text))
        {
            var emptyLines = new List<EditableLine> { EditableLine.Parse(string.Empty) };
            transform(emptyLines);

            var emptyReplacement = string.Join('\n', emptyLines.Select(static line => line.ToText()));
            var emptyCaretOffset = emptyLines[0].Text.Length;
            return new MarkdownEditResult(0, 0, emptyReplacement, emptyCaretOffset, 0);
        }

        var start = Clamp(selectionStart, 0, text.Length);
        var length = Clamp(selectionLength, 0, text.Length - start);
        var end = start + length;

        if (start > end)
        {
            (start, end) = (end, start);
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(start - 1, 0));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        var lineEnd = end < text.Length ? text.IndexOf('\n', end) : -1;
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        if (lineEnd < lineStart)
        {
            lineEnd = lineStart;
        }

        var block = text[lineStart..lineEnd];
        var rawLines = block.Split('\n');
        var lines = rawLines.Select(static line => EditableLine.Parse(line)).ToList();
        transform(lines);

        var replacement = string.Join('\n', lines.Select(static line => line.ToText()));
        if (length == 0 && lines.Count == 1)
        {
            var caretOffset = lineStart + lines[0].Text.Length;
            return new MarkdownEditResult(lineStart, lineEnd - lineStart, replacement, caretOffset, 0);
        }

        return new MarkdownEditResult(lineStart, lineEnd - lineStart, replacement, lineStart, replacement.Length);
    }

    private static string RemoveHeading(string content)
    {
        var index = 0;
        while (index < content.Length && content[index] == '#')
        {
            index++;
        }

        if (index == 0 || index >= content.Length || content[index] != ' ')
        {
            return content;
        }

        return content[(index + 1)..];
    }

    private static string StripListPrefix(string content)
    {
        if (content.StartsWith("- [ ] ", StringComparison.Ordinal) || content.StartsWith("- [x] ", StringComparison.Ordinal) || content.StartsWith("- [X] ", StringComparison.Ordinal))
        {
            return content[6..];
        }

        if (content.StartsWith("- ", StringComparison.Ordinal) || content.StartsWith("* ", StringComparison.Ordinal) || content.StartsWith("+ ", StringComparison.Ordinal))
        {
            return content[2..];
        }

        return content;
    }

    private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

    private sealed class EditableLine
    {
        public required string Indent { get; init; }

        public required string Content { get; set; }

        public string Text => Indent + Content;

        public bool IsTaskList => Content.StartsWith("- [ ] ", StringComparison.Ordinal) || Content.StartsWith("- [x] ", StringComparison.Ordinal) || Content.StartsWith("- [X] ", StringComparison.Ordinal);

        public bool IsBulletList => Content.StartsWith("- ", StringComparison.Ordinal) || Content.StartsWith("* ", StringComparison.Ordinal) || Content.StartsWith("+ ", StringComparison.Ordinal);

        public int HeadingLevel
        {
            get
            {
                var count = 0;
                while (count < Content.Length && Content[count] == '#')
                {
                    count++;
                }

                return count > 0 && count < Content.Length && Content[count] == ' ' ? count : 0;
            }
        }

        public string ToText() => Text;

        public static EditableLine Parse(string text)
        {
            var index = 0;
            while (index < text.Length && text[index] == ' ')
            {
                index++;
            }

            return new EditableLine
            {
                Indent = text[..index],
                Content = text[index..]
            };
        }
    }
}

internal readonly record struct MarkdownEditResult(int Start, int Length, string Replacement, int SelectionStart, int SelectionLength);
