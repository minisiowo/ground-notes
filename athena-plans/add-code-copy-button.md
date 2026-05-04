# Add Code Copy Button

## Context

GroundNotes already recognizes fenced markdown code blocks and renders a themed background for them, but code blocks do not expose a direct copy affordance. The requested behavior is a small, minimalist, theme-aware button in the top-right corner of a rendered fenced code block. The button should appear only while the pointer is over that block and should copy the block contents to the system clipboard.

The existing editor stack has useful building blocks:

- `MarkdownColorizingTransformer.QueryIsFencedCodeLine(...)` is the source of truth for fenced-code state.
- `CodeBlockBackgroundRenderer` groups visible fenced lines and paints the block background on `KnownLayer.Background`.
- `MarkdownImagePreviewLayer` is the closest existing render-only overlay pattern: it is inserted into `TextView.Layers`, refreshes on visual-line and scroll changes, and computes bounds from `VisualLine` geometry without changing document text.
- `EditorThemeController` owns markdown presentation setup, detach, resource refresh, and resize refresh for the editor.
- `ClipboardTextService.SetTextAsync(...)` centralizes clipboard writes and preserves the Linux/Wayland fallback.
- `AppStyles.axaml` already has small theme-aware button patterns such as `compactIconButton` and `contextChipRemoveButton`.

Interpretation: copy **only the content between opening and closing fences**, not the fence marker lines themselves. Preserve original line breaks inside the block. If a block has no content, keep the button hidden or disabled rather than copying fence text.

## Design

Add a new editor overlay layer for fenced-code copy controls rather than modifying document content, inserting fake lines, or changing AvaloniaEdit layout. The layer should mirror the image preview layer's lifecycle, but it must be hit-testable because it hosts an interactive button.

Recommended new app-side types:

- `src/GroundNotes/Editors/MarkdownCodeBlockCopyLayer.cs`
  - A `Control` inserted above `KnownLayer.Text`.
  - Tracks currently hovered fenced block and positions one reusable `Button` at that block's visible top-right edge.
  - Subscribes to `TextView.VisualLinesChanged`, `TextView.ScrollOffsetChanged`, pointer movement/exit events, and document changes as needed.
  - Does not alter visual-line height, indentation, or document text.
- `src/GroundNotes/Editors/MarkdownCodeBlockInfo.cs` or private records inside the layer
  - Stores block start/end line numbers, content start/end offsets, visible bounds, and copied text.
  - Keep this private unless tests benefit from a small internal helper.

The layer should reuse existing code-block detection instead of creating a parallel markdown parser. It should scan from the visual line under the pointer outward through `TextDocument` lines using:

- `MarkdownColorizingTransformer.QueryIsFencedCodeLine(document, lineNumber)` to confirm membership.
- `MarkdownLineParser.TryMatchFence(lineText)` to find opening/closing fence lines and exclude them from copied content.
- `MarkdownLineParser.AdvanceFenceState(...)` only if a helper needs deterministic full-document state reconstruction; prefer the colorizer's query for visible-line membership.

The UI should be a compact glyph/text button, for example `⧉`, `Copy`, or `⎘`, styled through dynamic resources. Start by reusing/adding an app style in `AppStyles.axaml` based on `compactIconButton` or `contextChipRemoveButton`: transparent idle background, `SurfaceHoverBrush` on hover, `SurfacePressedBrush` on press, `BorderBrushBase`/`FocusBorderBrush`, and `PlaceholderTextBrush`/`AppTextBrush`. Avoid new theme tokens unless existing brushes cannot produce readable contrast.

## Implementation Order

### Step 1 — Extract code-block geometry and text safely

1. Add a small helper inside the new layer, or a focused internal helper in `Editors`, that can resolve a document line number to a fenced code block:
   - Return `null` when the line is not fenced according to `QueryIsFencedCodeLine(...)`.
   - Walk upward to the first fenced line in the contiguous block and downward to the last fenced line.
   - Treat the first and last fence marker lines as structural lines when `MarkdownLineParser.TryMatchFence(...)` succeeds.
   - Copy text from the first content line after the opening fence through the line before the closing fence.
2. Preserve exact block content between fences:
   - Include internal blank lines.
   - Preserve indentation.
   - Preserve line endings as returned by the document APIs, or normalize only if surrounding editor copy helpers already do so.
