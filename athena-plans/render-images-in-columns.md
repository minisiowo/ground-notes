# Render Images in Columns

## Context

GroundNotes renders markdown image previews as bitmap overlays beneath the document line that contains them. The supported syntax is:

```markdown
![](assets/photo.png)|50
```

where `|50` is an optional scale percent. Today the pipeline is built around **one preview per document line**:

- `MarkdownLineParser` finds all image matches on a line and marks each `IsStandalone`.
- `MarkdownImagePreviewProvider` returns the *first* standalone image per line.
- `MarkdownImagePreviewLayer` stores one `RenderedPreview` per line number and draws it.
- `MarkdownImageVisualLineTransformer` adds `preview.Height + 12` extra visual height for that line.

The user wants a **column layout** so that multiple images on the same line render side-by-side with a small gap. The proposed syntax is:

```markdown
![](assets/baletnica-brunetka.png)|40||![](assets/image-20260501-173632515.png)|40
```

Generalized: a line composed exclusively of two or more `![](path)|scale` expressions separated by `||` (with optional surrounding whitespace) should render as a horizontal row of image previews.

## Approach

The change touches the parser, the preview provider, the overlay layer, and the visual-line height transformer. The guiding principle is to **generalize the one-preview-per-line model to a list-of-previews-per-line model** while preserving all existing caching, coalescing, reuse, and invalidation behavior.

### Parser: Recognize `||` Column Separators

After `FindImages` populates `analysis.Images`, run a new helper `TryBuildColumnLayout(lineText, analysis.Images, out var columnImages)`. A line qualifies as a column layout when:

1. It contains at least two images.
2. The text before the first image is whitespace only.
3. The text after the last image is whitespace only.
4. Between each consecutive pair of images, the non-image text trims to exactly `||`.

If true, set `analysis.IsColumnLayout = true` and populate `analysis.ColumnImages` with the sorted matches. `MarkdownImageMatch.IsStandalone` is left unchanged (it will be `false` for most column images because `||` sits adjacent to their `FullSpan`), so existing colorizing logic is unaffected.

### Provider: Return a List of Previews Per Line

Change `MarkdownImagePreviewProvider.GetPreview` to return `IReadOnlyList<MarkdownImagePreview>` instead of `MarkdownImagePreview?`. An empty list means "no preview".

Internal changes:
- Introduce `ColumnGap = 12` constant (keep in sync with the layer).
- Change `_previewLineCache` to store `IReadOnlyList<PreviewLine>? PreviewLines`.
- Change `_previewRenderCache` to store `IReadOnlyList<MarkdownImagePreview>? Previews` and `IReadOnlyList<FileStamp>? FileStamps`.
- Refactor `GetPreviewLine` → `GetPreviewLines`:
  - If `analysis.IsColumnLayout`, resolve **all** `ColumnImages`.
  - If not, fall back to the first `IsStandalone` image (existing behavior).
  - When resolving a column image, if the path does not resolve or the bitmap fails to load, **skip that image and continue** (do not abort the whole line). Return an empty list only if no images survive.
- Update `ComputeScaledSize` to accept a `maxWidth` parameter.
  - Single image: `maxWidth = Math.Max(1, Math.Min(MaxRenderWidth, availableWidth - HorizontalPadding))`.
  - Column of N images: `maxWidthPerImage = Math.Max(1, Math.Min(MaxRenderWidth, (availableWidth - HorizontalPadding - (N - 1) * ColumnGap) / N))`.
- Update `InvalidateImage` to scan the new list-based render cache.
- Update `OnDocumentChanged` line cache invalidation — keys remain line numbers, no logic change needed.

### Layer: Render and Hit-Test Multiple Previews Per Line

Change `_renderedPreviews` from `Dictionary<int, RenderedPreview>` to `Dictionary<int, IReadOnlyList<RenderedPreview>>`.

`Refresh()`:
- Call `GetPreview` (now returns a list).
- If the list is empty, remove the line entry and record `HasPreview = false`.
- Otherwise, compute X offsets left-to-right: each preview’s `Bounds` is `GetPreviewRect(..., xOffset)`, and `xOffset` increments by `preview.Width + ColumnGap`.
- Store the list. Record `HasPreview = true`.
- In the **line-state reuse** fast path, rebuild the bounds list with updated scroll offsets the same way.

`Render(DrawingContext)`:
- Iterate `_renderedPreviews.Values` (lists), then each `RenderedPreview` inside.
- Call `RecordPreviewLayerDrawnPreview()` once per bitmap drawn.

`TryHitTestPreview(Point)`:
- Iterate lists ordered by descending line number, then each preview in the list.
- Return the hit result for the exact preview whose bounds contain the point.

`GetPreviewRect` signature expands to accept a `double xOffset`.

### Height Transformer: Reserve Space for the Tallest Column Image

`MarkdownImageVisualLineTransformer.Transform`:
- Call `GetPreview`.
- If empty, return.
- `maxHeight = previews.Max(p => p.Height)`.
- `SetAdditionalVisualHeight(maxHeight + VerticalSpacing * 2)`.

This keeps all column images on the same visual row without clipping.

## Implementation Order

### Step 1 — Parser

