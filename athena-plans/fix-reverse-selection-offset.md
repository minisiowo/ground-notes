# Fix Reverse Selection Offset in Visually Indented Code Blocks

## Context

The visible bug is no longer just “selection starts in the code-block gutter.” The current screenshot shows a more specific failure:

- The user starts selecting from the end of the word `obiekt` and drags/extends left.
- The first two intended characters (`ob`) are not highlighted.
- Two extra characters after the caret/word end are highlighted instead.
- The drift is exactly **2 characters**, matching the synthetic fenced-code visual indentation.

This means the whole selection rectangle is shifted by the visual indentation width. The previous fixes focused on clamping selection/caret at the line start. They do not prove that mid-line selection ranges are mapped correctly when visual columns include a zero-document-length leading element.

Relevant current implementation:

- `src/GroundNotes/Editors/MarkdownVisualLineIndentationProvider.cs` returns 2 visual indentation columns for fenced code lines.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs` inserts `VisualIndentationElement(2)` in `ApplyVisualLineIndentation(...)`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualIndentationElement.cs` has `VisualLength == 2` and `DocumentLength == 0`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` maps document offsets to visual columns, so a document column inside code text becomes `documentColumn + 2`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs` renders selection rectangles with `TextLine.GetTextBounds(segmentStartVcInLine, segmentEndVcInLine - segmentStartVcInLine)`.

## Root Cause

The likely root cause is that `BackgroundGeometryBuilder.ProcessTextLines(...)` passes **visual columns that include the synthetic indentation** directly into `TextLine.GetTextBounds(...)`. For a code-block word in the middle of the line:

- Document range for `obiekt`: `[wordStart, wordEnd)`.
- Visual range after `VisualIndentationElement(2)`: `[wordStart + 2, wordEnd + 2)`.
- If `TextLine.GetTextBounds(...)` interprets the start index as the rendered/document text index for bounds, passing `wordStart + 2` paints `[wordStart + 2, wordEnd + 2)` — exactly: missing the first 2 characters and adding 2 after the caret.

The old tests did not catch this because they mostly asserted `Left >= firstCodeTextX` or compared line-start caret/selection positions. They did not assert exact mid-line word bounds for a backward selection.

The previous hardening that only forced the first rectangle to `selectableLeft` when the selection starts at the first selectable column cannot fix mid-line ranges like `obiekt`, because `segmentStartVcInLine != selectableStartVcInLine` for those ranges.

## Approach

Fix the selection geometry at the point where selected ranges are converted to painted rectangles.

For lines containing leading zero-document visual elements, `BackgroundGeometryBuilder` should not trust `TextLine.GetTextBounds(...)` with raw visual columns for mid-line ranges. Instead, compute selection rectangle edges from the same visual-position API used by caret rendering:

```csharp
var left = visualLine.GetTextLineVisualXPosition(line, segmentStartVcInLine) - scrollOffset.X;
var right = visualLine.GetTextLineVisualXPosition(line, segmentEndVcInLine) - scrollOffset.X;
```

Then build the rectangle from those visual x-positions. This keeps caret and selection geometry in the same coordinate system and removes the two-column drift.

Preserve the existing `TextLine.GetTextBounds(...)` path for normal lines without synthetic zero-document leading elements, because it is useful for bidi/RTL selections. Use the visual-position path only when the current visual line/text line has zero-document visual content before real document text.

## Implementation Steps

### Step 1 — Add an exact mid-line reverse-selection regression test

Add a focused test in `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` near the existing code-block selection tests.

Suggested test name:

```csharp
CodeBlockSelection_BackwardWordSelectionDoesNotShiftByVisualIndentation
```

Use a sample with a word long enough to expose a 2-character drift and with trailing punctuation/text so over-selection is visible:

```text
before
```
```text
tonalne na obiekt, twarde przejscia
```
```text
after
```

Test construction:

1. Create the editor with markdown formatting enabled via existing `CreateEditor(...)` helper.
2. Locate the fenced content line and the word `obiekt`.
3. Compute:
   - `wordStartOffset`
   - `wordEndOffset`
   - `wordStartVisualColumn = visualLine.GetVisualColumn(wordStartOffset - line.Offset)`
   - `wordEndVisualColumn = visualLine.GetVisualColumn(wordEndOffset - line.Offset)`
4. Create a reverse-order segment to mimic drag from word end back to word start:

```csharp
var segment = new SelectionSegment(
    wordEndOffset,
    wordEndVisualColumn,
    wordStartOffset,
    wordStartVisualColumn);
```

5. Call `BackgroundGeometryBuilder.GetRectsForSegment(textView, segment)`.
6. Assert one rectangle on the content line.
7. Expected edges must be exact visual positions:

```csharp
var expectedLeft = visualLine.GetTextLineVisualXPosition(textLine, wordStartVisualColumn) - textView.ScrollOffset.X;
var expectedRight = visualLine.GetTextLineVisualXPosition(textLine, wordEndVisualColumn) - textView.ScrollOffset.X;
```

8. Assert:

```csharp
Assert.True(Math.Abs(rect.Left - expectedLeft) < 0.5, ...);
Assert.True(Math.Abs(rect.Right - expectedRight) < 0.5, ...);
```

The failure mode should show `rect.Left` and `rect.Right` roughly two character widths to the right.

### Step 2 — Add a diagnostic/helper assertion for `TextLine.GetTextBounds`

In the same test or a sibling test, compare the current `TextLine.GetTextBounds(...)` result against `GetTextLineVisualXPosition(...)` for the same word range.

