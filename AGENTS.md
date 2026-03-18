# AGENTS.md

This document gives agentic coding assistants repository-specific instructions for working in `quick-notes-txt`.

## Purpose
- Build, test, and modify the codebase safely with minimal churn.
- Follow existing architecture and conventions before introducing new patterns.
- Keep behavior deterministic for filesystem, parsing, and settings flows.

## Project Snapshot
- App: desktop plain-text notes app.
- Stack: .NET 10, Avalonia UI 11, CommunityToolkit.Mvvm, xUnit.
- Notes format: `.txt` files with YAML-like frontmatter plus body.
- Solution: `QuickNotesTxt.sln`.
- App project: `src/QuickNotesTxt/QuickNotesTxt.csproj`.
- Test project: `tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj`.

## Repository Layout
- `src/QuickNotesTxt/Models/`: note, theme, font, and AI models.
- `src/QuickNotesTxt/Services/`: repository, watcher, settings, theme, font, prompt, and OpenAI services.
- `src/QuickNotesTxt/ViewModels/`: MVVM state, commands, and save orchestration.
- `src/QuickNotesTxt/Views/`: Avalonia XAML and code-behind.
- `src/QuickNotesTxt/Styles/`: app themes and style resources.
- `src/QuickNotesTxt/Assets/`: bundled fonts and prompt assets.
- `tests/QuickNotesTxt.Tests/`: xUnit tests for services and models.

## Cursor/Copilot Rules Status
- `.cursorrules`: not present.
- `.cursor/rules/`: not present.
- `.github/copilot-instructions.md`: not present.
- No editor-specific rule files were found in this repository.

## Toolchain and Environment
- SDK pin: `.NET 10.0.103` via `global.json`.
- `rollForward` is set to `latestFeature`.
- `Directory.Build.props` enables nullable reference types and implicit usings.
- No dedicated lint command is configured.

## Setup Commands
```bash
dotnet restore QuickNotesTxt.sln
```

Optional local tooling setup:
```bash
mise install
```

## Build Commands
```bash
dotnet build QuickNotesTxt.sln
dotnet build src/QuickNotesTxt/QuickNotesTxt.csproj
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
```

Notes:
- Prefer solution-level build for full validation.
- If Avalonia artifacts are locked by another process, stop conflicting process and rerun.

## Run Command
```bash
dotnet run --project src/QuickNotesTxt
```

Notes:
- Requires a graphical desktop session.

## Test Commands
Run all tests:
```bash
dotnet test QuickNotesTxt.sln
dotnet test QuickNotesTxt.sln --no-build
```

Run project tests:
```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build
```

List tests:
```bash
dotnet test QuickNotesTxt.sln --no-build --list-tests
```

## Single-Test Commands (Important)
Use `--filter` and prefer fully-qualified names:
```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~QuickNotesTxt.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```

Useful filters:
```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~ThemeLoaderServiceTests"
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "Name~SerializeAndParse"
```

Single-test workflow:
- Build once (`dotnet build`).
- Iterate with `dotnet test --no-build --filter ...`.
- Use `--list-tests` if exact names are unclear.

## Linting and Formatting
- No linter command is defined in this repo.
- No repo `.editorconfig` currently defines a formatting policy.
- Do not invent mandatory lint/format steps in CI notes.
- Match surrounding formatting and keep diffs small.

## Architecture Guidance
- Use MVVM with CommunityToolkit.Mvvm patterns.
- Keep business logic in `Services` and `ViewModels`, not view code-behind.
- `MainViewModel` owns editor state, save scheduling, filtering, picker state, and commands.
- `NotesRepository` owns frontmatter parse/serialize, file naming, filtering, and scoring logic.
- `FileWatcherService` handles external file-change monitoring and refresh triggers.
- `FolderSettingsService` persists folder/theme/font/window and related preferences.

## C# Style and Formatting
- Use file-scoped namespaces.
- Use 4-space indentation.
- Keep one top-level type per file.
- Prefer guard clauses and early returns over deep nesting.
- Keep methods focused; extract private helpers for repeated logic.
- Use expression-bodied members only when they improve readability.

## Imports and Dependencies
- `using` order:
  1) `System.*`
  2) third-party namespaces
  3) `QuickNotesTxt.*`
- Remove redundant imports.
- Prefer existing .NET/Avalonia APIs over new dependencies.
- Add packages only when clearly justified by requirements.

## Types, Nullability, and Data Shapes
- Treat nullable annotations as part of the contract.
- Prefer explicit null checks over null-forgiving (`!`) where possible.
- Use `var` when type is obvious from RHS; otherwise use explicit type.
- Keep model and DTO shapes explicit and stable.
- Preserve deterministic serialization/parsing behavior.

## Naming Conventions
- `PascalCase`: types, methods, properties, enums, enum values.
- `_camelCase`: private fields.
- `I` prefix for interfaces.
- Use descriptive method names based on behavior.
- Keep boolean names readable and positive (`Is...`, `Has...`, `Can...`).

## Error Handling and Control Flow
- Fail fast with guard clauses.
- Catch expected exceptions only (I/O, JSON, cancellation, HTTP, etc.).
- Avoid broad catch-all unless rethrowing with context.
- Do not silently swallow exceptions without a clear benign reason.
- Use `StringComparison.Ordinal`/`OrdinalIgnoreCase` for string and path comparisons.

## Avalonia / MVVM Boundaries
- Keep code-behind focused on UI concerns (windowing, focus, keyboard routing, clipboard).
- Keep app/state logic in view models and services.
- Follow CommunityToolkit attributes (`[ObservableProperty]`, `[RelayCommand]`) and partial hooks.
- Reuse existing event/focus patterns before introducing new channels.

## Filesystem and Data Safety
- Preserve note frontmatter compatibility when editing parser/serializer behavior.
- Keep file naming and sanitization deterministic.
- Avoid destructive behavior changes in rename/delete flows without explicit requirement.
- Never log or commit secrets (API keys, org/project IDs, credentials).

## Testing Expectations
- Update or add tests whenever behavior changes in repository/services/view model logic.
- Prefer focused unit tests for parser/filter/ranking behavior.
- Use temp directories in filesystem tests and clean up in `Dispose()`.
- Relevant suites include repository, theme loader, font catalog, settings, AI prompt catalog, and AI text action tests.

## Agent Working Rules
- Read nearby code before editing.
- Keep changes scoped to the user request.
- Avoid unrelated refactors or broad formatting churn.
- Validate touched behavior with targeted tests first, then broader suite as needed.
- If a command cannot run (environment lock/UI requirement), report it clearly.

## Validation Baseline
- `dotnet build QuickNotesTxt.sln` should succeed.
- `dotnet test QuickNotesTxt.sln --no-build` should pass after successful build.
- Single-test execution via `--filter` should work reliably.
- Treat `artifacts/verify/` as generated output, not hand-edited source.
