# Fix Caret vs Selection 2-Char Offset in Code Blocks

## Context

When selecting text inside a fenced markdown code block by dragging from the left side (or using Shift+End / Shift+Right from the start of a line), the caret is visibly offset by **exactly 2 characters** from the selection highlight. The previous fix (`fix-code-block-selection-bounds.md`) added clamping in `BackgroundGeometryBuilder` and `Caret`, but the offset persists in practice.

The visual indentation in code blocks comes from `MarkdownVisualLineIndentationProvider` returning **2 columns** for fenced code lines. `TextView.ApplyVisualLineIndentation` inserts a `VisualIndentationElement` (VisualLength=2, DocumentLength=0) at the start of the visual line. Both caret rendering (`Caret.CalcCaretRectangle`) and selection geometry (`BackgroundGeometryBuilder.ProcessTextLines`) were supposed to clamp to the first selectable position after this element, yet the user still sees a mismatch.

## Root Cause Hypothesis

Static analysis shows both code paths *should* converge to visual column 2, but they do not in the running app. The most probable causes are:

1. **Unvalidated visual column leaking into `SelectionSegment`**. Although `Caret.Position` getter validates lazily, there may be a path where `SimpleSelection` stores a raw `TextViewPosition` whose `VisualColumn` was never validated (e.g. created directly from mouse hit-testing without going through the caret getter, or preserved across a visual-line rebuild).

2. **Mismatch between `TextLine.GetTextBounds` and `GetTextLineVisualXPosition` at the boundary** of a zero-document-length element. `TextLine.GetTextBounds(2, length)` might return a rectangle whose `Left` does not exactly align with `GetTextLineVisualXPosition(line, 2)` due to how Avalonia's text formatter handles the transition from the indentation spaces to real text, especially when the indentation element has `DocumentLength==0`.

3. **`VisualLine.GetVisualColumn(int)` boundary behaviour is correct for offset→column, but `GetRelativeOffset(int)` allows columns 0 and 2 to both map to document offset 0**. This dual mapping means a raw visual column of 0 passes `ValidateVisualColumn`’s offset-consistency check (`offsetFromVisualColumn == offset`) and gets normalized to 2, BUT the raw value 0 might still be used somewhere before normalization (e.g. in a direct `GetTextViewPosition(0)` call or in `SelectionMouseHandler` creating the anchor).

Because the previous fix only added downstream clamping (in geometry builder and caret renderer) rather than fixing the source mapping, the two paths can still diverge if one uses the clamped value and the other uses a differently-clamped or unclamped intermediate.

## Fix Strategy

**Primary fix:** Change `VisualLine.GetVisualColumn(int)` so that when multiple elements claim the same document offset at a boundary, it **always returns the visual column of the first element that has real document content**, never the interior of a zero-document-length indentation element. This makes visual column 0 an invalid representative for document offset 0 inside an indented code block; the canonical mapping becomes unambiguous.

**Secondary fix:** Harden `BackgroundGeometryBuilder` so that even if a raw visual column 0 leaks into a `SelectionSegment`, `ProcessTextLines` forces the start of the selection rectangle to exactly match `GetTextLineVisualXPosition(line, selectableStartVcInLine)`, not merely clamp the `Rect.Left`. Eliminate any chance that `line.GetTextBounds` returns a slightly different x-coordinate at the boundary.

**Tertiary fix:** Harden `Caret.CalcCaretRectangle` to compute `xPos` directly from `GetTextLineVisualXPosition(textLine, validatedVisualColumn)` after explicit validation, instead of relying on `minXPos` clamping as a safety net.

## Implementation Order

### Step 1 — Reproduction Test

Add a focused test in `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` (or a new `CodeBlockSelectionCaretOffsetTests.cs` if the file grows too large) that:

1. Creates a `TextEditor` with markdown formatting and a fenced code block whose first content line begins immediately after the fence (no leading spaces in the document).
2. Sets editor width wide enough that the line does not wrap.
3. Simulates a mouse-down at the very left edge of the first code content line (inside the 2-column visual indentation).
4. Simulates a mouse-move / mouse-up that extends the selection to the 10th character.
5. Calls `textView.EnsureVisualLines()`.
6. Captures:
   - `Caret.CalculateCaretRectangle().X`
   - First rectangle from `BackgroundGeometryBuilder.GetRectsForSegment(textView, selectionSegment)`
