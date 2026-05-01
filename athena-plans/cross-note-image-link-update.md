# Cross-Note Image Link Update on Rename

## Context

When a user renames an image asset via the editor's image context menu (`Rename image...`), the markdown link is updated **only in the current editor**. If the same image is referenced in other notes, those references become broken because the file on disk was moved but their text was not updated.

The rename flow is in `MainWindow.RenameImageAssetAsync`:
1. `RenameImageWindow` asks for a new file name.
2. `NoteAssetService.TryBuildRenameAssetPath` builds the new disk path and markdown path.
3. `File.Move` renames the asset on disk.
4. `MarkdownImageEditingCommands.RenameImageUrl` + `ApplyEditorEdit` updates only the current editor's text.

## Desired Behavior

After moving the image file on disk, every note in the current notes folder that references that image must have its markdown link updated to the new path.

## Approach

### 1. Text Replacement Helper

Add a private static helper to `MainViewModel.Notes.cs` that performs precise image-URL replacement using `MarkdownLineParser`:

- Split the note text into lines (preserving `\r\n` / `\n` endings).
- For each line, call `MarkdownLineParser.Analyze(line, MarkdownFenceState.None)`.
- For each parsed image, resolve its URL via `NoteAssetService.ResolveImagePath`.
- If the resolved path matches the old resolved path (ordinal-ignore-case), replace **only** the `Url` span with the new markdown path.
- Process images right-to-left within a line so offsets remain valid.
- Return the modified text, or `null` if nothing changed.

This ensures:
- Only actual image URLs are replaced (no false positives in plain text).
- Scale suffixes (`|40`) and column separators (`||`) are preserved automatically because we only touch the `Url` span.
- Line endings are preserved.

### 2. Bulk Update Method on `MainViewModel`

Add a new public async method `RenameImageReferenceInAllNotesAsync` to `MainViewModel`:

```csharp
public async Task<int> RenameImageReferenceInAllNotesAsync(
    string oldResolvedPath,
    string oldMarkdownUrl,
    string newMarkdownPath,
    NoteAssetService noteAssetService,
    CancellationToken cancellationToken = default)
```

Logic:
1. Guard if `NotesFolder` is empty or missing.
2. Enumerate all note file paths via `NotesRepository.EnumeratePreferredNoteFiles(NotesFolder)`.
3. Call `SuppressWatcher()` once before the loop to avoid triggering file-watcher reloads.
4. Iterate each note file:
   - **Current primary note**: skip (the caller already updates the editor directly).
   - **Open in a secondary pane**: update `pane.EditorBody` with the replaced text if changed.
   - **All other notes**: load via `_notesRepository.LoadNoteAsync`, replace in `Body`, save via `_noteMutationService.SaveAsync` inside a `BeginMutationScope()`.
5. Return the count of notes updated.

### 3. Update `MainWindow.RenameImageAssetAsync`

After the successful `File.Move` and the current editor edit, call:

```csharp
var updatedCount = await vm.RenameImageReferenceInAllNotesAsync(
    hit.ResolvedPath,
    GetEditorText(editor).Substring(hit.UrlStart, hit.UrlLength),
    newMarkdownPath,
    _noteAssetService);
```

Update the status message to reflect cross-note updates, e.g.:
- `"Renamed image to {fileName} (updated in {updatedCount} other note(s))"`

### 4. Handle Edge Cases

| Edge Case | Handling |
|-----------|----------|
| **Note is open in secondary pane** | Update `pane.EditorBody` so the pane's autosave writes the change. |
| **Note is the current primary note** | Skip in the bulk loop; the editor edit already updates it. |
| **Image referenced with different relative paths** | Resolved via `NoteAssetService.ResolveImagePath`, so both `assets/photo.png` and `photo.png` match the same file. |
| **Image inside fenced code block** | `MarkdownLineParser` still parses it; we update it because it's a real reference even if not rendered. |
| **File watcher triggers reload** | `SuppressWatcher()` sets a 900 ms suppression window, covering the bulk saves. |
| **Save fails for one note** | Catch `IOException` / `UnauthorizedAccessException`, continue with remaining notes, report failure in status. |
| **No other notes reference the image** | Return 0; status message still shows the rename succeeded. |

## Files to Modify

| File | Nature of Change |
|------|------------------|
| `src/GroundNotes/ViewModels/MainViewModel.Notes.cs` | Add `TryReplaceImageReferences` helper and `RenameImageReferenceInAllNotesAsync` method. Add `using GroundNotes.Editors;`. |
| `src/GroundNotes/Views/MainWindow.axaml.cs` | Update `RenameImageAssetAsync` to call the new ViewModel method and adjust the status message. |
| `tests/GroundNotes.Tests/MarkdownEditingCommandsTests.cs` or new test file | Add unit tests for `TryReplaceImageReferences` helper. |

## Implementation Order

1. Add `using GroundNotes.Editors;` to `MainViewModel.Notes.cs`.
2. Implement `TryReplaceImageReferences` static helper in `MainViewModel.Notes.cs`.
3. Implement `RenameImageReferenceInAllNotesAsync` in `MainViewModel.Notes.cs`.
4. Update `RenameImageAssetAsync` in `MainWindow.axaml.cs`.
5. Add unit tests for the helper.
6. Build and run tests.

## Verification

### Automated

```bash
dotnet build GroundNotes.sln
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownEditingCommandsTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MainViewModelTests"
```

### Manual

1. Create two notes, `note-a.md` and `note-b.md`.
2. In both notes, insert `![](assets/photo.png)|40`.
3. Ensure `assets/photo.png` exists on disk.
4. In `note-a.md`, right-click the image preview and choose **Rename image...**.
5. Rename to `newphoto.png`.
6. Verify `note-a.md` now shows `![](assets/newphoto.png)|40`.
7. Open `note-b.md` and verify it also shows `![](assets/newphoto.png)|40`.
8. Check that `assets/newphoto.png` exists and `assets/photo.png` does not.
