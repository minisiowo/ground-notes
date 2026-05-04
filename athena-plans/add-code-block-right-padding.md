# Add Code Block Right Padding

## Context

Fenced code blocks currently have a visual left gutter but no matching right gutter. The left side comes from a render-only indentation hook: `MarkdownVisualLineIndentationProvider` returns 2 columns for fenced code lines, and the AvaloniaEdit fork inserts a zero-document `VisualIndentationElement` before the real line text in `TextView.ApplyVisualLineIndentation(...)`. The code block background itself is painted by `CodeBlockBackgroundRenderer` across the full `TextView.Bounds.Width`.

The requested behavior is to make the block feel symmetric: code text, selection, wrapping, caret movement, and hit-testing should respect a right-side inset inside the same dark code-block background. This should not modify the markdown document text.

## Design

Implement the right inset as a **line-specific formatting width reduction** for fenced code lines, not as a painted-only overlay or a trailing zero-document visual element.

Rationale:

- Reducing the formatted width makes wrapping happen before the right edge, producing a real empty gutter inside the existing full-width code block background.
- Caret, selection, and hit-testing then continue to operate against real `TextLine` layout instead of needing separate right-edge clipping rules.
- A trailing zero-document element would introduce new document/visual-column mismatch cases in `VisualLine`, `BackgroundGeometryBuilder`, and mouse selection. The current special handling only covers leading zero-document indentation.
- A background-only change would hide the symptom visually but would not prevent selected text/caret from reaching the right edge.

Keep `CodeBlockBackgroundRenderer` filling the full text-view width initially. The visual goal from the screenshot is an empty right gutter within the code-block rectangle, not a narrower code-block rectangle.

## API

Extend the existing AvaloniaEdit indentation hook instead of adding a parallel app-side renderer path.

Update `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/IVisualLineIndentationProvider.cs` with a trailing-inset member, for example:

```csharp
/// <summary>
/// Returns the number of trailing visual columns to reserve from the formatted width for the specified line.
/// Return 0 to apply no extra trailing inset.
/// </summary>
int GetTrailingVisualInsetColumns(TextView textView, DocumentLine documentLine);
```

Use columns rather than pixels so the right padding matches the existing left code-block indentation and tracks editor font size. The app implementation should return the same `2` columns for fenced code lines as the left inset.

## Implementation Steps

### Step 1 — Add provider support for trailing columns

Modify `IVisualLineIndentationProvider`:

- Keep `GetVisualIndentationColumns(...)` unchanged for left padding.
- Add `GetTrailingVisualInsetColumns(...)` with XML documentation that makes clear it is render/layout-only and should not change document text.
- Because this is a fork-local interface and the app has one implementation, update all implementers directly.

Modify `src/GroundNotes/Editors/MarkdownVisualLineIndentationProvider.cs`:

- Add a private `const int FencedCodeInsetColumns = 2` or equivalent to avoid duplicating the number.
- Construct `FencedCodeIndentationRule` with this constant.
- Implement `GetTrailingVisualInsetColumns(TextView textView, DocumentLine documentLine)`.
- Return `FencedCodeInsetColumns` when `_colorizer.QueryIsFencedCodeLine(textView.Document, documentLine.LineNumber)` is true; otherwise return `0`.
- Preserve current list-continuation behavior; lists should not receive trailing padding.

### Step 2 — Apply the right inset during line formatting

Modify `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs` in `BuildVisualLine(...)`.

Before the `while (textOffset <= visualLine.VisualLengthWithEndOfLineMarker)` formatting loop, compute the line-specific formatted width:

```csharp
var formattedWidth = availableSize.Width;
var trailingInsetColumns = VisualLineIndentationProvider?.GetTrailingVisualInsetColumns(this, documentLine) ?? 0;
if (trailingInsetColumns > 0)
{
    var trailingInset = trailingInsetColumns * WideSpaceWidth;
    formattedWidth = Math.Max(WideSpaceWidth, availableSize.Width - trailingInset);
}
```

Then pass `formattedWidth` to:

- `_formatter.FormatLine(...)` instead of `availableSize.Width`
- `ClampContinuationIndentation(...)` instead of `availableSize.Width`

Keep `availableSize` itself unchanged for measuring the editor and arranging layers. This makes only the fenced line wrap earlier while preserving the full editor/background width.

Important details:

- Clamp to at least one `WideSpaceWidth` to avoid zero/negative format widths in very narrow panes.
- The existing leading `VisualIndentationElement` is already part of the visual line before formatting, so the formatted width includes the left inset and real text. Subtracting the right inset should make the remaining space symmetric.
- Do not subtract from non-code lines.

### Step 3 — Keep the background renderer unchanged unless the product asks for a narrower block

Leave `src/GroundNotes/Editors/CodeBlockBackgroundRenderer.cs` unchanged for the first implementation pass:

- It fills from `x = 0` to `TextView.Bounds.Width`.
- With Step 2, text and selection should stop before the right edge, naturally showing the right gutter inside that background.

Only revisit this file if manual review shows the desired visual is a smaller rounded/filled block rather than internal padding. If it is revisited, avoid using it to clip selection/caret; it should remain purely decorative.

### Step 4 — Add focused provider tests

