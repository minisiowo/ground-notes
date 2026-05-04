# Fix Annotation Caret Height

## Context

Image annotation text editing now feels visually close to writing directly on the image: the editor background and border are transparent, and committed text stays aligned with the text being edited. One remaining mismatch is the caret. While editing a text annotation in `ImageViewerWindow`, the caret appears much taller than the visible annotation font, so it no longer feels like a natural text cursor on the image.

Desired behavior:

- The annotation text caret should visually match the current annotation text size.
- The caret should stay aligned with the text baseline while typing, dragging, resizing text via the annotation size control, and zooming the image.
- Committed annotation text must remain in the same position as the edited text.
- The fix should be scoped to the image annotation editor, not global app text boxes.

## Root Cause

The annotation text editor has font-size-aware width logic but still keeps fixed minimum heights:

- `src/GroundNotes/Views/ImageViewerWindow.axaml`
  - `AnnotationTextEditor` has `MinHeight="34"`.
  - `AnnotationTextBox` has `MinHeight="32"`.
  - `AnnotationTextBox` uses `Classes="annotationTextEditor"`, `Padding="0"`, and a transparent background/border.

- `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`
  - `BeginTextAnnotationEdit(...)` sets `AnnotationTextBox.FontSize = fontSize * GetViewportImageScale()`.
  - `UpdateActiveTextEditorPosition(...)` updates `AnnotationTextBox.FontSize` and `AnnotationTextEditor.Width` as zoom/text size changes.
  - `MeasureTextEditorWidth(...)` uses `ImageAnnotationLayer.CreateTextLayout(...)` for width, but no equivalent height calculation is applied to the editor.

- `src/GroundNotes/Styles/AppStyles.axaml`
  - Global `TextBox` style sets `MinHeight=30` and app-wide caret resources.
  - `TextBox.annotationTextEditor` resets background, border, padding, and caret color, but does not override min-height/height or line metrics.

The likely result is that Avalonia's `TextBox` caret is sized according to the text box line box / available editor height rather than just the annotation glyph metrics. The fixed `MinHeight` values are now visually exposed because the editor is transparent.

## Fix

Use text metrics to size the annotation editor vertically, the same way the existing code already sizes it horizontally.

### Step 1: Remove Fixed Annotation Editor Minimum Heights

File: `src/GroundNotes/Views/ImageViewerWindow.axaml`

Change the annotation editor controls so they do not force a 32/34px line box:

- Remove or reduce `MinHeight="34"` from `AnnotationTextEditor`.
- Remove or reduce `MinHeight="32"` from `AnnotationTextBox`.
- Keep `Padding="0"`, transparent background, and `BorderThickness="0"` to preserve the direct-on-image feel.

If Avalonia requires a nonzero minimum to render/focus correctly, use the smallest safe value and let code-behind set the actual height.

### Step 2: Override Annotation TextBox Minimum Height in Styles

File: `src/GroundNotes/Styles/AppStyles.axaml`

Extend the `TextBox.annotationTextEditor` selector so global `TextBox MinHeight=30` does not leak into the annotation editor:

```xml
<Setter Property="MinHeight" Value="0" />
```

If supported by Avalonia `TextBox`, also consider setting line metrics on this class only:

```xml
<Setter Property="LineHeight" Value="NaN or unset-compatible default" />
```

Only add `LineHeight` if it compiles and demonstrably improves caret sizing. Do not alter global `TextBox` or editor text box styles.

### Step 3: Add Text Editor Height Measurement

File: `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`

Add a height counterpart to the current width measurement.

Current width method:

- `MeasureTextEditorWidth(string text, double fontSize)`
  - calls `ImageAnnotationLayer.CreateTextLayout(...)`
  - returns `layout.WidthIncludingTrailingWhitespace + 8`

Add one of these options:

```csharp
private static double MeasureTextEditorHeight(string text, double fontSize)
```

or a combined helper:

