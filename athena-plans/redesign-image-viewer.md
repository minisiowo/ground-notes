# Redesign Image Viewer

## Context

The markdown image viewer currently works functionally well: clicking a rendered markdown image preview opens `ImageViewerWindow`, supports pan/zoom, pen/text annotations, copy-to-clipboard, save-copy, and overwrite. The problem is visual consistency. `ImageViewerWindow.axaml` is one of the most custom-styled surfaces in the app and still uses hardcoded backdrop, toolbar, swatch, and slider colors instead of the shared theme resources used by the main window, dialogs, popups, and editor overlays.

Desired behavior:

- Keep the current image viewer and annotation workflow intact.
- Redesign the viewer chrome, annotation toolbar, action buttons, color/size controls, and text editor overlay so they feel like part of the app theme.
- Prefer existing semantic resources (`PaneBackgroundBrush`, `SurfaceHoverBrush`, `FocusBorderBrush`, `AppTextBrush`, `SecondaryTextBrush`, etc.) over adding a parallel viewer theme system.
- Preserve the fullscreen image-first experience unless the redesign explicitly decides otherwise.

## Current Implementation

### Viewer and Annotation UI

- `src/GroundNotes/Views/ImageViewerWindow.axaml`
  - Fullscreen borderless window with hardcoded `Background="#CC000000"` on both the `Window` and `RootGrid`.
  - Top toolbar is a plain `DockPanel` with two `StackPanel`s.
  - Tool toggles: `PenToggleButton`, `TextToggleButton` use `Classes="secondaryButton"`, but there is no checked-state style specific to viewer tools.
  - Size control: `AnnotationSizeControl` uses hardcoded track `#7A7A7A` and thumb `#DF795F`.
  - Color swatches use hardcoded button backgrounds/tags: red `#E5484D`, yellow `#F5D547`, green `#30A46C`, blue `#3B82F6`, white `#F8FAFC`, black `#111827`.
  - Actions use mixed text/icon buttons: `Undo`, `Save copy`, `Copy`, `Overwrite`, `-`, `Fit`/percentage, `+`, `X`.
  - `AnnotationTextEditor` / `AnnotationTextBox` are transparent and use `Classes="annotationTextEditor"` plus sidebar font resources.

- `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`
  - Owns all interaction and state: `_annotations`, `_activeTool`, `_annotationColor`, `_annotationSize`, `_zoom`, `_pan`, text-edit state, save/copy delegates.
  - `SetActiveTool(...)` updates toggle checks and sets `ViewerViewport.Cursor` to `Cross` for pen/text modes.
  - `OnWindowPointerPressedForAnnotationToolbar(...)` treats any control under the root toolbar `DockPanel` as toolbar interaction to avoid committing text edits while changing toolbar settings.
  - `UpdateAnnotationSizeThumb()` only positions the thumb; visual colors are XAML-only.
  - `ApplyAnnotationTextBoxColor(Color)` sets `AnnotationTextBox.Foreground` and `CaretBrush` directly from the selected annotation color.
  - `UpdateAnnotationButtons()` enables/disables undo/save/copy/overwrite based on annotation count and delegates.

- `src/GroundNotes/Views/ImageAnnotationLayer.cs`
  - Draws committed and active annotations onto the live viewer.
  - `DrawAnnotation(...)` is reused by `ImageViewerWindow.RenderAnnotatedBitmap()` for export/copy, so visual annotation rendering is already centralized.
  - `CreateTextLayout(...)` uses `ThemeKeys.SidebarFont`, `SidebarFontWeight`, and `SidebarFontStyle` but annotation color remains user-selected.

### Theme System and Reusable Patterns

- `src/GroundNotes/Styles/AppStyles.axaml`
  - Existing app visual language is based on small-radius surfaces, thin borders, muted text, and consistent hover/pressed/focus states.
  - Useful existing classes/resources:
    - `Border.sectionSurface`, `Border.titleBarSurface`, `Border.toolPopupSurface`
    - `Button.secondaryButton`, `Button.compactIconButton`, `Button.titleBarButton`, `Button.titleBarCloseButton`
    - `TextBlock.metaText`, `TextBlock.mutedText`, `TextBlock.titleBarText`
    - `TextBox.editorTextBox`, `TextBox.annotationTextEditor`
  - Generic `ToggleButton` active styling is not well-defined except for tag filter chips/rows, so viewer tool toggles need a dedicated checked-state style.