1. In `MarkdownLineParser.cs`, add `TryBuildColumnLayout` (private static).
2. Add `IsColumnLayout` and `ColumnImages` to `MarkdownLineAnalysis`.
3. Invoke `TryBuildColumnLayout` inside `Analyze` after `analysis.Images` is populated.
4. Add parser unit tests for:
   - `![](a)|40||![](b)|40` → `IsColumnLayout == true`, two `ColumnImages`.
   - `![](a)|40 || ![](b)|40` with spaces → true.
   - `![](a)|40||![](b)|40||![](c)|40` → true, three images.
   - Mixed text `![](a)|40||![](b)|40 text` → `IsColumnLayout == false`.
   - Single image `![](a)|40` → `IsColumnLayout == false`.

### Step 2 — Provider Model

1. In `MarkdownImagePreviewProvider.cs`:
   - Change `GetPreview` return type to `IReadOnlyList<MarkdownImagePreview>`.
   - Introduce `ColumnGap = 12`.
   - Update `PreviewLineCacheEntry`, `PreviewRenderCacheEntry`, `GetPreviewLines`, `ComputeScaledSize`, cache validation, and `InvalidateImage`.
2. Add provider unit tests:
   - Column layout returns two previews, each scaled to half the available width (minus gap).
   - Column layout with one broken image link returns preview for the valid image only.
   - Cache hit/miss semantics still work for columns.
   - Width change recomputes all column preview sizes.

### Step 3 — Visual-Line Transformer

1. In `MarkdownImageVisualLineTransformer.cs`, update `Transform` to consume the list and use `Max` height.

### Step 4 — Preview Layer

1. In `MarkdownImagePreviewLayer.cs`:
   - Change `_renderedPreviews` dictionary value type.
   - Update `Refresh` to build lists of `RenderedPreview` with cumulative `xOffset`.
   - Update line-state reuse path.
   - Update `Render` to iterate nested lists and count drawn previews correctly.
   - Update `TryHitTestPreview` to flatten-hit-test.
   - Add `xOffset` parameter to `GetPreviewRect`.
2. Add layer unit tests:
   - Column layout produces two `RenderedPreview` entries for the same line number.
   - Hit test returns the correct preview when two are side-by-side.
   - Horizontal scroll repositions both previews without new preview requests.

### Step 5 — Integration & Compilation

1. Verify `EditorThemeController.cs`, `EditorHostController.cs`, and `MainWindow.axaml.cs` compile with the new list-based API. No logic changes expected in these files.
2. Update any existing test helpers that reflect into `_renderedPreviews` (e.g., `GetRenderedPreviewBounds` in `MarkdownImagePreviewLayerTests`) to account for the new dictionary value type.

### Step 6 — Validation

Run the full build and targeted tests, then manual validation.

## Files to Modify

| File | Nature of Change |
|------|------------------|
| `src/GroundNotes/Editors/MarkdownLineParser.cs` | Add `TryBuildColumnLayout`, `IsColumnLayout`, `ColumnImages`. |
| `src/GroundNotes/Editors/MarkdownImagePreviewProvider.cs` | List-based API, column sizing, multi-image cache entries. |
| `src/GroundNotes/Editors/MarkdownImagePreviewLayer.cs` | List-based `_renderedPreviews`, multi-image layout, hit-test, draw. |
| `src/GroundNotes/Editors/MarkdownImageVisualLineTransformer.cs` | Consume list, use max height. |
| `tests/GroundNotes.Tests/MarkdownLineParserTests.cs` | New column-layout assertions. |
| `tests/GroundNotes.Tests/MarkdownImagePreviewProviderTests.cs` | Update existing callers to `Assert.Single`, add column tests. |
| `tests/GroundNotes.Tests/MarkdownImagePreviewLayerTests.cs` | Update reflection helpers, add column hit-test/layout tests. |
| `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` | Compile-check only; likely no changes. |

## Verification

### Automated

```bash
dotnet build GroundNotes.sln
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownLineParserTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewProviderTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewLayerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
dotnet test GroundNotes.sln --no-build
```

### Manual

1. Launch the app.
2. Create a note with:
   ```markdown
   ![](assets/photo1.png)|40||![](assets/photo2.png)|40
   ```
3. Verify two images render side-by-side with a gap.
4. Resize the window — both images should rescale proportionally within their column slots.
5. Add a third column:
   ```markdown
   ![](assets/photo1.png)|40||![](assets/photo2.png)|40||![](assets/photo3.png)|40
   ```
6. Verify all three fit across the line.
7. Mix text and images on one line (e.g., `text ![](a.png)|40||![](b.png)|40`) and confirm it falls back to normal single-image or no-image behavior.
8. Click each image in a column and confirm the context menu / hit-test targets the correct image.

## Edge Cases & Risks

| Risk | Mitigation |
|------|------------|
| **Single-image lines regress** | Existing `IsStandalone` logic is preserved as the fallback path when `IsColumnLayout` is false. Tests must continue to pass unchanged (after updating `GetPreview` call sites to expect a list). |
| **Broken image in a column hides the rest** | Skip unresolved / unloadable images individually rather than aborting the line. |
| **Column width overflows available space** | `maxWidthPerImage` caps each image so that `N * maxWidth + (N-1)*gap <= availableWidth - HorizontalPadding`. |
| **Cache invalidation misses a changed column image** | `InvalidateImage` scans the list of previews in each render-cache entry. |
| **Layer fast paths break** | `RefreshSnapshot` includes `LineText`; changing the number of images changes the text, so a full refresh occurs. Line-state reuse only applies when text is identical, which is correct. |
| **Hit-test ambiguity** | `TryHitTestPreview` iterates in line-number-descending order (as today) and checks every preview rect; the first hit wins. |
| **AvaloniaEdit fork changes not needed** | `SetAdditionalVisualHeight` is a scalar; using the max column height is sufficient. No fork changes required. |
