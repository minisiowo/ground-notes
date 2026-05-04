# Fix Editor Caret Height

## Context

The requested caret-height issue is about the general text editor used throughout the app, not the image annotation text overlay. GroundNotes uses a forked `AvaloniaEdit` editor for notes, secondary panes, and chat note display. The caret currently appears too tall compared with the editor font, especially when line-height settings add extra vertical space around text.

Desired behavior:

- The caret in the main note editor should match the visible text glyph height, not the full line box height.
- The fix should apply consistently to primary editor, secondary panes, and chat editor because they share `AvaloniaEdit`.
- Existing caret x-position fixes for visually indented markdown code blocks must be preserved.
- Selection rectangles can continue using full line height unless the user explicitly asks to change selection visuals.

## Current Implementation

### App-side editor setup

- `src/GroundNotes/Views/MainWindow.axaml`
  - Primary editor: `EditorTextEditor`, `Classes="editorTextBox"`.
  - Secondary pane editors also use `ae:TextEditor Classes="editorTextBox"`.

- `src/GroundNotes/Views/ChatWindow.axaml`
  - `ChatTextEditor`, also `Classes="editorTextBox"`.

- `src/GroundNotes/Views/EditorHostController.cs`
  - Shared wrapper that constructs `EditorThemeController`, `EditorLayoutController`, and related editor behavior for both main and chat editors.

- `src/GroundNotes/Views/EditorLayoutController.cs`
  - Applies editor layout options, including `TextEditor.Options.LineHeightFactor` from persisted editor layout settings.

- `src/GroundNotes/Views/EditorThemeController.cs`
  - Refreshes typography/theme resources and presentation layers for each `TextEditor`.

- `src/GroundNotes/Styles/AppStyles.axaml`
  - `ae|TextEditor.editorTextBox` sets editor foreground/background/border and terminal font resources.
  - `TextBox.editorTextBox` is unrelated to `AvaloniaEdit` caret geometry.

### AvaloniaEdit caret path

- `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/Caret.cs`
  - `Caret.CalculateCaretRectangle()` returns the geometry used by scrolling and rendering.
  - `CalcCaretRectangle(VisualLine visualLine)` computes normal insert-mode caret geometry.
  - `CalcCaretOverstrikeRectangle(VisualLine visualLine)` computes overstrike-mode geometry.
  - Current insert-mode code uses:
    - `VisualYPosition.LineTop`
    - `VisualYPosition.LineBottom`
    - height = `lineBottom - lineTop`

- `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/CaretLayer.cs`
  - Draws the caret rectangle produced by `Caret`.

- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualYPosition.cs`
  - Explicitly distinguishes full line box positions from text glyph positions:
    - `LineTop` / `LineBottom`: full `TextLine` bounds.
    - `TextTop` / `TextBottom`: top/bottom of actual text; for larger line-height or inline UI elements this can differ from line bounds.

- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs`
  - Maintains `DefaultLineHeight`, `DefaultTextHeight`, and `DefaultBaseline`.
  - `DefaultLineHeight` is affected by line-height factor and is used in line layout.

## Root Cause

`Caret.CalcCaretRectangle(...)` sizes the insert-mode caret to the full visual line box:

```csharp
var lineTop = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.LineTop);
var lineBottom = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.LineBottom);
return new Rect(xPos, lineTop, CaretWidth, lineBottom - lineTop);
```

This means caret height grows with `Options.LineHeightFactor` and any extra vertical line spacing. That is useful for a full-line caret but visually wrong for GroundNotes, where the user expects the caret to correspond to the glyph height.

`VisualYPosition.TextTop` / `TextBottom` already provide the text-only vertical bounds needed for this fix.

## Fix

### Step 1: Change Insert-mode Caret Height to Text Bounds

File: `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/Caret.cs`

Update `CalcCaretRectangle(VisualLine visualLine)` to use `TextTop` and `TextBottom` instead of `LineTop` and `LineBottom`:

```csharp
var textTop = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextTop);
var textBottom = visualLine.GetTextLineVisualYPosition(textLine, VisualYPosition.TextBottom);
return new Rect(xPos, textTop, CaretWidth, Math.Max(CaretWidth, textBottom - textTop));
```

Use a minimum height guard so unusual/empty/zero-height cases still render a visible caret.

Important: keep the existing x-position clamp:

```csharp
var minXPos = GetSelectableStartXPosition(visualLine, textLine);
if (xPos < minXPos) xPos = minXPos;
```

That clamp was added to keep caret positions inside visually indented markdown code blocks.

### Step 2: Review Overstrike Mode Separately

