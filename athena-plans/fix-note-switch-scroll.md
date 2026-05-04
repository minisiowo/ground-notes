# Fix Note Switch Scroll

## Context

When switching between notes, the editor can open the next note at a stale vertical scroll position. The symptom is most noticeable around markdown image previews because image preview lines add render-only visual height, so a pixel offset that belonged to the previous note can map into the middle of a very different visual region in the newly loaded note. Users then see a note appear far down the document, and a later switch or layout pass can make it look like it suddenly jumped back to the top.

Desired behavior: loading a different note into an editor pane should start from a deterministic viewport at the top of that note, unless the app later introduces explicit per-note scroll restoration. Editing the current note in place should not reset scroll, and existing image preview rendering/caching should stay intact.

## Root Cause

Normal note switching updates the existing `TextEditor.Document` in place instead of assigning a new document:

- Primary note load: `MainViewModel.OpenNoteInActivePaneAsync(...)` loads the note, then `ApplyDocumentToEditor(...)` sets `CurrentNote`, `EditorTitle`, `EditorTags`, and `EditorBody`.
- Secondary note load: `LoadPaneNoteAsync(...)` calls `ApplyDocumentToPane(...)`, which sets `pane.CurrentNote` and `pane.EditorBody`.
- View propagation: `MainWindow.OnViewModelPropertyChanged(...)` and `OnSecondaryPaneViewModelPropertyChanged(...)` call `SyncEditorText(...)` / `SyncSecondaryEditorText(...)` when `EditorBody` changes.
- Text replacement: `EditorTextSyncController.SyncFromViewModel(...)` calls `document.Replace(0, document.TextLength, text)` and preserves/clamps the old caret offset. It never resets `TextView.ScrollOffset`.
- Layout refresh: `EditorHostController.RefreshLayoutAfterDocumentReplace()` calls `EditorThemeController.RefreshAfterDocumentReplace()` and `EditorLayoutController.RefreshLayout()`, which invalidates and rebuilds visual lines but also does not reset the viewport.

The AvaloniaEdit fork clears scroll state only when the `TextView.Document` property changes: `TextView.OnDocumentChanged(...)` calls `ClearScrollData()` in `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs`. Because normal note switching keeps the same document instance, the old `TextView` `IScrollable.Offset` survives the note swap.

Markdown images amplify the bug:

- `MarkdownImageVisualLineTransformer.Transform(...)` calls `context.VisualLine.SetAdditionalVisualHeight(maxHeight + VerticalSpacing * 2)` for image preview lines.
- `MarkdownImagePreviewLayer.Refresh()` positions preview overlays from the current `TextView.ScrollOffset` and visible visual lines.
- After a note switch, the preserved pixel offset is interpreted against the new note's variable-height visual line tree. If the new note has image lines, the stale offset often lands around those image previews.

## Fix

Reset the editor viewport when a full model-to-view document replacement represents a note load, while preserving normal in-note editing behavior.

### Step 1 — Add an editor host viewport reset helper

In `src/GroundNotes/Views/EditorHostController.cs`, add a narrow helper such as `ResetViewportToDocumentStart()` that delegates to the layout/text-view layer. Keep it UI-only and editor-local; do not add scroll state to `MainViewModel`.

Recommended implementation shape:

1. Add a public/internal method on `EditorHostController`:
   - `internal void ResetViewportToDocumentStart()`
   - It should reset the wrapped editor's caret/selection to offset `0` when the document is non-null.
   - It should reset `TextArea.TextView` scroll by setting `((IScrollable)_editor.TextArea.TextView).Offset = new Vector(0, 0)`.
2. Because `MainWindow.axaml.cs` already imports `Avalonia.Controls.Primitives`, the same cast can also live there if that is less invasive, but centralizing it in `EditorHostController` keeps primary and secondary editor behavior identical.
3. After setting the offset, invalidate/rebuild the text view if needed by reusing the existing `EditorLayoutController.RefreshLayout()` path or by calling the helper after `RefreshLayoutAfterDocumentReplace()`.

Do not change the AvaloniaEdit fork for this bug. The fork's `TextView` behavior is internally consistent: in-place document edits preserve scroll, document replacement clears it. The app-side note switch path is choosing in-place replacement and therefore must define its own viewport policy.

### Step 2 — Detect full note-load replacements in `MainWindow`

In `src/GroundNotes/Views/MainWindow.axaml.cs`, update the model-to-view sync methods so the reset happens only for full replacements:

- `SyncEditorText(string text)`
- `SyncSecondaryEditorText(EditorPaneViewModel pane)`

Current structure:

```csharp
var changed = host.SyncFromViewModel(..., appendSuffixWhenPossible: false, out var appendedOnly);
if (!changed)
{
    return;
}

if (!appendedOnly)
{
    host.RefreshLayoutAfterDocumentReplace();
}
```

Planned behavior:

1. Keep `appendSuffixWhenPossible: false` unchanged for note loads.
2. When `changed && !appendedOnly`, call `RefreshLayoutAfterDocumentReplace()` first so image-preview visual-line heights are recalculated.
3. Immediately reset the viewport to the document start for the same editor host.
4. Keep `_slashCommandPopup.ScheduleRefresh()` after the primary editor update.

This handles both primary and secondary panes because secondary editors are synchronized through `_secondaryEditorHosts[pane.Id]` and `SyncSecondaryEditorText(...)`.

### Step 3 — Preserve caret semantics intentionally

