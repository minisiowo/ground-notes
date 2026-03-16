# AGENTS.md
This file gives coding agents the repository-specific context needed to work safely and quickly in `quick-notes-txt`.

## Project Summary
- Stack: .NET 10, Avalonia UI 11, xUnit, CommunityToolkit.Mvvm.
- App type: desktop notes app for plain-text notes with frontmatter metadata.
- Notes live in a user-selected folder and support `.md` and `.txt` inputs.
- Current features include auto-created drafts, debounced auto-save, tag filtering, inline rename/delete, note picker, runtime themes, bundled fonts, AI text actions, and persisted window layout.
- Solution: `QuickNotesTxt.sln`.
- Test project: `tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj`.

## Repository Layout
- `src/QuickNotesTxt/` - Avalonia application code.
- `src/QuickNotesTxt/Models/` - note, theme, font, and AI models.
- `src/QuickNotesTxt/Services/` - repository, watcher, settings, theme, font, prompt, and OpenAI services.
- `src/QuickNotesTxt/ViewModels/` - MVVM state and commands, mainly `MainViewModel`.
- `src/QuickNotesTxt/Views/` - XAML views and code-behind.
- `src/QuickNotesTxt/Assets/` - bundled fonts and AI prompt assets.
- `tests/QuickNotesTxt.Tests/` - xUnit tests.
- `artifacts/verify/` - generated verification output, not source.

## Cursor / Copilot Rules
- No `.cursorrules` file was found.
- No files were found under `.cursor/rules/`.
- No `.github/copilot-instructions.md` file was found.
- Do not assume hidden editor-specific rules exist elsewhere.

## Tooling And Setup
- Required SDK: .NET `10.0.103` or newer in the same feature band.
- `global.json` pins the SDK and uses `rollForward: latestFeature`.
- `Directory.Build.props` enables nullable reference types, implicit usings, and `LangVersion=latest`.

```bash
mise install
dotnet restore QuickNotesTxt.sln
```

- `mise install` is optional.
- Prefer `dotnet restore QuickNotesTxt.sln` as the normal restore entry point.

## Build Commands
```bash
dotnet build QuickNotesTxt.sln
dotnet build src/QuickNotesTxt/QuickNotesTxt.csproj
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
```

- Use the solution build for broad validation.
- If Avalonia XAML compilation fails because `original.dll` is locked, rerun after the conflicting process exits.

## Run Commands
```bash
dotnet run --project src/QuickNotesTxt
```

- Running the app requires a graphical desktop session.

## Test Commands
```bash
dotnet test QuickNotesTxt.sln
dotnet test QuickNotesTxt.sln --no-build
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build
dotnet test QuickNotesTxt.sln --no-build --list-tests
```

- Prefer `--no-build` after a successful build.

## Run A Single Test
Use `--filter` with the fully qualified test name:

```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~QuickNotesTxt.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```

Useful variants:

```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "FullyQualifiedName~ThemeLoaderServiceTests"
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --no-build --filter "Name~SerializeAndParse"
```

- Discover exact names with `dotnet test QuickNotesTxt.sln --no-build --list-tests`.
- Prefer project-scoped single-test runs unless solution-wide behavior matters.

## Lint / Formatting Expectations
- No dedicated lint command is configured.
- No repo `.editorconfig` file is present.
- Do not invent a mandatory formatter or lint step in docs, CI notes, or commits.
- Match existing formatting and keep diffs narrow.

## Architecture Notes
- This is a desktop MVVM app built around `MainViewModel`.
- `NotesRepository` owns frontmatter parsing, serialization, file naming, searching, filtering, and picker scoring.
- `FileWatcherService` monitors the notes folder for external changes.
- `FolderSettingsService` persists folder, theme, font, AI settings, and window layout.
- `OpenAiTextActionService` and `AiPromptCatalogService` support prompt-driven AI text actions.

