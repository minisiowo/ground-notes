# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

QuickNotesTxt is a desktop plain-text notes app built with .NET 10, Avalonia UI 11, and CommunityToolkit.Mvvm. Notes are stored as `.txt` files with YAML-like frontmatter (title, tags, createdAt, updatedAt) in a user-selected folder.

## Commands

```bash
dotnet build QuickNotesTxt.sln            # Build everything
dotnet run --project src/QuickNotesTxt     # Run the app (requires graphical session)
dotnet test QuickNotesTxt.sln              # Run all tests
dotnet test QuickNotesTxt.sln --no-build   # Run tests without rebuilding

# Single test
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build \
  --filter "FullyQualifiedName~NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"

# Discover test names
dotnet test QuickNotesTxt.sln --no-build --list-tests

# Release build
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
```

No lint or format command is configured. SDK version is pinned in `global.json` (.NET 10.0.103, `rollForward: latestFeature`).

## Architecture

**MVVM pattern** using CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`, partial hooks).

- **Services layer** (`Services/`): `NotesRepository` handles file I/O, frontmatter serialization/parsing, search/filter/sort logic. `FileWatcherService` monitors the notes folder for external changes. `FolderSettingsService` persists user preferences. All services have interface abstractions (`INotesRepository`, etc.).
- **ViewModels** (`ViewModels/`): `MainViewModel` is the primary VM — manages note selection, editor state, debounced auto-save (450ms), file watcher suppression, inline rename, tag filtering, and theme/font settings.
- **Models** (`Models/`): `NoteDocument` (full note with frontmatter), `NoteSummary` (list display), `SortOption` enum.
- **Views** (`Views/`): Avalonia XAML + code-behind. `MainWindow` handles windowing/input; `ConfirmDeleteWindow` for delete confirmation.
- **Styles** (`Styles/`): `AppTheme` defines built-in themes; `ThemeService` applies them.

Key data flow: editor changes -> `UpdateCurrentNoteFromEditor()` -> `ScheduleSave()` -> 450ms debounce -> `SaveNoteAsync()` which serializes frontmatter and writes to disk. External file changes trigger `FileWatcherService.NotesChanged` -> `RefreshFromDiskAsync()` (suppressed briefly after own writes).

## Code Style

- File-scoped namespaces, 4-space indentation, one type per file
- `sealed` for concrete classes unless inheritance is needed
- `_camelCase` private fields, `PascalCase` everything else, `I` prefix for interfaces
- `Async` suffix on async methods
- `using` order: `System.*`, third-party, `QuickNotesTxt.*`
- Nullable reference types enabled repo-wide (`Directory.Build.props`)
- Use `StringComparison.Ordinal`/`OrdinalIgnoreCase` for string and path comparisons
- Guard clauses and early returns preferred
- Keep business logic in services/view models, not code-behind

## Testing

Tests are xUnit-based in `tests/QuickNotesTxt.Tests/`. `NotesRepositoryTests` covers repository parsing, serialization, filtering, rename, and deletion using temp directories cleaned up via `Dispose()`. Add/update tests when changing repository behavior.