3. Handle incomplete blocks conservatively:
   - If there is an opening fence but no closing fence, copy lines after the opening fence to the end of the fenced region/document.
   - If the resolved content range is empty, hide or disable the copy button.

### Step 2 — Add `MarkdownCodeBlockCopyLayer`

1. Model the layer after `MarkdownImagePreviewLayer`:
   - Constructor accepts `TextView`, `MarkdownColorizingTransformer`, and an async callback such as `Func<string, Task>` for copying.
   - Subscribe to `VisualLinesChanged` and `ScrollOffsetChanged` to reposition or hide the button.
   - Provide `RequestRefresh()`, `Refresh()`, `ClearState()`, and `Dispose()` methods so `EditorThemeController` can manage it consistently.
2. Unlike `MarkdownImagePreviewLayer`, set `IsHitTestVisible = true` and host a single child `Button`.
   - Keep the layer itself transparent and avoid intercepting pointer events except over the button.
   - If direct child hosting in a `Control` becomes awkward, use `Canvas`/`Panel` semantics or a minimal custom control pattern that can arrange the button at explicit coordinates.
3. Track hover state with immediate pointer movement rather than document changes:
   - On pointer moved over `TextView`, map the pointer to a document position with `TextView.GetPosition(...)` or match against visible-line bounds.
   - If the pointer is inside a fenced block's painted area, set it as the active block and show the button.
   - On pointer leave, scroll, document replace, or markdown formatting disable, hide the button.
   - Keep the button visible while the pointer is over the button itself so it can be clicked.
4. Position the button at the visible block's top-right corner:
   - Compute block top/bottom similarly to `CodeBlockBackgroundRenderer.Draw(...)`: use visible lines with `visualLine.VisualTop - textView.VerticalOffset` and `visualLine.Height`.
   - Use `textView.Bounds.Width` for the right edge, minus a small margin such as `6` or `8` px.
   - Place the button near `blockTop + 4`, clamp within `Bounds`, and keep it stable when the top of a block is scrolled out by anchoring to the first visible line in the hovered block.
5. On click:
   - Stop propagation enough that the editor does not move the caret or start a selection.
   - Call the supplied copy callback with resolved block content.
   - Optionally set a short visual state (`Copied` text for ~1s) if this can be done without extra complexity; otherwise rely on the status bar.

### Step 3 — Integrate through `EditorThemeController`

1. Add a private field for `MarkdownCodeBlockCopyLayer` in `EditorThemeController`.
2. Extend the constructor to accept or create a copy callback. Prefer passing the callback from `MainWindow` so clipboard and status messages remain at the view boundary; avoid making `Editors` depend on `TopLevel` or `MainViewModel`.
3. Insert the copy layer above `KnownLayer.Text`, near the image preview layer:
   - The copy button should visually sit above text and above the code background.
   - Do not disrupt `_imagePreviewLayer` hit testing or rendering.
4. Update lifecycle methods:
   - `AttachMarkdownPresentation()` inserts the layer if missing and requests refresh.
   - `DetachMarkdownPresentation()` clears/removes the layer.
   - `Dispose()` removes and disposes it.
   - `RefreshVisualResources()` and `RefreshPresentation()` invalidate/reapply button style resources as needed.
   - `RefreshAfterDocumentReplace()` clears stale hover/block state.

### Step 4 — Wire clipboard behavior in `MainWindow`

1. Add a small method near `CopyEditorSelectionAsync`:
   - `private async Task CopyCodeBlockAsync(string code)`.
   - Resolve `TopLevel.GetTopLevel(this)?.Clipboard`.
   - If unavailable, set `MainViewModel.StatusMessage = "System clipboard is not available."` as image-copy methods already do.
   - Use `ClipboardTextService.SetTextAsync(topLevel.Clipboard, code)`.
   - On success set a concise status such as `Copied code block`.
   - Catch expected clipboard exceptions and report `Could not copy code block: ...`.
2. Update wherever `EditorThemeController` instances are constructed so the callback is supplied for primary and secondary pane editors.
3. Keep selection copy/cut behavior unchanged.

### Step 5 — Style the button

