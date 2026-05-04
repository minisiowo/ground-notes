# Fix Image Note Cold-Start Delay

## Context

After launching the app, the first click on a note containing markdown images has a noticeable delay. Opening image notes again afterwards is responsive. Repository evidence points to a cold image-preview path rather than note parsing alone:

- `MainViewModel.OpenNoteInActivePaneAsync(...)` loads the note and assigns `EditorBody` through `ApplyDocumentToEditor(...)`.
- `MainWindow.SyncEditorText(...)` responds to `EditorBody`, replaces the AvaloniaEdit document through `EditorHostController.SyncFromViewModel(...)`, then immediately calls `EditorHostController.RefreshLayoutAfterDocumentReplace()`.
- `EditorHostController.RefreshLayoutAfterDocumentReplace()` calls `EditorThemeController.RefreshAfterDocumentReplace()` and `EditorLayoutController.RefreshLayout()`.
- `EditorLayoutController.RefreshLayout()` forces `TextView.EnsureVisualLines()` synchronously.
- While visual lines are built, `MarkdownImageVisualLineTransformer.Transform(...)` calls `MarkdownImagePreviewProvider.GetPreview(...)`.
- `MarkdownImagePreviewProvider.GetPreview(...)` parses image markdown, resolves file paths through `NoteAssetService.ResolveImagePath(...)`, checks file stamps, and on a bitmap-cache miss calls `new Bitmap(resolvedPath)` synchronously in `TryGetBitmap(...)`.

The provider has useful caches (`_previewLineCache`, `_previewRenderCache`, bounded `_bitmapCache`) and the layer has coalescing/reuse fast paths, so repeat opens are fast. The first open after app startup still pays bitmap decoder initialization and first image decode during the note-switch/layout path.

## Root Cause

The first image-heavy note switch blocks on cold bitmap work while the editor is trying to make the new document visible. The highest-risk point is `MarkdownImagePreviewProvider.TryGetBitmap(...)`:

```csharp
var bitmap = new Bitmap(resolvedPath);
```

That call occurs synchronously during visual-line construction and can be triggered by `TextView.EnsureVisualLines()` in `EditorLayoutController.RefreshLayout()`. Once `_bitmapCache` is warmed, later image notes avoid the expensive decode path or at least avoid first-use codec initialization, which matches the user-visible “only after startup” symptom.

There may also be smaller contributors:

- first `File.GetLastWriteTimeUtc` / `FileInfo` work in `TryGetFileStamp(...)`,
- first markdown image parsing for visible lines,
- forced layout rebuild immediately after document replacement,
- one independent `MarkdownImagePreviewProvider` per `EditorHostController`, so primary/secondary panes do not share warmth.

## Approach

Make the first image note switch responsive by **deferring cold bitmap-cache misses until after the document text has been applied and the UI has had a chance to render**, while preserving the existing synchronous/cached path for warm images.

The intended behavior:

1. On note/document replacement, the initial layout pass may parse image lines but should not synchronously decode uncached bitmaps.
2. Uncached image paths discovered during that first pass are queued for deferred bitmap loading.
3. The note text appears quickly.
4. Deferred bitmap loading runs at a lower dispatcher priority after the initial render/input work.
5. When queued bitmaps are cached, the image preview layer invalidates and refreshes so previews appear.
6. Subsequent opens use `_bitmapCache` and continue to render previews immediately.

This avoids relying on thread-safety of `Avalonia.Media.Imaging.Bitmap` construction. Keep bitmap creation on the UI thread unless profiling or Avalonia documentation proves it is safe off-thread. Moving decode to a background thread is a separate, higher-risk follow-up.

## Implementation Steps

### Step 1 — Add diagnostics for cold/deferred bitmap work

Extend `src/GroundNotes/Editors/MarkdownDiagnostics.cs` and `MarkdownDiagnosticsSnapshot` with counters such as:

- `DeferredBitmapLoadRequests`
- `DeferredBitmapLoads`
- `DeferredBitmapLoadSkips`

Record:

- when `TryGetBitmap(...)` sees a cache miss and queues it instead of decoding synchronously,
- when the deferred drain decodes a bitmap,
- when a queued item is skipped because it is stale, already cached, missing, or the provider was disposed.

This gives a testable signal and helps confirm the fix addresses the cold path.

### Step 2 — Add deferred cold-load state to `MarkdownImagePreviewProvider`

Modify `src/GroundNotes/Editors/MarkdownImagePreviewProvider.cs`.

Add private state:

- `_deferColdBitmapLoads` flag,
- `_deferredBitmapLoadQueued` flag,
- `_deferredBitmapGeneration` integer,
- a pending collection keyed by resolved path, e.g. `Dictionary<string, DeferredBitmapLoad>` using `StringComparer.OrdinalIgnoreCase`,
- an event or callback, e.g. `public event EventHandler? DeferredBitmapLoadsCompleted;`.

Suggested record:

```csharp
private readonly record struct DeferredBitmapLoad(string ResolvedPath, FileStamp FileStamp, int Generation);
```