- `src/GroundNotes/Styles/ThemeKeys.cs`, `ThemeBuilder.cs`, `ThemeSemanticTokens.cs`, `ThemeService.cs`
  - Existing semantic resources cover most viewer chrome needs: `AppBackgroundBrush`, `PaneBackgroundBrush`, `SurfaceBackgroundBrush`, `SurfaceHoverBrush`, `SurfacePressedBrush`, `SurfaceRaisedBrush`, `BorderBrushBase`, `FocusBorderBrush`, `AppTextBrush`, `SecondaryTextBrush`, `MutedTextBrush`, `PlaceholderTextBrush`, `TitleBarButtonHoverBrush`, `TitleBarCloseHoverBrush`, `MenuSurfaceBrush`.
  - Adding new theme keys is possible but cross-cutting because it affects resource constants, token construction, runtime application, custom theme loading/export, and tests.

- Theme-aligned window/dialog examples:
  - `src/GroundNotes/Views/RenameImageWindow.axaml`
  - `src/GroundNotes/Views/ConfirmDeleteWindow.axaml`
  - `src/GroundNotes/Views/KeyboardShortcutsHelpWindow.axaml`
  - `src/GroundNotes/Views/TitleBarControl.axaml`

## Design

### Keep the Viewer Fullscreen, but Make the Chrome Theme-Aware

Keep `ImageViewerWindow` as a fullscreen borderless viewer because the existing click-outside-image-to-close behavior, image-first layout, and topmost fullscreen opening are part of the current workflow. Do not convert it to a normal dialog unless explicitly requested later.

Redesign the surface as:

1. A viewer backdrop using a dynamic theme resource instead of inline `#CC000000`.
2. A compact top command bar styled as a theme surface, visually closer to `sectionSurface`/`toolPopupSurface` than a raw transparent `DockPanel`.
3. Grouped controls: annotation tools, annotation styling, save/export actions, zoom/window actions.
4. Dedicated style classes for viewer-specific controls, still built from existing theme resources.

Recommended first pass: avoid new `ThemeKeys` and define viewer styles only in `AppStyles.axaml` using existing brushes. If the backdrop needs opacity, use local XAML brush definitions or an app style resource in `AppStyles.axaml`; do not expand the theme schema unless reuse of existing brushes cannot produce acceptable contrast.

### Proposed Visual Structure

Update `ImageViewerWindow.axaml` from a raw toolbar to a structured command surface:

- `RootGrid`
  - Replace hardcoded backgrounds with theme-aware resources. Candidate: `AppBackgroundBrush` with `Opacity`, or a viewer-specific `SolidColorBrush` resource in the window/styles that uses a darkened app background if practical.
  - Keep `Margin="12"` or move to `Padding` on a root container; preserve viewer viewport clipping.

- Top command bar
  - Wrap the toolbar in a `Border Classes="imageViewerToolbarSurface"` with:
    - `Background={DynamicResource PaneBackgroundBrush}` or `MenuSurfaceBrush`
    - `BorderBrush={DynamicResource BorderBrushBase}`
    - `BorderThickness="1"`
    - `CornerRadius="1"`
    - compact padding such as `6`
  - Use nested groups with a new `Border Classes="imageViewerToolbarGroup"` or simple `StackPanel` plus separators.
  - Preserve the root toolbar ancestry assumption used by `OnWindowPointerPressedForAnnotationToolbar(...)`, or update that method to target a named toolbar root instead of any `DockPanel` parent.

- Tool toggles
  - Add `ToggleButton.imageViewerToolButton` style in `AppStyles.axaml`.
  - Checked state should use `SurfacePressedBrush` + `FocusBorderBrush`; pointerover should use `SurfaceHoverBrush`; disabled should use `MutedTextBrush`.
  - Keep current `PenToggleButton` / `TextToggleButton` names and `IsCheckedChanged` handlers.

- Annotation size control
  - Keep `AnnotationSizeControl`, `AnnotationSizeThumb`, and pointer handlers.
  - Replace track/thumb hardcoded colors with themed styles:
    - track: `BorderBrushBase` or `SecondaryTextBrush` with opacity,
    - thumb outer border: `FocusBorderBrush`,
    - thumb fill: selected annotation color if possible, otherwise `SurfaceRaisedBrush`.
  - If implementing selected-color thumb fill in code, update `UpdateAnnotationSizeThumb()` or `OnAnnotationColorClick(...)` to set `AnnotationSizeThumb.Fill` from `_annotationColor` so the slider previews the active annotation color.