1. Prefer adding a specific selector such as `Button.codeBlockCopyButton` in `src/GroundNotes/Styles/AppStyles.axaml`.
2. Base it on existing compact patterns:
   - `Width`/`Height` around `24` or `26`.
   - `Padding="0"`, `MinWidth`, `MinHeight`, `FontSize` using `AppFontSizeSmall`.
   - Transparent idle background or a subtle `PaneBackgroundBrush` with low visual weight.
   - Hover/pressed via `SurfaceHoverBrush` and `SurfacePressedBrush`.
   - Border via `BorderBrushBase` and `FocusBorderBrush`.
3. Use a `ToolTip` like `Copy code` so the minimalist glyph remains understandable.
4. Verify both light/dark/custom themes through dynamic resources; do not hard-code colors unless following an existing style already does so.

### Step 6 — Tests

Add focused tests around logic that can run headlessly:

1. New tests for the code-block resolver/copy text helper:
   - Copies only content between fences.
   - Preserves blank lines and indentation.
   - Supports language info on opening fence.
   - Supports tilde fences as well as backtick fences.
   - Handles an incomplete final fence without throwing.
   - Returns no copyable content for an empty fenced block.
2. Add or extend `EditorThemeControllerTests` to verify the copy layer is attached, detached, and disposed along with markdown presentation.
3. If geometry helpers are internal and deterministic, add tests for button placement with visible top line, scrolled block, and block leaving the viewport. Use `MarkdownImagePreviewLayerTests` as the closest pattern for overlay state and hit-test style tests.
4. Add `ThemeBuilderTests` only if new theme keys are introduced. If existing dynamic resources are reused, no theme-token tests are needed.

## Files to Modify

- `src/GroundNotes/Editors/MarkdownCodeBlockCopyLayer.cs` — new overlay layer and block-hover/button behavior.
- `src/GroundNotes/Editors/MarkdownLineParser.cs` — only if a small public/internal helper is needed; avoid changing existing parser semantics.
- `src/GroundNotes/Views/EditorThemeController.cs` — construct, insert, refresh, detach, and dispose the new layer.
- `src/GroundNotes/Views/MainWindow.axaml.cs` — provide clipboard callback and status handling.
- `src/GroundNotes/Styles/AppStyles.axaml` — add the minimalist theme-aware button style.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` — lifecycle/integration coverage.
- `tests/GroundNotes.Tests/MarkdownColorizingTransformerTests.cs` or a new focused test file — fenced-block content extraction coverage.
- `tests/GroundNotes.Tests/MarkdownImagePreviewLayerTests.cs` or a new `MarkdownCodeBlockCopyLayerTests.cs` — overlay geometry/hover coverage if practical.

## Edge Cases and Constraints

- Do not modify persisted markdown text or insert placeholder lines for the button.
- Do not implement this in the AvaloniaEdit fork unless app-side hit testing proves impossible; the current requirement fits app-side overlay infrastructure.
- Preserve existing code-block wrap/indent behavior from `MarkdownVisualLineIndentationProvider`.
- Keep the copy overlay independent from image preview rendering and image hit-testing.
- Avoid unbounded caches. The layer should keep only the currently hovered block and the current button state.
- Ensure clicking the button does not change active selection, caret position, or pane focus unexpectedly.
- For multi-pane editors, each pane needs its own layer and callback; copying should target the block from the pane under the pointer.

## Verification

Run targeted checks first:

```bash
dotnet build src/GroundNotes/GroundNotes.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests|FullyQualifiedName~MarkdownColorizingTransformerTests|FullyQualifiedName~MarkdownImagePreviewLayerTests|FullyQualifiedName~MarkdownVisualLineIndentationProviderTests|FullyQualifiedName~MarkdownLineParserTests"
```

Before final handoff, run the broader baseline:

```bash
dotnet build GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```

Manual regression checks:

1. Open a note with multiple fenced code blocks, including long/wrapped lines and blank lines.
2. Hover over a code block and verify the button appears only for that block, at the top-right, without shifting text.
3. Move the pointer away and verify the button hides.
4. Click the button and paste elsewhere to confirm only the block content was copied, not opening/closing fences.
5. Verify the button remains usable after scrolling, resizing the editor, switching notes, and toggling markdown formatting if exposed.
6. Validate light/dark/custom themes for readable idle, hover, and pressed states.
7. Validate multi-pane mode: hover/copy in primary and secondary panes without causing horizontal workspace scroll or focus surprises.
