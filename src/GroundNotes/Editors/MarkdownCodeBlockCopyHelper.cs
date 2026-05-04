using AvaloniaEdit.Document;

namespace GroundNotes.Editors;

internal static class MarkdownCodeBlockCopyHelper
{
    public static bool TryResolve(TextDocument? document, int lineNumber, out MarkdownCodeBlockCopyInfo info)
    {
        info = default;
        if (document is null || lineNumber < 1 || lineNumber > document.LineCount)
        {
            return false;
        }

        MarkdownFenceState state = MarkdownFenceState.None;
        int? blockStartLine = null;

        for (var currentLineNumber = 1; currentLineNumber <= document.LineCount; currentLineNumber++)
        {
            var line = document.GetLineByNumber(currentLineNumber);
            var lineText = document.GetText(line.Offset, line.Length);
            var stateBeforeLine = state;
            var fence = MarkdownLineParser.TryMatchFence(lineText);

            if (!stateBeforeLine.IsInsideFence && fence is not null)
            {
                blockStartLine = currentLineNumber;
            }

            var isFencedLine = stateBeforeLine.IsInsideFence || blockStartLine == currentLineNumber;
            state = MarkdownLineParser.AdvanceFenceState(state, lineText);

            if (isFencedLine && !state.IsInsideFence && stateBeforeLine.IsInsideFence && blockStartLine is not null)
            {
                if (lineNumber >= blockStartLine.Value && lineNumber <= currentLineNumber)
                {
                    return TryBuildInfo(document, blockStartLine.Value, currentLineNumber, isClosed: true, out info);
                }

                blockStartLine = null;
                continue;
            }

            if (currentLineNumber == document.LineCount && blockStartLine is not null && state.IsInsideFence)
            {
                if (lineNumber >= blockStartLine.Value)
                {
                    return TryBuildInfo(document, blockStartLine.Value, document.LineCount, isClosed: false, out info);
                }
            }
        }

        return false;
    }

    private static bool TryBuildInfo(TextDocument document, int startLineNumber, int endLineNumber, bool isClosed, out MarkdownCodeBlockCopyInfo info)
    {
        info = default;

        var contentStartLineNumber = startLineNumber + 1;
        var contentEndLineNumber = isClosed ? endLineNumber - 1 : endLineNumber;
        if (contentStartLineNumber > contentEndLineNumber)
        {
            return false;
        }

        var contentStartLine = document.GetLineByNumber(contentStartLineNumber);
        var contentEndLine = document.GetLineByNumber(contentEndLineNumber);
        var contentStartOffset = contentStartLine.Offset;
        var contentEndOffset = contentEndLine.Offset + contentEndLine.Length;
        if (contentEndOffset < contentStartOffset)
        {
            return false;
        }

        var text = document.GetText(contentStartOffset, contentEndOffset - contentStartOffset);
        if (text.Length == 0)
        {
            return false;
        }

        info = new MarkdownCodeBlockCopyInfo(
            startLineNumber,
            endLineNumber,
            contentStartOffset,
            contentEndOffset - contentStartOffset,
            text);
        return true;
    }
}

internal readonly record struct MarkdownCodeBlockCopyInfo(
    int StartLineNumber,
    int EndLineNumber,
    int ContentStartOffset,
    int ContentLength,
    string Text);