`EditorTextSyncController.SyncFromViewModel(...)` currently preserves the previous caret offset after a full replace:

```csharp
var caretOffset = Math.Min(_editor.CaretOffset, text.Length);
...
_editor.CaretOffset = caretOffset;
_editor.Select(caretOffset, 0);
```

For note switches, that old caret offset belongs to the previous note and can trigger confusing scroll anchoring. The fix should explicitly move the caret to offset `0` in the new `ResetViewportToDocumentStart()` helper after the text replace.

Do not remove caret preservation from `EditorTextSyncController.SyncFromViewModel(...)` globally unless tests prove it is safe. That controller is also used for non-note-load model-to-view updates and should remain conservative.

### Step 4 — Keep image preview invalidation unchanged

Do not bypass or weaken existing image preview refresh behavior:

- `EditorThemeController.RefreshAfterDocumentReplace()` must continue calling `_imagePreviewLayer.ClearRenderedState()` and `_imagePreviewLayer.RequestRefresh()` when markdown formatting is enabled.
- `MarkdownImagePreviewLayer` should keep its queued refresh coalescing, whole-view no-op refresh skipping, and per-line rendered-state reuse.
- `MarkdownImageVisualLineTransformer` should remain the source that reserves extra visual height for previews.

The viewport reset should be layered after the existing refresh/layout path, not implemented by disabling image preview height or clearing caches more broadly.

## Implementation Order

### Step 1 — Add the reset API

1. Update `EditorHostController` to expose `ResetViewportToDocumentStart()`.
2. If the method needs access to `IScrollable`, add `using Avalonia;` / `using Avalonia.Controls.Primitives;` as needed in the file.
3. Implement the reset against the host's `TextEditor`:
   - Clamp to an empty document safely.
   - `Select(0, 0)` and `CaretOffset = 0`.
   - Set `((IScrollable)_editor.TextArea.TextView).Offset = new Vector(0, 0)`.

### Step 2 — Invoke it on primary note-body replacement

1. In `MainWindow.SyncEditorText(...)`, inside the `if (!appendedOnly)` block, keep `_editorHost.RefreshLayoutAfterDocumentReplace();`.
2. Add `_editorHost.ResetViewportToDocumentStart();` immediately after the refresh.
3. Confirm this method is called by the `EditorBody` property-change path after `ApplyDocumentToEditor(...)` sets the new note body.

### Step 3 — Invoke it on secondary pane note-body replacement

1. In `MainWindow.SyncSecondaryEditorText(EditorPaneViewModel pane)`, inside the `if (!appendedOnly)` block, keep `host.RefreshLayoutAfterDocumentReplace();`.
2. Add `host.ResetViewportToDocumentStart();` immediately after the refresh.
3. Confirm secondary editors created in `OnSecondaryEditorAttachedToVisualTree(...)` still call `SyncSecondaryEditorText(pane)` and therefore receive the same deterministic top reset.

### Step 4 — Add focused regression coverage

Add tests near the editor/view tests rather than the repository tests. Good candidates:

- `tests/GroundNotes.Tests/EditorTextSyncControllerTests.cs` if one exists or is created for sync behavior.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` if test setup benefits from existing `TextEditor`/`TextView` layout helpers.

Test cases to cover:

1. A `TextEditor` with a long document is scrolled via `((IScrollable)editor.TextArea.TextView).Offset = new Vector(0, somePositiveY)`.
2. Sync a different note body through `EditorHostController.SyncFromViewModel(...)`, call `RefreshLayoutAfterDocumentReplace()`, then `ResetViewportToDocumentStart()`.
3. Assert `editor.CaretOffset == 0`, `editor.SelectionStart == 0`, `editor.SelectionLength == 0`, and `editor.TextArea.TextView.ScrollOffset.Y == 0`.
4. Include a markdown image line in the replacement text and attach `EditorThemeController`/image transformer when practical, so the regression covers variable visual heights.

If direct `MainWindow` testing is too heavy, unit-test the new host helper and rely on manual verification for the `MainWindow` call sites.

## Files to Modify

- `src/GroundNotes/Views/EditorHostController.cs`
  - Add the document-start viewport reset helper.
- `src/GroundNotes/Views/MainWindow.axaml.cs`
  - Invoke the helper after full primary and secondary editor body replacements.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` or a new `tests/GroundNotes.Tests/EditorHostControllerTests.cs`
  - Add regression coverage for reset-after-replace behavior.

## Non-Goals

- Do not implement persisted per-note scroll restoration in this fix. That would require a new policy and storage model for primary and secondary panes.
- Do not change markdown image preview syntax, parsing, sizing, bitmap caching, or overlay rendering.
- Do not replace the whole `TextDocument` on every note switch unless the host-level reset proves insufficient; that path has broader undo/caret/selection implications.

## Verification

Run focused validation first:

```bash
dotnet build GroundNotes.sln
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewLayerTests"
```

Then run the full suite:

```bash
dotnet test GroundNotes.sln --no-build
```

Manual regression check:

1. Run the app with `dotnet run --project src/GroundNotes --no-build` after a successful build.
2. Open a long note with image previews and scroll well below the top, preferably to an area around an image.
3. Switch to another note that also contains image previews; it should open at the top every time.
4. Repeat by switching back and forth between several notes; there should be no intermittent old-position reuse or delayed jump back to top.
5. Repeat in a secondary pane and while switching active panes, confirming pane focus does not reintroduce workspace auto-scroll regressions.