7. **Asserts** that the caret x-position equals the selection rectangle’s left edge, within a sub-pixel tolerance (e.g. `0.5`).
8. Also asserts that both are **strictly greater than** `GetTextLineVisualXPosition(firstTextLine, 0)` (i.e. they are not at the raw column-0 position).

This test must fail before the fix, demonstrating the 2-character offset.

### Step 2 — Source Mapping Fix in `VisualLine`

Edit `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs`:

Refactor `GetVisualColumn(int relativeTextOffset)`:

```csharp
public int GetVisualColumn(int relativeTextOffset)
{
    ThrowUtil.CheckNotNegative(relativeTextOffset, "relativeTextOffset");
    VisualLineElement? candidate = null;
    foreach (var element in _elements)
    {
        if (element.RelativeTextOffset > relativeTextOffset)
            break;

        if (element.RelativeTextOffset + element.DocumentLength < relativeTextOffset)
            continue;

        // At a boundary where a zero-document element and a real element share the same offset,
        // prefer the real element so the returned visual column is inside selectable text.
        if (element.DocumentLength == 0 && element.RelativeTextOffset == relativeTextOffset)
        {
            candidate = element;
            continue;
        }

        return element.GetVisualColumn(relativeTextOffset);
    }

    if (candidate != null)
        return candidate.GetVisualColumn(relativeTextOffset);

    return VisualLength;
}
```

Rationale: When `relativeTextOffset == 0` and the first element is `VisualIndentationElement` (DocumentLength=0), the old code immediately returned `candidate.GetVisualColumn(0)` which gives `VisualColumn + VisualLength = 2`. The new code preserves this outcome but makes the intent explicit: **skip zero-document boundary elements when a real content element starts at the same offset**. This eliminates ambiguity for callers that do their own offset-to-column lookups.

### Step 3 — Geometry Builder Hardening

Edit `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs`:

In `ProcessTextLines`, after computing `selectableLeft`:

```csharp
double selectableLeft = visualLine.GetTextLineVisualXPosition(line, selectableStartVcInLine) - scrollOffset.X;
```

When building the selection rectangle for a non-empty segment, instead of:

```csharp
foreach (var b in line.GetTextBounds(segmentStartVcInLine, segmentEndVcInLine - segmentStartVcInLine)) {
    double left = b.Rectangle.Left - scrollOffset.X;
    double right = b.Rectangle.Right - scrollOffset.X;
    // ...
    lastRect = ClampLeft(new Rect(Math.Min(left, right), y, Math.Abs(right - left), lineHeight), selectableLeft);
}
```

Force the first rectangle’s left edge to exactly `selectableLeft` when `segmentStartVcInLine == selectableStartVcInLine`:

```csharp
bool isFirstBound = true;
foreach (var b in line.GetTextBounds(segmentStartVcInLine, segmentEndVcInLine - segmentStartVcInLine)) {
    double left = b.Rectangle.Left - scrollOffset.X;
    double right = b.Rectangle.Right - scrollOffset.X;
    if (lastRect != default)
        yield return lastRect;
    var rect = new Rect(Math.Min(left, right), y, Math.Abs(right - left), lineHeight);
    if (isFirstBound && segmentStartVcInLine == selectableStartVcInLine)
    {
        rect = rect.WithX(selectableLeft);
        isFirstBound = false;
    }
    lastRect = ClampLeft(rect, selectableLeft);
}
```

This guarantees that the selection highlight starts at exactly the same x-coordinate that `GetTextLineVisualXPosition` reports for the first selectable column, removing any discrepancy introduced by `TextLine.GetTextBounds` internal rounding or subpixel positioning at the element boundary.

### Step 4 — Caret Renderer Hardening

Edit `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/Caret.cs`:

In `CalcCaretRectangle`, explicitly validate before computing xPos:

