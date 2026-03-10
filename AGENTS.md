# AGENTS.md

This file gives coding agents the repository-specific context needed to work safely and quickly in `quick-notes-txt`.

## Project Summary

- Stack: .NET 10, Avalonia UI 11, xUnit, CommunityToolkit.Mvvm.
- App type: desktop notes app that stores plain-text `.txt` files in a user-selected folder.
- Solution file: `QuickNotesTxt.sln`.
- Test project: `tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj`.

## Repository Layout

- `src/QuickNotesTxt/` - Avalonia application code.
- `src/QuickNotesTxt/Models/` - note and UI data models.
- `src/QuickNotesTxt/Services/` - filesystem, settings, and watcher services.
- `src/QuickNotesTxt/ViewModels/` - MVVM view models.
- `src/QuickNotesTxt/Views/` - Avalonia XAML views and code-behind.
- `src/QuickNotesTxt/Styles/` - themes and shared styles.
- `global.json` - pinned .NET SDK version.
- `mise.toml` - optional tool setup for `mise` users.

## Cursor / Copilot Rules

- No `.cursorrules` file was found.
- No files were found under `.cursor/rules/`.
- No `.github/copilot-instructions.md` file was found.
- Do not assume hidden editor-specific rules exist elsewhere in the repo.

## Required Tooling

- Use .NET SDK `10.0.103` or a compatible newer SDK in the same allowed feature band.
- `global.json` pins the expected SDK.
- `mise.toml` also pins `dotnet = "10.0.103"`.
- Nullable reference types are enabled repository-wide.
- The repo sets `LangVersion` to `latest`.

## Setup Commands

```bash
mise install
dotnet restore QuickNotesTxt.sln
```

- `mise install` is optional; use it only if `mise` is available.
- `dotnet restore QuickNotesTxt.sln` is the standard restore entry point.

## Build Commands

```bash
dotnet build QuickNotesTxt.sln
dotnet build src/QuickNotesTxt/QuickNotesTxt.csproj
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
```

- Prefer solution builds when validating broad changes.
- Use project-only builds for focused app iteration.
- If Avalonia XAML compilation fails with a locked `original.dll`, rerun the build after the conflicting process exits.

## Run Commands

```bash
dotnet run --project src/QuickNotesTxt
```

- Running the app requires a graphical desktop session.
- First launch prompts the user to choose a notes folder.

## Test Commands

```bash
dotnet test QuickNotesTxt.sln
dotnet test QuickNotesTxt.sln --no-build
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build
dotnet test QuickNotesTxt.sln --no-build --list-tests
```

- Prefer `--no-build` after a successful build when iterating quickly.
- Current suite is xUnit-based and mainly covers repository behavior.

## Run A Single Test

Use `--filter` with the fully qualified test name:

```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~QuickNotesTxt.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```

Useful variants:

```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "Name~SerializeAndParse"
```

- Discover exact names with `dotnet test QuickNotesTxt.sln --no-build --list-tests`.
- Keep single-test commands project-scoped unless there is a good reason to hit the whole solution.

## Lint / Format Expectations

- No dedicated lint command is configured in the repository.
- No `.editorconfig` file is present in the repository root.
- Do not invent a required lint step in commits or automation docs.
- When making edits, follow existing formatting conventions and keep diffs minimal.

## C# Code Style

- Use file-scoped namespaces.
- Use 4-space indentation.
- Keep one top-level type per file.
- Prefer `sealed` for concrete classes unless inheritance is intended.
- Use `PascalCase` for types, methods, properties, enums, and enum members.
- Use `_camelCase` for private fields.
- Prefix interfaces with `I`.
- Suffix asynchronous methods with `Async`.

## Imports And Dependencies

- Order `using` directives with `System.*` first, then third-party namespaces, then `QuickNotesTxt.*`.
- Keep `using` blocks simple; do not add alias imports unless they clearly reduce confusion.
- Add dependencies only when they are necessary and consistent with the app's lightweight scope.

## Types And Nullability

- Treat nullable annotations as meaningful design signals, not noise.
- Prefer explicit null handling over the null-forgiving operator.
- Use `var` when the right-hand side makes the type obvious; otherwise prefer explicit types.
- Favor `IReadOnlyList<T>` in APIs that should not expose mutation.

## Naming Conventions

- Match established names such as `NotesRepository`, `FileWatcherService`, and `MainViewModel`.
- Name methods by behavior, not implementation detail.
- Keep boolean names positive and readable, for example `HasSelectedFolder` and `ShowEditorWatermark`.

## Error Handling And Control Flow

- Prefer guard clauses and early returns.
- Catch only expected exceptions.
- Swallow exceptions only for known benign cases already justified by UI or cancellation behavior.
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string and path comparisons.
- Keep filesystem behavior deterministic and explicit.

## MVVM / Avalonia Guidance

- Keep business logic in services and view models, not in code-behind.
- Keep Avalonia code-behind focused on windowing, input wiring, and UI-specific event handling.
- Follow the existing CommunityToolkit.Mvvm pattern: `[ObservableProperty]`, `[RelayCommand]`, partial hooks, and `ObservableObject` base types.
- Preserve the current theme/resource architecture when editing UI styling.
- Keep the terminal-style typography and compact desktop layout consistent with existing styles.

## Testing Guidance

- Add or update tests when changing repository parsing, serialization, filtering, rename behavior, or deletion behavior.
- Prefer focused unit tests in `tests/QuickNotesTxt.Tests/NotesRepositoryTests.cs` style.
- Use temp directories for filesystem tests and clean them up in `Dispose()`.

## Change Checklist For Agents

- Read the surrounding file before editing.
- Keep changes scoped to the user's request.
- Avoid unrelated renames or formatting churn.
- Build the solution after non-trivial code changes.
- Mention any commands you could not run or any environment-specific failures.

## Validation Baseline

- `dotnet build QuickNotesTxt.sln` succeeds in this repository.
- `dotnet test QuickNotesTxt.sln --no-build` passes.
- `dotnet test ... --filter` works for single-test execution.

## Notes For Future Agents

- The repo currently has no dedicated AGENT-specific editor rules to merge in.
- If future Cursor or Copilot rule files are added, update this document to include and reconcile them.
- Keep this file repository-specific; avoid generic advice that is not grounded in the actual codebase.
