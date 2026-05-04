# Fix Code Block Selection Bounds

## Context

Markdown fenced code blocks in GroundNotes are rendered with a small visual inset so code text starts a few columns inside the block. When selecting text inside a fenced code block, the selection highlight can extend left into that visual-only inset instead of starting at the rendered code text. The result looks like the selection escapes the code content area even though the selected text itself is inside the block.

Relevant current behavior:

- `src/GroundNotes/Editors/MarkdownVisualLineIndentationProvider.cs` returns a **2-column** fenced-code leading indentation via `FencedCodeIndentationRule`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs` applies that indentation in `ApplyVisualLineIndentation(...)` by inserting a `VisualIndentationElement` at visual column `0`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualIndentationElement.cs` renders spaces with `VisualLength > 0` and `DocumentLength == 0`; it is visual-only and not part of the document.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/SelectionLayer.cs` renders selected ranges through `BackgroundGeometryBuilder`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs` computes selection rectangles from `SelectionSegment` offsets/visual columns, then calls `VisualLine.ValidateVisualColumn(...)` and `ProcessTextLines(...)`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` maps document offsets to visual columns in `GetVisualColumn(int relativeTextOffset)`.

The likely root cause is that `VisualLine.GetVisualColumn(0)` can return the visual column of the inserted zero-document-length indentation element (`0`) when a selection starts at the first real character of an indented code line. The real text starts after the `VisualIndentationElement`, but the selection geometry is reconstructed from the line-start document offset and begins at the fake indentation column.

## Root Cause

`VisualIndentationElement` is intentionally zero-length in document space, so it does not affect copied text, caret offsets, or persisted content. However, the existing visual-column lookup in `VisualLine.GetVisualColumn(int relativeTextOffset)` treats an element as matching when:

```csharp
element.RelativeTextOffset <= relativeTextOffset
&& element.RelativeTextOffset + element.DocumentLength >= relativeTextOffset
```

For a zero-document-length element at the start of a line, this condition matches offset `0`. If the selection segment has only offsets, or if `ValidateVisualColumn(...)` needs to reconstruct the visual column for the line-start offset, it can resolve to the indentation element's column instead of the first real text element's column.

This should be fixed in the AvaloniaEdit fork's visual-column mapping/selection geometry path rather than by app-side workarounds, because selection rendering is owned by AvaloniaEdit and the bug is caused by interaction between zero-length visual elements and selection rectangles.

## Fix

Adjust selection/visual-column mapping so zero-document-length leading visual indentation is not included when a document selection starts at the first real character on that line.

The preferred fix is to make `VisualLine.GetVisualColumn(int relativeTextOffset)` choose the visual column of the first non-zero-document-length element at a boundary when a zero-length element and real text share the same `RelativeTextOffset`.

Concretely:

- At offset boundaries, especially `relativeTextOffset == 0`, do not return a `VisualLineElement` whose `DocumentLength == 0` when a following element also starts at the same document offset and has document content.
- Preserve the current behavior for non-boundary offsets and for actual visual-only elements that do not precede selectable text.
- Keep `GetRelativeOffset(int visualColumn)` unchanged unless tests prove it is also part of the selection geometry bug; changing both directions increases hit-testing/caret risk.

If changing `GetVisualColumn(...)` is too broad, use a narrower helper for selection rectangles in `BackgroundGeometryBuilder`, for example:

- Resolve the candidate visual column normally.
- Clamp the start column to the first selectable/content visual column of the `TextLine` when the segment starts at the document line start and the preceding columns are only zero-document-length indentation.
- Keep this clamp local to selection geometry so caret hit-testing remains untouched.

Prefer the `VisualLine.GetVisualColumn(...)` fix only if existing caret/hit-testing tests pass, because it fixes the source of the mismatch and avoids selection-only special casing.

## Implementation Order

### Step 1 — Reproduce and Lock Down Geometry

1. Add a focused regression test in `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` using a fenced code block whose content line starts at column 0.
2. Create a `TextEditor` with markdown formatting enabled, `WordWrap = true`, and enough width that the selected line does not wrap for the first assertion.
3. Select from the first content character to a later character on that same fenced line.
4. Call `textView.EnsureVisualLines()` and use `BackgroundGeometryBuilder.GetRectsForSegment(...)` with the editor selection segment to inspect the first selection rectangle.
5. Assert that the selection rectangle `Left` is at or to the right of the code text's rendered x-position:
   - Get the fenced line's `VisualLine`.
   - Use `visualLine.GetVisualColumn(lineStartRelativeOffset)` after the fix, or explicitly compute the first real text visual column if the test needs to demonstrate the old mismatch.
   - Convert it with `visualLine.GetTextLineVisualXPosition(textLine, visualColumn) - textView.ScrollOffset.X`.

This test should fail before the fix by showing `rect.Left` starting at the fake indentation column.

### Step 2 — Fix Boundary Visual-Column Resolution

1. In `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs`, refactor `GetVisualColumn(int relativeTextOffset)` so it handles zero-document-length elements at boundaries deliberately.
2. Suggested logic:
   - Validate `relativeTextOffset` as today.
   - Iterate `_elements` by index instead of returning the first match immediately.
   - When an element has `DocumentLength == 0` and `element.RelativeTextOffset == relativeTextOffset`, look ahead for the next element with the same `RelativeTextOffset` and `DocumentLength > 0`.
   - Return that following element's `GetVisualColumn(relativeTextOffset)` instead of the zero-length element's visual column.
   - If no following content element exists, fall back to the existing behavior.
3. Keep this behavior targeted to zero-length boundary elements so normal inline elements, folding markers, and virtual-space behavior are not broadly changed.

### Step 3 — Verify Wrapped Code Blocks

1. Extend the regression test or add a second assertion for a wrapped fenced-code line.
2. Select text on a continuation segment and verify the left edge still respects the wrapped continuation indent that already works through `WrappedLineContinuationIndent`.
3. Preserve these existing tests:
   - `WrappedCodeBlock_PreservesHookIndentAcrossSegments`
   - `WrappedCodeBlock_PreservesHookIndentAfterResize`
   - `WrappedCodeBlock_ReflowsWithHookOnResizeWithoutManualTextViewRedraw`
   - `WrappedCodeBlock_ContinuationCaretRoundTripsThroughVisualPosition`
   - `WrappedCodeBlock_ContinuationHitTestingProducesCopyableSelection`
   - `WrappedCodeBlock_PreservesHookIndentAtNarrowWidth`

### Step 4 — Check Other Selection Modes

1. Manually verify drag selection, shift-click/keyboard selection, double-click word selection, and full-line selection inside fenced code blocks.
2. If full-line selection intentionally highlights the entire text area in AvaloniaEdit, decide whether the product expectation also applies there. For this bug, the must-fix path is normal text-range selection inside the code block.
3. If whole-line selection still extends left after Step 2, inspect `SelectionMouseHandler.GetLineAtMousePosition(...)` and offset-only `Selection.Create(...)` call sites; they may need the same visual-column boundary treatment through `BackgroundGeometryBuilder`.

## Files to Modify

- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` — primary fix for zero-document-length visual indentation boundary mapping.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/BackgroundGeometryBuilder.cs` — fallback/narrower fix point if changing `VisualLine.GetVisualColumn(...)` is too broad.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` — add regression coverage for selection rectangle left bounds inside fenced code blocks.
- Potentially `tests/GroundNotes.Tests/MarkdownVisualLineIndentationProviderTests.cs` only if the provider contract changes; it should not need changes for the preferred fix.