```csharp
private Rect CalcCaretRectangle(VisualLine visualLine)
{
    if (!_visualColumnValid)
    {
        RevalidateVisualColumn(visualLine);
    }

    var textLine = visualLine.GetTextLine(_position.VisualColumn, _position.IsAtEndOfLine);
    var validatedVisualColumn = _position.VisualColumn;
    var xPos = visualLine.GetTextLineVisualXPosition(textLine, validatedVisualColumn);
    var minXPos = GetSelectableStartXPosition(visualLine, textLine);
    if (xPos < minXPos)
    {
        xPos = minXPos;
    }

    var textTop = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop);
    var textBottom = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom);

    return new Rect(xPos,
                    textTop,
                    CaretWidth,
                    Math.Max(CaretWidth, textBottom - textTop));
}
```

This is mostly documentation-of-intent; the current code already validates. However, adding an explicit `validatedVisualColumn` local makes it obvious that the x position is based on the validated column, not a raw one.

### Step 5 — Mouse Handler Hardening (Prevent Raw Leak)

Edit `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/SelectionMouseHandler.cs`:

In `SetCaretOffsetToMousePosition`, after setting the caret position, force an immediate validation so that any subsequent read of `TextArea.Caret.Position` cannot return a raw value:

```csharp
if (offset >= 0)
{
    TextArea.Caret.Position = new TextViewPosition(TextArea.Document.GetLocation(offset), visualColumn) { IsAtEndOfLine = isAtEndOfLine };
    TextArea.Caret.DesiredXPos = double.NaN;
    // Force validation now so SimpleSelection stores the canonical visual column,
    // preventing a raw 0 from leaking into SelectionSegment.
    TextArea.Caret.VisualColumn.ToString(); // triggers ValidateVisualColumn via getter side-effect
}
```

**Alternative / cleaner approach:** Change `Caret.Position` setter to call `ValidateVisualColumn()` immediately before raising `PositionChanged`. This ensures any listener that reads `Caret.Position` gets a validated value. Evaluate whether this broader change is safe (it should be, because validation is idempotent).

### Step 6 — Run Tests and Validate

Build:
```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet build src/GroundNotes/GroundNotes.csproj
```

Run targeted tests:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownVisualLineIndentationProviderTests"
```

If the new reproduction test exists and fails before the fix, run it after the fix to confirm it passes.

## Files to Modify

- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` — boundary mapping fix in `GetVisualColumn(int)`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs` — force first selection rectangle to exact `selectableLeft`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/Caret.cs` — explicit validated column usage (documentation/robustness).
- `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/SelectionMouseHandler.cs` — ensure caret validation before selection creation (or broader setter change in `Caret.cs`).
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` — add reproduction test (or new file).

## Edge Cases and Constraints

- **Wrapped lines:** The fix in `VisualLine.GetVisualColumn` only affects boundary resolution at `RelativeTextOffset == 0`. It does not change behaviour for offsets inside real text or on continuation lines.
- **Other zero-document elements:** Folding sections, inline UI elements, or other `DocumentLength==0` elements that appear at a boundary will also be skipped in favour of the next real element. This is the desired behaviour for caret/selection consistency.
- **Keyboard selection:** `CaretNavigationCommandHandler` uses `GetNextCaretPosition`, which already skips `VisualIndentationElement` interior. The fix aligns offset→column mapping with that existing caret-navigation behaviour.
- **Copy command:** The copy layer (`MarkdownCodeBlockCopyLayer`) and copy helper operate on document offsets, not visual columns, so they are unaffected.
- **Performance:** The `GetVisualColumn` change adds a single pass over elements with early break; the element count per line is tiny, so this is negligible.

## Verification

1. The new reproduction test must pass after the fix.
2. Existing tests for wrapped code blocks, indentation, caret height, and image previews must still pass.
3. Manual check: open a note with a fenced code block, click inside the left indentation area, drag to select text. The caret and selection highlight must start at exactly the same x-position (the first real text character), with no visible gap.
4. Repeat with Shift+End and Shift+Right from the start of a code line.
5. Verify non-code paragraphs and list items show no regression in selection behaviour.
