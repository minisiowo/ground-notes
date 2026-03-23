# CLAUDE.md

Quick reference for Claude/Codex working in `ground-notes`.

GroundNotes is a note app built around plain text files in a user-selected folder. Notes are stored as `.txt` or `.md` files with YAML-like frontmatter and the UI is Avalonia-based with AI prompt actions plus a dedicated chat window.

## Commands
```bash
dotnet build GroundNotes.sln            # Build everything
dotnet run --project src/GroundNotes    # Run the app (requires graphical session)
dotnet test GroundNotes.sln             # Run all tests
dotnet test GroundNotes.sln --no-build  # Run tests without rebuilding
```

Single test:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build \
  --filter "FullyQualifiedName~NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```

List tests:
```bash
dotnet test GroundNotes.sln --no-build --list-tests
```

Publish:
```bash
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release
```

## Architecture
- `MainViewModel` coordinates editor state, selection/filter/sort, save orchestration, AI prompt execution, settings persistence, and opening the chat window.
- `NotesRepository` owns frontmatter parsing/serialization and filesystem note operations.
- `FileWatcherService` handles external file changes.
- `FolderSettingsService` persists folder/theme/font/window preferences plus AI settings.
- `ChatViewModel` owns the AI chat transcript, attached note context, model selection, and saving conversations back through `NotesRepository`.

## Style
- File-scoped namespaces.
- 4-space indentation.
- One top-level type per file.
- Prefer `sealed` classes.
- Use guard clauses and small cohesive methods.
- `using` order: `System.*`, third-party, `GroundNotes.*`

## Testing
Tests are xUnit-based in `tests/GroundNotes.Tests/`. `NotesRepositoryTests` covers repository parsing, serialization, filtering, rename, and deletion using temp directories cleaned up via `Dispose()`. Add or update tests when changing repository behavior.