```csharp
private static Size MeasureTextEditorSize(string text, double fontSize)
```

Use the same placeholder logic as width (`"Text"` for empty input), the same font family/path via `ImageAnnotationLayer.CreateTextLayout(...)`, and return `layout.Height` plus only minimal safety padding if needed. Keep the padding small because any extra vertical space may affect caret height.

### Step 4: Apply Height Whenever Font Size or Text Changes

File: `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`

Create a helper to keep width and height updates together, for example:

```csharp
private void UpdateAnnotationTextEditorSize()
```

Use it in all places that currently update only width:

- `BeginTextAnnotationEdit(...)`
  - after setting `AnnotationTextBox.Text` and `AnnotationTextBox.FontSize`
- `OnAnnotationTextBoxTextChanged(...)`
- `UpdateActiveTextEditorPosition(...)`
  - after recalculating `AnnotationTextBox.FontSize`
- `UpdateActiveTextSizeFromAnnotationSize(...)` indirectly through `UpdateActiveTextEditorPosition()`

The helper should set:

- `AnnotationTextEditor.Width`
- `AnnotationTextEditor.Height`
- `AnnotationTextBox.Height` if needed

Keep the existing minimum width behavior (`Math.Max(160, ...)`) unless manual testing shows it affects text editing. Do not add vertical padding that would reintroduce a tall caret.

### Step 5: Preserve Position and Hit-Testing Semantics

File: `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`

Keep `Canvas.SetLeft(AnnotationTextEditor, viewportPoint.X)` and `Canvas.SetTop(AnnotationTextEditor, viewportPoint.Y)` unchanged unless manual testing shows baseline drift.

Important related methods:

- `CancelTextAnnotationEdit(bool commit)` stores `ImageAnnotationText` at `_pendingTextLocation`.
- `ImageAnnotationLayer.DrawText(...)` draws committed text at that image location.
- `GetTextAnnotationViewportBounds(...)` uses `ImageAnnotationLayer.CreateTextLayout(...)` for committed text hit-testing.

The implementation should make the editor height match the same `TextLayout` height used for rendering/hit-testing rather than shifting either the editor origin or committed annotation origin.

## Files to Modify

- `src/GroundNotes/Views/ImageViewerWindow.axaml`
- `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`
- `src/GroundNotes/Styles/AppStyles.axaml`

## Non-Goals

- Do not change annotation text rendering in `ImageAnnotationLayer.DrawText(...)` unless measurement reveals a clear mismatch.
- Do not change annotation font family, color, or export rendering.
- Do not change global `TextBox` caret behavior outside `TextBox.annotationTextEditor`.
- Do not introduce a custom text editing control unless TextBox sizing cannot solve the issue.

## Risks

- Too-small heights can clip glyph ascenders/descenders or make the caret hard to see.
- Setting both `Height` and `MinHeight` incorrectly can break focus/click handling on the transparent editor.
- If Avalonia's caret height is template-internal and not derived from control height, a style/template change may still be needed after the first pass.
- Zooming changes `AnnotationTextBox.FontSize`; height must update on the same path as width or the caret can become wrong after zoom.
- Text annotation dragging uses the editor control as the pointer-capture target; reducing height too much may make dragging less forgiving.

## Verification

Build:

```bash
dotnet build src/GroundNotes/GroundNotes.csproj
```

Optional regression tests:

```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ImageAnnotationStrokeSmootherTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewLayerTests"
```

Manual checks:

1. Open an image preview and activate text annotation mode.
2. Click on the image and type text; confirm caret height matches the visible font size.
3. Press `Enter`; confirm committed text does not shift.
4. Edit an existing text annotation; confirm caret height and position match the existing text.
5. Change annotation size while editing text; confirm caret height updates with text size.
6. Zoom in and out while editing text; confirm caret height follows the scaled font.
7. Drag the active text editor; confirm the smaller editor height does not make dragging unusable.