- Color swatches
  - Keep fixed annotation paint colors; these are content colors, not theme chrome colors.
  - Add a theme-aware swatch style such as `Button.imageViewerColorSwatch` that supplies consistent border, hover, focus, and selected indication.
  - Add selected swatch tracking in code-behind rather than relying only on `_annotationColor`:
    - store the current swatch button or update pseudo-class/classes when color changes,
    - selected swatch should have `FocusBorderBrush`/accent border and maybe slightly larger inner fill.
  - Preserve `Tag` values so `ResolveAnnotationButtonColor(Button)` keeps working.

- Action buttons
  - Use consistent text casing and app patterns. Suggested labels: `undo`, `save copy`, `copy`, `overwrite`, `-`, `%`, `+`, `X` only if the rest of the app keeps `X`; otherwise consider `close` if visual space allows.
  - Keep names and click handlers unchanged: `UndoAnnotationButton`, `SaveCopyButton`, `CopyAnnotatedButton`, `OverwriteButton`, `ZoomResetButton`.
  - For destructive-ish `Overwrite`, consider a dedicated class `imageViewerDangerButton` using existing danger only if available through theme resources. Since `Danger` is not exposed as a brush resource today, the first pass can keep it neutral and rely on tooltip text.

- Text annotation editor
  - Keep transparent text rendering for the actual annotation color, but make the edit affordance visible:
    - use a subtle `BorderBrush={DynamicResource FocusBorderBrush}` / `BorderThickness=1`, maybe `Background={DynamicResource PaneBackgroundBrush}` with low opacity or a small padding.
    - Preserve `AnnotationTextBox.Foreground` and `CaretBrush` being set to the annotation color by `ApplyAnnotationTextBoxColor(Color)`.
  - Ensure `MeasureTextEditorWidth(...)` still matches rendered text after any padding changes; if adding horizontal padding, include it in the width calculation.

## Implementation Steps

### Step 1: Define Viewer Style Classes

Modify only styling first, before touching interaction code.

File: `src/GroundNotes/Styles/AppStyles.axaml`

Add a dedicated image viewer section near the existing button/textbox styles, with classes such as:

- `Border.imageViewerToolbarSurface`
- `Border.imageViewerToolbarGroup` or `Separator.imageViewerToolbarSeparator`
- `ToggleButton.imageViewerToolButton`
- `Button.imageViewerActionButton` if `secondaryButton` is not enough
- `Button.imageViewerColorSwatch`
- `Button.imageViewerColorSwatch.selected` or pseudo-class support if implemented from code
- `Border.imageViewerAnnotationTextEditor` if replacing direct border styling on `AnnotationTextEditor`

Base these styles on current resources:

- surfaces: `PaneBackgroundBrush`, `SurfaceBackgroundBrush`, `SurfaceHoverBrush`, `SurfacePressedBrush`, `SurfaceRaisedBrush`, `MenuSurfaceBrush`
- borders/focus: `BorderBrushBase`, `FocusBorderBrush`
- text: `AppTextBrush`, `SecondaryTextBrush`, `MutedTextBrush`, `PlaceholderTextBrush`
- typography: `SidebarFont`, `SidebarFontWeight`, `SidebarFontStyle`, `AppFontSize`, `AppFontSizeSmall`

Avoid adding new entries to `ThemeKeys.cs` unless existing resources cannot satisfy the redesign.

### Step 2: Restructure `ImageViewerWindow.axaml`

File: `src/GroundNotes/Views/ImageViewerWindow.axaml`

Change the visual tree while preserving named controls and event hookups:

- Replace inline `Background="#CC000000"` on `Window` and `RootGrid` with dynamic resource-backed styling.
- Wrap the command bar in a named container, e.g. `Border x:Name="AnnotationToolbar" Classes="imageViewerToolbarSurface"`.
- Keep `RootGrid` row structure (`Auto,*`) so `ViewerViewport` remains in row 1.
- Keep all existing named controls:
  - `PenToggleButton`
  - `TextToggleButton`
  - `AnnotationSizeControl`
  - `AnnotationSizeThumb`
  - color buttons and their `Tag` values
  - `UndoAnnotationButton`
  - `SaveCopyButton`
  - `CopyAnnotatedButton`
  - `OverwriteButton`
  - `ZoomResetButton`
  - `ViewerViewport`, `ViewerImage`, `AnnotationEditCanvas`, `AnnotationTextEditor`, `AnnotationTextBox`