Update `tests/GroundNotes.Tests/MarkdownVisualLineIndentationProviderTests.cs`:

- Add `GetTrailingVisualInsetColumns_ReturnsInsetForFencedLinesOnly`.
- Reuse `CreateSample()`.
- Assert line 1 and line 5 return `0`, while the opening fence, content line, and closing fence return `2`.
- Assert list samples still return `0` for trailing inset.

### Step 5 — Add layout/wrap regression tests

Update `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` near existing wrapped code-block tests.

Add a test such as `WrappedCodeBlock_ReservesRightInsetWhenWrapping`:

1. Create a word-wrapped editor with a narrow fixed width using existing `CreateEditor(...)` or `CreateWrappedEditor(...)` helpers.
2. Use a fenced content line long enough to wrap.
3. Get the content `VisualLine` and assert it has multiple `TextLines`.
4. Calculate the right edge of each `TextLine` with `visualLine.GetTextLineVisualXPosition(textLine, visualStart + line.Length)` or `textLine.WidthIncludingTrailingWhitespace` as appropriate.
5. Assert each rendered row's right edge is less than or equal to `textView.Bounds.Width - (2 * textView.WideSpaceWidth)` within a small tolerance.
6. Add a neighboring plain paragraph with the same long text and assert it can use more width than the fenced line, so the change is line-specific.

If direct right-edge assertions are brittle in Avalonia text formatting, assert the fenced line wraps into more rows than the neighboring plain line at the same width.

### Step 6 — Add selection/caret right-gutter regression tests

Add one or two tests in `EditorThemeControllerTests.cs` near the existing selection tests:

- `CodeBlockSelection_DoesNotExtendIntoTrailingInsetForLineEndSelection`
  - Select from the beginning of a fenced content line through its document line end.
  - Get selection rectangles through `BackgroundGeometryBuilder.GetRectsForSegment(...)`.
  - Assert the last rectangle right edge does not exceed `textView.Bounds.Width - trailingInset + tolerance` for the code line.

- `CodeBlockHitTesting_RightInsetMapsToLineEndWithoutExtendingSelection`
  - Click/hit-test a point inside the right gutter on a fenced line: `x = textView.Bounds.Width - trailingInset / 2`.
  - Assert it maps to the end of the line (or the closest valid wrapped-row endpoint) and does not create a selection/caret position beyond the formatted text edge.

These tests should also guard the recent `BackgroundGeometryBuilder` and caret/hit-testing fixes for zero-document left indentation.

### Step 7 — Manual polish pass

After tests pass, manually check the screenshot scenario:

- A fenced code block with long selected lines should show the same apparent padding on the right as on the left.
- Opening fence, closing fence, blank fenced lines, and wrapped continuation rows should all respect the right gutter.
- The hover `copy` button from `MarkdownCodeBlockCopyLayer` should still appear in the top-right of the code block and remain usable.

## Files to Modify

- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/IVisualLineIndentationProvider.cs` — add trailing visual inset API.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs` — subtract the provider's trailing inset from the line-specific format width in `BuildVisualLine(...)` and continuation indentation clamping.
- `src/GroundNotes/Editors/MarkdownVisualLineIndentationProvider.cs` — return `2` trailing inset columns for fenced code lines only.
- `tests/GroundNotes.Tests/MarkdownVisualLineIndentationProviderTests.cs` — provider-level trailing inset coverage.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` — wrapped layout, right-gutter selection, and hit-testing coverage.

## Risks and Edge Cases

- Very narrow editors: clamp formatted width so code lines still produce valid `TextLine` instances.
- Wrapped continuation rows: ensure the right inset affects every `TextLine` produced from a fenced document line, not just the first row.
- Horizontal scrolling/no-wrap mode: if `_canHorizontallyScroll` makes `TextWrapping.NoWrap`, the formatted width may not visibly constrain long lines. Preserve existing no-wrap behavior unless the app uses no-wrap for markdown notes and the user expects right padding there too; if needed, follow up with a separate no-wrap selection/background policy.
- Bidi/RTL selection: avoid changing `BackgroundGeometryBuilder` unless tests prove it is necessary, because the current fallback keeps `TextLine.GetTextBounds(...)` for normal lines.
- Image previews and code-copy overlay are width-sensitive UI layers but should not depend on fenced line format width. Spot-check them manually.
- `TextView.CreateAndMeasureVisualLines(...)` computes horizontal extent from `TextLine.WidthIncludingTrailingWhitespace`; subtracting a code-line format width may reduce horizontal extent for code lines. That is desirable in wrapped mode, but should be checked in no-wrap scenarios.

## Verification

Run targeted validation first:

```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet build tests/GroundNotes.Tests/GroundNotes.Tests.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownVisualLineIndentationProviderTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
```

Then run the broader build:

```bash
dotnet build GroundNotes.sln
```

Manual checks:

- Open a note with a fenced code block similar to the screenshot and select long lines left-to-right and right-to-left.
- Confirm the right gutter remains visible under selection and on wrapped continuation rows.
- Confirm caret placement at line end stays before the right gutter.
- Confirm clicking in the left visual indent still maps to the first document-backed code column.
- Confirm clicking in the right gutter maps to the nearest valid line end and does not create visual drift.
- Confirm normal paragraphs and list wrapping remain unchanged.