File: `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/Caret.cs`

Do not blindly change overstrike mode unless it also looks wrong.

Current overstrike behavior:

- If caret is within text, it uses `textLine.GetTextBounds(...)`, which is already tied to glyph/text bounds.
- If caret is at end of line or in virtual space, it falls back to `LineTop` / `LineBottom`.

Recommended first pass:

- Leave the within-text overstrike path unchanged.
- Consider changing only the end-of-line/virtual-space fallback to `TextTop` / `TextBottom` for consistency with normal caret height.
- Preserve width logic (`xPos2 - xPos`) and minimum width guard.

### Step 3: Add Focused Regression Test

File: `tests/GroundNotes.Tests/EditorThemeControllerTests.cs`

Add a test near existing caret/geometry tests, likely near `CodeBlockCaret_ClampsToVisualIndentationOnContentAndBlankLines()`.

Suggested test name:

```csharp
EditorCaret_UsesTextHeightInsteadOfLineHeight()
```

Test outline:

1. Create a `TextEditor` using existing helper `CreateEditor(...)` or a minimal editor helper.
2. Set a document with a simple line, e.g. `"hello"`.
3. Ensure visual lines are valid.
4. Get the visual line and text line for line 1.
5. Set caret offset inside the line.
6. Calculate:
   - `lineHeight = LineBottom - LineTop`
   - `textHeight = TextBottom - TextTop`
   - `caretRect = editor.TextArea.Caret.CalculateCaretRectangle()`
7. Assert:
   - caret height is approximately `textHeight`
   - caret height is less than `lineHeight` when line-height factor makes a difference
   - caret top is approximately `TextTop`

If the existing helper does not expose line-height factor control, set `editor.Options.LineHeightFactor` directly before layout/visual-line construction. Use a factor large enough that `lineHeight > textHeight`, e.g. `1.6` or `2.0`.

Also extend the existing code-block caret test to assert that the blank-line caret still clamps horizontally after changing height; do not remove its current top assertion unless the top moves from `LineTop` to `TextTop`. If that assertion currently expects line top, update it intentionally to `TextTop` for caret-specific behavior.

### Step 4: Avoid App-side Styling Workarounds

Do not try to solve the general editor caret height from `AppStyles.axaml` or `EditorThemeController` because `AvaloniaEdit` caret height is not controlled by Avalonia `TextBox` caret styling. The actual rectangle is produced in the fork.

No app-side `TextEditor` style changes should be required for this fix.

## Files to Modify

Primary:

- `extern/AvaloniaEdit/src/AvaloniaEdit/Editing/Caret.cs`
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs`

Likely no changes needed:

- `src/GroundNotes/Styles/AppStyles.axaml`
- `src/GroundNotes/Views/EditorThemeController.cs`
- `src/GroundNotes/Views/EditorLayoutController.cs`
- `src/GroundNotes/Views/MainWindow.axaml`
- `src/GroundNotes/Views/ChatWindow.axaml`

## Behavior to Preserve

- Caret x-position remains clamped to the first selectable/document-backed visual element for markdown code block indentation.
- `BringCaretToView()` still works because it uses `CalculateCaretRectangle()`.
- Caret remains visible on empty lines, blank lines, wrapped lines, and virtual-space/end-of-line positions.
- Overstrike mode remains usable.
- Selection geometry remains unchanged unless explicitly required.
- Image annotation text editor behavior is out of scope.

## Risks

- This is a fork-level editor change and affects all `AvaloniaEdit` instances.
- Using `TextTop` / `TextBottom` may move caret top downward compared with previous line-top positioning. That is intended if the goal is glyph-height caret, but tests with line-top assumptions need updating.
- Blank lines may have text bounds that differ from content lines; ensure the caret remains visible and not zero-height.
- Inline object lines or markdown image preview lines can have line boxes much taller than text. Text-height caret is desired near text, but manual validation should check caret behavior around image preview blocks.
- Overstrike mode end-of-line fallback may need special handling if text bounds are unavailable or unexpectedly small.

## Verification

Build fork and app:

```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet build src/GroundNotes/GroundNotes.csproj
```

Run focused tests:

```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
```

Optional broader validation:

```bash
dotnet build GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```

Manual checks:

1. Open the main note editor and type on a normal line; caret should match text height.
2. Change editor line-height setting in settings; caret should stay text-height while line spacing changes.
3. Check primary pane, secondary pane, and chat editor.
4. Check markdown code blocks, including blank lines inside code blocks, to confirm x-position clamping is preserved.
5. Check wrapped lines and image-preview-adjacent lines.