## C# Style
- Use file-scoped namespaces.
- Use 4-space indentation.
- Keep one top-level type per file.
- Prefer concise members and guard clauses over deeply nested control flow.
- Use expression-bodied members only when they stay easy to scan.

## Imports And Dependencies
- Order `using` directives as `System.*`, then third-party namespaces, then `QuickNotesTxt.*`.
- Do not add redundant `using` directives.
- Add new packages only when the current lightweight stack cannot reasonably handle the requirement.

## Types And Nullability
- Treat nullable annotations as part of the API contract.
- Prefer explicit null handling over the null-forgiving operator.
- Use `var` when the right-hand side makes the type obvious; otherwise spell the type out.
- Use collection expressions and target-typed `new` when they improve readability and match local style.

## Naming Conventions
- Use `PascalCase` for types, methods, properties, enums, and enum members.
- Use `_camelCase` for private fields.
- Prefix interfaces with `I`.
- Name methods by behavior, not implementation detail.
- Keep boolean members positive and readable, such as `HasSelectedFolder` or `IsAiBusy`.

## Error Handling And Control Flow
- Prefer guard clauses and early returns.
- Catch only expected exceptions such as JSON, I/O, cancellation, HTTP, or modeled UI-state exceptions.
- Swallow exceptions only for known benign cases with a clear cancellation or UI reason.
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string and path comparisons.
- Keep filesystem behavior deterministic and explicit.

## Avalonia / MVVM Guidance
- Keep business logic in services and view models, not in code-behind.
- Keep code-behind focused on windowing, resize/chrome behavior, focus, clipboard, and editor-specific input wiring.
- Follow the existing CommunityToolkit.Mvvm pattern: `[ObservableProperty]`, `[RelayCommand]`, partial hooks, and `ObservableObject` base types.
- `MainViewModel` should remain the owner of save scheduling, watcher suppression, filtering, picker state, rename/delete flows, theme state, font state, and AI command state.

## Filesystem, Themes, Fonts, And AI
- Keep note parse and serialize behavior aligned; notes use frontmatter plus body content.
- `NotesRepository` prefers `.md` output and supports both `.md` and `.txt` inputs.
- Keep title sanitization and unique file path behavior deterministic.
- Custom themes are loaded by `ThemeLoaderService` from the platform local app data themes directory.
- Bundled fonts are discovered by `FontCatalogService`; keep asset paths and resource URIs aligned.
- Never log, commit, or hardcode user API keys, project IDs, or organization IDs.

## Testing Guidance
- Add or update tests when changing note parsing, serialization, search, filtering, picker ranking, rename/delete behavior, theme loading/export, font discovery, AI prompt loading, AI request/response handling, or settings persistence.
- Relevant test files include `tests/QuickNotesTxt.Tests/NotesRepositoryTests.cs`, `tests/QuickNotesTxt.Tests/ThemeLoaderServiceTests.cs`, `tests/QuickNotesTxt.Tests/FontCatalogServiceTests.cs`, `tests/QuickNotesTxt.Tests/FolderSettingsServiceTests.cs`, `tests/QuickNotesTxt.Tests/AiPromptCatalogServiceTests.cs`, `tests/QuickNotesTxt.Tests/OpenAiTextActionServiceTests.cs`, and `tests/QuickNotesTxt.Tests/NoteSummaryTests.cs`.
- Use temp directories in filesystem tests and clean them up in `Dispose()`.

## Change Checklist For Agents
- Read the surrounding file before editing.
- Keep changes scoped to the user request.
- Avoid unrelated renames or formatting churn.
- Run targeted tests when behavior changes in a covered area.
- Mention any commands you could not run or environment-specific failures.

## Validation Baseline
- `dotnet build QuickNotesTxt.sln` should succeed.
- `dotnet test QuickNotesTxt.sln --no-build` should pass after a successful build.
- `dotnet test ... --filter` should work for single-test execution.
- Treat `artifacts/verify/` output as generated verification data, not editable source.
