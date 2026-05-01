# Preserve Note Timestamps During Bulk Image Rename

## Context

When a user renames an image via the editor context menu, `RenameImageReferenceInAllNotesAsync` scans **all notes** in the folder and updates every markdown reference to that image. For each affected note (excluding the current primary note), it loads the note, replaces the image URL, and calls `_noteMutationService.SaveAsync`, which ultimately calls `NotesRepository.SaveNoteAsync`.

`NotesRepository.SaveNoteAsync` unconditionally sets:

```csharp
persisted.UpdatedAt = DateTime.Now;
```

This means a mechanical bulk reference update — not initiated by the user editing the note — changes the `UpdatedAt` timestamp of every note that happens to contain that image. This is misleading: the user did not edit those notes.

## Root Cause

The save pipeline has no concept of a "mechanical" or "reference-only" save. Every call to `SaveNoteAsync` overwrites `UpdatedAt`.

## Fix

Thread an optional `preserveTimestamp` flag through the save pipeline so callers can opt out of timestamp mutation.

### 1. `INotesRepository`

Add `bool preserveTimestamp = false` to `SaveNoteAsync`:

```csharp
Task<NoteDocument> SaveNoteAsync(
    string folderPath,
    NoteDocument document,
    CancellationToken cancellationToken = default,
    bool preserveTimestamp = false);
```

### 2. `NotesRepository.SaveNoteAsync`

Wrap the timestamp assignment:

```csharp
if (!preserveTimestamp)
{
    persisted.UpdatedAt = DateTime.Now;
}
```

### 3. `INoteMutationService`

Add the same parameter to `SaveAsync`:

```csharp
Task<NoteDocument> SaveAsync(
    string folderPath,
    NoteDocument document,
    CancellationToken cancellationToken = default,
    bool preserveTimestamp = false);
```

### 4. `NoteMutationService.SaveAsync`

Pass `preserveTimestamp` through to `_notesRepository.SaveNoteAsync`.

### 5. `RenameImageReferenceInAllNotesAsync`

In the bulk loop, call:

```csharp
await _noteMutationService.SaveAsync(NotesFolder, note, cancellationToken, preserveTimestamp: true);
```

The current primary note (updated via the editor's normal autosave path) continues to update its timestamp because the editor edit is a genuine user modification.

### 6. Tests

- Add a test in `NotesRepositoryTests` that saves with `preserveTimestamp: true` and asserts `UpdatedAt` is unchanged.
- Add a test in `NoteMutationServiceTests` verifying the flag is forwarded.

## Files to Modify

| File | Change |
|------|--------|
| `src/GroundNotes/Services/INotesRepository.cs` | Add `preserveTimestamp` parameter. |
| `src/GroundNotes/Services/NotesRepository.cs` | Conditionally assign `UpdatedAt`. |
| `src/GroundNotes/Services/INoteMutationService.cs` | Add `preserveTimestamp` parameter. |
| `src/GroundNotes/Services/NoteMutationService.cs` | Forward parameter. |
| `src/GroundNotes/ViewModels/MainViewModel.Notes.cs` | Pass `preserveTimestamp: true` in bulk loop. |
| `tests/GroundNotes.Tests/NotesRepositoryTests.cs` | Add `preserveTimestamp` test. |
| `tests/GroundNotes.Tests/NoteMutationServiceTests.cs` | Add forwarding test. |

## Verification

```bash
dotnet build GroundNotes.sln
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~NoteMutationServiceTests"
```