Add methods:

```csharp
public void BeginDeferredColdBitmapLoads()
public void EndDeferredColdBitmapLoads()
```

Behavior:

- `BeginDeferredColdBitmapLoads()` sets `_deferColdBitmapLoads = true`, increments `_deferredBitmapGeneration`, and clears pending loads from older generations.
- `EndDeferredColdBitmapLoads()` sets `_deferColdBitmapLoads = false` but does not clear already queued loads for the current generation.
- `SetBaseDirectoryPath(...)`, `Detach()`, and `Dispose()` must increment generation or clear pending loads so old note paths cannot refresh the current document.

### Step 3 — Queue bitmap misses instead of decoding during deferred mode

In `TryGetBitmap(string resolvedPath, FileStamp fileStamp)`:

1. Keep the existing bitmap-cache hit path unchanged.
2. If an entry exists with a mismatched file stamp, remove it as today.
3. Before `new Bitmap(resolvedPath)`, check `_deferColdBitmapLoads`.
4. If deferred mode is active:
   - add/update the pending item for `resolvedPath`,
   - record a deferred request diagnostic,
   - call `RequestDeferredBitmapLoadDrain()`,
   - return `null`.
5. If deferred mode is not active, keep the existing synchronous decode path.

`RequestDeferredBitmapLoadDrain()` should use `Dispatcher.UIThread.Post(...)` at `DispatcherPriority.Background` or `DispatcherPriority.ContextIdle` so the note click and first render are not blocked. Keep the queued/drain flag so many visible image lines coalesce into one drain.

The drain method should:

- copy pending items for the current generation,
- clear them from the pending dictionary,
- for each item, skip if generation is stale or a matching cache entry now exists,
- re-check the file stamp before decoding,
- call the existing decode/cache insertion logic,
- trim the LRU as today,
- raise `DeferredBitmapLoadsCompleted` only if at least one bitmap was cached.

Extract the current synchronous decode body into a helper like `TryLoadBitmapIntoCache(...)` to avoid duplicating LRU/eviction logic.

### Step 4 — Enable deferred mode only around document replacement

Modify `src/GroundNotes/Views/EditorThemeController.cs`.

In `RefreshAfterDocumentReplace()`:

- call `_imagePreviewProvider.BeginDeferredColdBitmapLoads()` before clearing/render refresh work,
- keep `_imagePreviewLayer.ClearRenderedState()` and `_imagePreviewLayer.RequestRefresh()` as today,
- schedule `EndDeferredColdBitmapLoads()` after the first render-priority refresh has had a chance to discover visible image misses.

One concrete shape:

```csharp
_imagePreviewProvider.BeginDeferredColdBitmapLoads();
_imagePreviewLayer.ClearRenderedState();
if (_markdownFormattingEnabled)
{
    _imagePreviewLayer.RequestRefresh();
}
Dispatcher.UIThread.Post(_imagePreviewProvider.EndDeferredColdBitmapLoads, DispatcherPriority.Background);
```

Subscribe in the constructor:

```csharp
_imagePreviewProvider.DeferredBitmapLoadsCompleted += OnDeferredBitmapLoadsCompleted;
```

Unsubscribe in `Dispose()`.

`OnDeferredBitmapLoadsCompleted` should:

- return if markdown formatting is disabled,
- invalidate preview render state,
- call `TextView.Redraw()` or invalidate measure/arrange as needed so `MarkdownImageVisualLineTransformer` recalculates preview heights,
- request the preview layer refresh.

Prefer the least expensive refresh that makes images appear reliably. If `Redraw()` alone is enough in tests, avoid forcing `EnsureVisualLines()` from the completion handler. If layout height does not update, use the same targeted invalidation pattern already used by `RefreshImagePreviews(...)`.

### Step 5 — Avoid duplicate synchronous work in `EditorLayoutController.RefreshLayout()` only if needed

Do not remove `TextView.EnsureVisualLines()` as the first fix; it is used by many editor layout tests and recent selection/caret/image work.

If profiling after Steps 1–4 still shows a large first-open delay, then add a second phase:

- split `RefreshLayout()` into an eager layout path and a deferred layout path,
- call the deferred path from `EditorHostController.RefreshLayoutAfterDocumentReplace()` for image documents only,
- keep the eager path for typography/settings changes and tests that need immediate visual lines.

This is higher risk because it can affect caret position, scroll reset, slash popup positioning, and image preview layer state.

### Step 6 — Optional prewarm after startup, separate from the must-do path

If deferred cold loading improves responsiveness but the first image appearance is still too slow, add a non-blocking prewarm pass after folder load/window open:

- identify a small number of likely notes to prewarm (current selected note, first visible note, or most recent notes),
- load only their text with `NotesRepository.LoadNoteAsync(...)` on a background task,
- marshal preview-provider cache priming to the UI dispatcher at idle,
- cap total images/time so startup does not become slower.

Keep this optional. The primary fix should be that clicking an image note no longer blocks on cold decode.

## Tests