## Edge Cases and Constraints

- Do not remove or reduce the 2-column fenced-code indentation unless the visual design changes; the issue is selection geometry, not code-block layout.
- Do not change persisted markdown text or insert real leading spaces into the document.
- Preserve caret hit-testing and copyable selection behavior for wrapped code blocks.
- Preserve list-continuation indentation behavior; `MarkdownVisualLineIndentationProvider` handles both fenced code and inherited list continuation indentation.
- Be careful with zero-document-length elements in AvaloniaEdit generally. Visual indentation is one such element, but folding/inline UI features may also rely on boundary mapping.
- Treat `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` as high risk; keep the patch minimal and heavily tested.

## Verification

Run the AvaloniaEdit fork build first because the preferred fix changes fork internals:

```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet build src/GroundNotes/GroundNotes.csproj
```

Run targeted tests:

```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownVisualLineIndentationProviderTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownColorizingTransformerTests"
```

Before final handoff, run:

```bash
dotnet build GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```

Manual regression checks:

1. Open a note with a fenced code block whose content starts at the first code column.
2. Drag-select text starting at the beginning of a code line; the highlight should start at the rendered code text, not in the left fake indentation area.
3. Repeat selection from the middle of a code line; the highlight should still start exactly at the selected character.
4. Test wrapped fenced-code lines at a narrow editor width; continuation selection should keep the existing wrapped indent behavior.
5. Test keyboard selection and double-click word selection inside the block.
6. Test regular non-code paragraphs and list continuations to confirm their selection geometry did not change unexpectedly.