This is diagnostic, not necessarily permanent:

```csharp
var bounds = Assert.Single(textLine.GetTextBounds(wordStartVisualColumn, wordEndVisualColumn - wordStartVisualColumn));
var boundsLeft = bounds.Rectangle.Left - textView.ScrollOffset.X;
```

If `boundsLeft` differs from `expectedLeft` by approximately two character widths, it confirms that `TextLine.GetTextBounds(...)` is the drifting layer.

Do not keep brittle diagnostic assertions if they depend on Avalonia internals; the permanent regression should assert final selection geometry.

### Step 3 — Add a `BackgroundGeometryBuilder` helper for visually indented lines

Edit `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs`.

Add a private helper that detects whether a text line/range is affected by leading zero-document visual content:

```csharp
private static bool HasLeadingZeroDocumentVisualContent(VisualLine visualLine, int visualStartCol, int visualEndCol)
{
    foreach (var element in visualLine.Elements) {
        int elementStart = element.VisualColumn;
        int elementEnd = elementStart + element.VisualLength;

        if (elementEnd <= visualStartCol)
            continue;

        if (elementStart > visualEndCol)
            break;

        if (element.DocumentLength <= 0 && elementStart <= visualStartCol)
            return true;

        if (element.DocumentLength > 0)
            return false;
    }

    return false;
}
```

Adjust the condition if the test proves the zero-document element is before, but not overlapping, the current wrapped `TextLine`. The important condition is: the visual line contains synthetic columns before real document text, and selected visual columns are offset from document columns by those synthetic columns.

### Step 4 — Render affected selection ranges from visual x-positions

In `ProcessTextLines(...)`, inside the non-empty selection branch:

Current risky path:

```csharp
foreach (var b in line.GetTextBounds(segmentStartVcInLine, segmentEndVcInLine - segmentStartVcInLine)) {
    ...
}
```

For visually indented lines, replace this with a visual-position rectangle:

```csharp
if (HasLeadingZeroDocumentVisualContent(visualLine, visualStartCol, visualEndCol)) {
    double left = visualLine.GetTextLineVisualXPosition(line, segmentStartVcInLine) - scrollOffset.X;
    double right = visualLine.GetTextLineVisualXPosition(line, segmentEndVcInLine) - scrollOffset.X;
    lastRect = ClampLeft(new Rect(Math.Min(left, right), y, Math.Abs(right - left), lineHeight), selectableLeft);
} else {
    // existing GetTextBounds path for normal lines / bidi-aware rendering
}
```

This directly aligns selection edges with caret/hit-test visual positions and should eliminate the exact 2-character drift.

Keep the existing `segmentEndVc == visualEndCol` and virtual-space extension logic after this block, but verify it does not re-extend the rectangle past the caret in this regression.

### Step 5 — Remove or simplify the previous first-bound-only workaround if redundant

The previous patch added:

```csharp
if (isFirstBound && segmentStartVcInLine == selectableStartVcInLine) {
    rect = rect.WithX(selectableLeft);
}
```

This only affects selections that start at the first selectable column. It does not fix mid-line ranges like `obiekt`. After Step 4, keep it only in the non-indented fallback path if still useful. For the visually indented path, it should be unnecessary because both edges come from `GetTextLineVisualXPosition(...)`.

### Step 6 — Re-evaluate recent `VisualLine.GetVisualColumn(...)` and `SelectionMouseHandler` changes

The recent changes in:

- `VisualLine.GetVisualColumn(int relativeTextOffset)`
- `SelectionMouseHandler.SetCaretOffsetToMousePosition(...)`

may not be harmful, but they did not address this mid-line rectangle drift. After the regression passes, review whether they are still justified:

- Keep `VisualLine.GetVisualColumn(...)` if it makes boundary behavior explicit and does not regress tests.
- Keep immediate caret validation only if no tests show side effects; otherwise remove it because selection rendering should be correct even when segments carry raw visual columns.

Do not add more normalization layers until the geometry bug is fixed; the symptom is a rendered range shift, not necessarily a stored selection offset bug.

## Files to Modify

- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs` — primary fix: compute affected selection rectangles from visual x-positions instead of `TextLine.GetTextBounds(...)` with synthetic-indent visual columns.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` — add exact reverse-selection word-bound regression.
- Possibly `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` — only if the diagnostic proves endpoint visual columns themselves are wrong.
- Possibly `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/SelectionMouseHandler.cs` — only if actual mouse simulation shows raw/validated endpoint mismatch after geometry is fixed.

## Verification

Build the fork and app:

```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet build src/GroundNotes/GroundNotes.csproj
```

Run targeted tests:

```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~CodeBlockSelection_BackwardWordSelectionDoesNotShiftByVisualIndentation"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownVisualLineIndentationProviderTests"
```

Manual regression:

1. Open a fenced code block containing a line like `tonalne na obiekt, twarde przejscia`.
2. Start selection from the end of `obiekt` and drag left to the beginning of `obiekt`.
3. Confirm the highlight covers exactly `obiekt` — not `iekt, ` and not any two-character-shifted range.
4. Repeat forward selection from the beginning of `obiekt` to the end.
5. Repeat on a wrapped code-block continuation row and on a normal paragraph to confirm no regression.

## Non-goals

- Do not change persisted markdown text.
- Do not remove the 2-column visual indentation design.
- Do not keep stacking caret/selection normalization changes unless tests prove endpoint storage is wrong. The currently observed bug is a rendered rectangle shift.