- Replace direct color values on size track/thumb and swatches where they represent chrome. Keep swatch fill colors as annotation paint values.
- Apply new style classes instead of repeating `MinWidth`, `Height`, `Padding`, `BorderBrush`, and similar details on every control.

Important: preserve `ViewerViewport` pointer handlers and `ClipToBounds=True`; those are central to drawing, panning, and hit-testing.

### Step 3: Make Toolbar Detection Robust

File: `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`

Update `OnWindowPointerPressedForAnnotationToolbar(...)` if the toolbar is no longer a `DockPanel` whose parent is `RootGrid`.

Preferred implementation after naming the toolbar root:

```csharp
_isPointerDownInAnnotationToolbar = e.Source is Control control
    && control.FindAncestorOfType<Border>() is { } border
    && ReferenceEquals(border, AnnotationToolbar);
```

or use a helper that walks ancestors until it finds the named toolbar. This preserves the existing text-edit behavior where clicking toolbar controls does not commit/cancel the active text annotation.

### Step 4: Add Selected Tool and Selected Color Affordances

File: `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`

The tool selected state already exists through `ToggleButton.IsChecked`. Ensure the new `ToggleButton.imageViewerToolButton:checked` style makes this visible.

For color swatches, add explicit selected-state UI:

- Add a private field such as `_selectedColorButton` if necessary.
- In the constructor after `InitializeComponent()`, initialize the red/default selected state.
- In `OnAnnotationColorClick(...)`, after `_annotationColor = ResolveAnnotationButtonColor(button)`, update the selected swatch class/pseudo-class.
- If using classes, remove `selected` from the previous button and add it to the new one.
- Update `AnnotationSizeThumb.Fill` from the selected color so the size control communicates the active color.

Do not change the annotation model (`ImageAnnotationStroke`, `ImageAnnotationText`) or persisted/exported bitmap behavior.

### Step 5: Polish Text Annotation Editing Without Changing Export

Files:

- `src/GroundNotes/Views/ImageViewerWindow.axaml`
- `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`
- `src/GroundNotes/Views/ImageAnnotationLayer.cs` only if typography changes are truly needed

Make the active text editor look intentional while preserving annotation color:

- Add a visible themed border/background around `AnnotationTextEditor`.
- Keep `AnnotationTextBox.Background="Transparent"` if the annotation text should appear directly on the image.
- Keep `ApplyAnnotationTextBoxColor(Color)` as the source of text/caret color.
- If adding padding to `AnnotationTextBox` or `AnnotationTextEditor`, update `MeasureTextEditorWidth(...)` and `UpdateActiveTextEditorPosition(...)` so editing boxes do not clip text or shift unexpectedly.
- Keep `ImageAnnotationLayer.CreateTextLayout(...)` and `DrawText(...)` export behavior unchanged unless there is a clear need to alter annotation text rendering.

### Step 6: Optional Theme Schema Expansion Only If Needed

If reuse of existing resources cannot produce a good viewer backdrop or toolbar contrast, add viewer-specific theme resources in a focused follow-up within the same implementation:

Files likely affected:

- `src/GroundNotes/Styles/ThemeKeys.cs`
- `src/GroundNotes/Styles/ThemeSemanticTokens.cs`
- `src/GroundNotes/Styles/ThemeBuilder.cs`
- `src/GroundNotes/Styles/ThemeService.cs`
- `src/GroundNotes/Styles/ThemeTokenOverrides.cs`
- `src/GroundNotes/Services/ThemeLoaderService.cs` if custom JSON import/export validates known properties
- `tests/GroundNotes.Tests/ThemeBuilderTests.cs`
- `tests/GroundNotes.Tests/ThemeLoaderServiceTests.cs`

Potential keys, only if justified:

- `ImageViewerBackdropBrush`
- `ImageViewerToolbarBrush`
- `ImageViewerToolbarBorderBrush`
- `ImageViewerAnnotationControlBrush`

Non-goal for the first pass: make annotation swatch colors theme-configurable. They are user paint colors and should remain stable unless a separate request asks for configurable palettes.

## Files to Modify

Primary files:

- `src/GroundNotes/Views/ImageViewerWindow.axaml`
- `src/GroundNotes/Views/ImageViewerWindow.axaml.cs`
- `src/GroundNotes/Styles/AppStyles.axaml`

