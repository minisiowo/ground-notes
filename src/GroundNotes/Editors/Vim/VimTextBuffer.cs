using System;
using System.Collections.Generic;

namespace GroundNotes.Editors.Vim;

internal sealed class VimTextBuffer
{
    private readonly List<Line> _lines = [];

    public VimTextBuffer(string text)
    {
        Text = text;
        ParseLines();
    }

    public string Text { get; }

    public int LineCount => _lines.Count;

    public Line this[int index] => _lines[index];

    public string PreferredNewLine
    {
        get
        {
            foreach (var line in _lines)
            {
                if (line.NewLine.Length > 0)
                {
                    return line.NewLine;
                }
            }

            return "\n";
        }
    }

    public int GetLineIndex(int offset)
    {
        offset = Math.Clamp(offset, 0, Text.Length);

        for (var index = 0; index < _lines.Count - 1; index++)
        {
            if (offset < _lines[index].EndIncludingBreak)
            {
                return index;
            }
        }

        return _lines.Count - 1;
    }

    public int NormalizeNormalOffset(int offset)
    {
        var line = _lines[GetLineIndex(offset)];
        return line.Length == 0
            ? line.Start
            : Math.Clamp(offset, line.Start, line.ContentEnd - 1);
    }

    public int GetNormalOffset(int lineIndex, int column)
    {
        var line = _lines[Math.Clamp(lineIndex, 0, _lines.Count - 1)];
        return line.Length == 0
            ? line.Start
            : line.Start + Math.Clamp(column, 0, line.Length - 1);
    }

    public int GetColumn(int offset)
    {
        var normalized = NormalizeNormalOffset(offset);
        var line = _lines[GetLineIndex(normalized)];
        return normalized - line.Start;
    }

    public int GetFirstNonBlankOffset(int lineIndex)
    {
        var line = _lines[Math.Clamp(lineIndex, 0, _lines.Count - 1)];
        for (var offset = line.Start; offset < line.ContentEnd; offset++)
        {
            if (Text[offset] is not (' ' or '\t'))
            {
                return offset;
            }
        }

        return line.Start;
    }

    private void ParseLines()
    {
        if (Text.Length == 0)
        {
            _lines.Add(new Line(0, 0, 0, string.Empty));
            return;
        }

        var start = 0;
        while (start < Text.Length)
        {
            var contentEnd = start;
            while (contentEnd < Text.Length && Text[contentEnd] is not ('\r' or '\n'))
            {
                contentEnd++;
            }

            var end = contentEnd;
            if (end < Text.Length)
            {
                end += Text[end] == '\r' && end + 1 < Text.Length && Text[end + 1] == '\n'
                    ? 2
                    : 1;
            }

            _lines.Add(new Line(start, contentEnd, end, Text[contentEnd..end]));
            start = end;
        }

        if (_lines[^1].EndIncludingBreak == Text.Length && _lines[^1].NewLine.Length > 0)
        {
            _lines.Add(new Line(Text.Length, Text.Length, Text.Length, string.Empty));
        }
    }

    internal readonly record struct Line(
        int Start,
        int ContentEnd,
        int EndIncludingBreak,
        string NewLine)
    {
        public int Length => ContentEnd - Start;
    }
}