### Provider tests

Update `tests/GroundNotes.Tests/MarkdownImagePreviewProviderTests.cs`.

Add tests for deferred behavior:

- `GetPreview_QueuesColdBitmapLoadWhenDeferred`
  - create a temp image note,
  - call `BeginDeferredColdBitmapLoads()`,
  - call `GetPreview(...)`,
  - assert it returns empty and records a deferred request instead of a bitmap cache miss,
  - run dispatcher jobs or an internal test helper to drain pending loads,
  - call `EndDeferredColdBitmapLoads()` if needed,
  - call `GetPreview(...)` again and assert the preview appears with a bitmap cache hit or no second decode.

- `DeferredBitmapLoad_IsDiscardedAfterBaseDirectoryChanges`
  - queue a deferred load,
  - call `SetBaseDirectoryPath(...)` to simulate note/folder switch,
  - drain dispatcher,
  - assert stale queued work does not populate the current preview cache or fire a misleading refresh.

- `DeferredBitmapLoad_CoalescesDuplicateVisibleLines`
  - use two lines referencing the same image,
  - defer both,
  - assert only one deferred decode occurs.

If dispatcher-driven tests are brittle, add an `internal` method guarded by `InternalsVisibleTo`-compatible test access, e.g. `DrainDeferredBitmapLoadsForTests()`, and keep production scheduling separate.

### Layer/controller tests

Update `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` or `MarkdownImagePreviewLayerTests.cs`.

Add a controller-level test such as:

- `RefreshAfterDocumentReplace_DefersColdImageBitmapDecodeUntilAfterInitialRefresh`
  - create an editor with an image markdown line,
  - attach `EditorThemeController`,
  - reset diagnostics,
  - simulate document replace and call `RefreshAfterDocumentReplace()`,
  - ensure the immediate path does not perform bitmap-cache misses synchronously,
  - flush dispatcher and assert previews eventually render.

Also preserve existing tests that verify:

- cache reuse on repeated refresh,
- rendered line state reuse during forced refresh,
- bitmap cache eviction,
- invalidation on file overwrite,
- image preview hit testing.

## Files to Modify

- `src/GroundNotes/Editors/MarkdownImagePreviewProvider.cs` — primary change: deferred cold bitmap load queue, generation/stale protection, completion event, extracted decode/cache helper.
- `src/GroundNotes/Editors/MarkdownDiagnostics.cs` — deferred-load counters for tests and profiling.
- `src/GroundNotes/Views/EditorThemeController.cs` — enable deferred mode around document replacement and refresh the preview layer when deferred loads complete.
- `tests/GroundNotes.Tests/MarkdownImagePreviewProviderTests.cs` — deferred queue/drain/cache/stale-generation tests.
- `tests/GroundNotes.Tests/EditorThemeControllerTests.cs` or `tests/GroundNotes.Tests/MarkdownImagePreviewLayerTests.cs` — integration coverage that first refresh is non-decoding and later refresh displays previews.
- Possibly `src/GroundNotes/Views/EditorHostController.cs` — only if the controller needs a named document-replace preview mode instead of direct `EditorThemeController` behavior.
- Possibly `src/GroundNotes/Views/EditorLayoutController.cs` — only for the second phase if deferring bitmap decode is not enough.

## Risks and Edge Cases

- Image previews may appear a moment after text on the first cold open. That is acceptable if it removes the note-click freeze, but manual UX review should confirm the transition is not jarring.
- Changing preview height after deferred load can move text below image lines. Ensure `TextView.Redraw()`/layout invalidation recalculates visual-line heights correctly.
- Rapid note switching can leave queued work for the previous note. Use generation tokens and base-directory checks to discard stale loads.
- Cache invalidation on document edits, width changes, base-directory changes, and on-disk image changes must remain intact.
- Preserve the bounded LRU bitmap cache; do not keep extra strong references outside `_bitmapCache`/rendered previews.
- Do not move `Bitmap` construction off the UI thread without a separate proof/plan.
- Do not bypass `MarkdownImagePreviewLayer` coalescing, whole-view no-op refresh skipping, or per-line rendered-state reuse.

## Verification

Targeted commands:

```bash
dotnet build src/GroundNotes/GroundNotes.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewProviderTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownImagePreviewLayerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MainViewModelTests"
```

Broader validation:

```bash
dotnet build GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```

Manual regression checks:

1. Cold-launch the app with a folder containing image-heavy notes.
2. Immediately click an image note and verify the text/editor switches promptly.
3. Confirm image previews appear shortly afterward without requiring a second click or scroll.
4. Click the same note again and another image note; repeat opens should remain fast.
5. Edit an image line and verify preview invalidation still works.
6. Overwrite an existing image file on disk and verify refresh/invalidation still shows the updated image when `RefreshImagePreviews(...)` is triggered.
7. Check scrolling/caret reset on note switch still starts at the document top.
8. If this is ready to try on Windows, run the repository's WSL deployment validation:

```bash
bash scripts/publish-and-install-wsl.sh
```