Only if necessary:

- `src/GroundNotes/Styles/ThemeKeys.cs`
- `src/GroundNotes/Styles/ThemeSemanticTokens.cs`
- `src/GroundNotes/Styles/ThemeBuilder.cs`
- `src/GroundNotes/Styles/ThemeService.cs`
- `src/GroundNotes/Styles/ThemeTokenOverrides.cs`
- `src/GroundNotes/Services/ThemeLoaderService.cs`
- `tests/GroundNotes.Tests/ThemeBuilderTests.cs`
- `tests/GroundNotes.Tests/ThemeLoaderServiceTests.cs`

Files to preserve behavior in, but avoid modifying unless required:

- `src/GroundNotes/Views/ImageAnnotationLayer.cs`
- `src/GroundNotes/Views/ImageAnnotationStrokeSmoother.cs`
- `src/GroundNotes/Views/MainWindow.axaml.cs`

## Behavior to Preserve

- `ImageViewerWindow.TryOpen(...)` loads valid image files, returns `false` on missing/unloadable files, and opens the viewer owned by the main window.
- Viewer starts fullscreen/topmost and fits the image on open.
- `Escape` closes the viewer unless text editing is active; when text editing is active, `Escape` cancels the edit.
- Clicking outside the image closes the viewer when no annotation tool is active.
- Pan with no active tool still works.
- Mouse wheel zoom remains anchored at the pointer.
- Pen mode still captures pointer, smooths strokes through `ImageAnnotationStrokeSmoother`, and commits on release.
- Text mode still creates text on image click, edits existing text by reverse-order hit test, supports dragging the active text editor, commits on `Enter`/lost focus, cancels on `Escape`, and restores original text when cancelling an edit.
- Clicking toolbar controls while editing text must not prematurely commit/cancel the text edit.
- `RenderAnnotatedBitmap()` continues exporting at native bitmap resolution using `ImageAnnotationLayer.DrawAnnotation(...)`.
- Save-copy, copy, and overwrite button enablement remains controlled by `UpdateAnnotationButtons()`.
- Successful save clears annotations, reloads the displayed bitmap, and refits the viewport; copy leaves annotations intact.
- `MainWindow.SaveAnnotatedImageAsync(...)` and markdown URL rewrite/preview refresh behavior remain unchanged.

## Risks

- Toolbar restructuring can break `_isPointerDownInAnnotationToolbar` and cause text annotations to commit when changing color/size/tool.
- Adding padding/borders to `AnnotationTextEditor` can desynchronize text hit-testing, editor width measurement, and visual placement.
- The viewer is fullscreen/topmost/borderless, so contrast issues are more visible than in normal dialogs.
- `ToggleButton` styling can be template-sensitive under FluentTheme; styles should target `ToggleButton.imageViewerToolButton /template/ ContentPresenter#PART_ContentPresenter` the same way existing button styles target `PART_ContentPresenter`.
- Theme schema changes are broad; prefer `AppStyles.axaml` classes using existing resources unless a new semantic resource is clearly required.
- There are no direct automated UI tests for `ImageViewerWindow`; manual regression is required.

## Verification

Build/test commands:

```bash
dotnet build src/GroundNotes/GroundNotes.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ImageAnnotationStrokeSmootherTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewLayerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewProviderTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~NoteAssetServiceTests"
```

If theme keys/schema are changed, also run:

```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ThemeBuilderTests|FullyQualifiedName~ThemeLoaderServiceTests"
```

Manual regression checklist:

1. Open an image from a markdown preview.
2. Confirm viewer opens fullscreen and visually matches the current app theme in both dark and light/custom themes.
3. Verify toolbar hover/pressed/checked/disabled states for pen, text, undo, save copy, copy, overwrite, zoom, fit, and close.
4. Change annotation colors and confirm selected swatch and size thumb reflect the current color.
5. Change annotation size and verify thumb movement and actual stroke/text size remain correct.
6. Draw pen strokes, including slow and fast strokes.
7. Add, edit, drag, commit, and cancel text annotations.
8. While editing text, click color/size/tool controls and verify focus handling remains correct.
9. Pan and zoom with no active tool; reset fit; close with `X`, outside-image click, and `Escape`.
10. Save copy, copy annotated image, and overwrite; confirm markdown preview refreshes after save/overwrite.
11. Reopen the viewer after changing app theme/font settings if live resource changes are supported while the window is open.
