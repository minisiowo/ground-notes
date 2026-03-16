# AGENTS.md
This file gives coding agents the repository-specific context needed to work safely and quickly in `quick-notes-txt`.

## Project Summary
- Stack: .NET 10, Avalonia UI 11, xUnit, CommunityToolkit.Mvvm.
- App type: desktop notes app for plain-text notes with frontmatter metadata.
- Notes live in a user-selected folder and support both `.md` and `.txt` files.
- Current features include drafts, debounced auto-save, tag filtering, inline rename/delete, keyboard note picker, themes, bundled fonts, AI text actions, and persisted window layout.
- Solution file: `QuickNotesTxt.sln`.
- Main app project: `src/QuickNotesTxt/QuickNotesTxt.csproj`.
- Test project: `tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj`.

## Repository Layout
- `src/QuickNotesTxt/` - Avalonia application code.
- `src/QuickNotesTxt/Models/` - note, theme, font, and AI models.
- `src/QuickNotesTxt/Services/` - repository, watcher, settings, theme, font, prompt, and OpenAI services.
- `src/QuickNotesTxt/ViewModels/` - MVVM state and commands, mainly `MainViewModel`.
- `src/QuickNotesTxt/Views/` - XAML views and code-behind.
- `src/QuickNotesTxt/Styles/` - shared Avalonia styles and theme resources.
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
- Prefer `dotnet restore QuickNotesTxt.sln` as the standard restore entry point.

## Build Commands
```bash
dotnet build QuickNotesTxt.sln
dotnet build src/QuickNotesTxt/QuickNotesTxt.csproj
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
```

- Use the solution build for broad validation.
- Avalonia may fail when an `obj/Debug/...` artifact is locked by a running process; rerun after the conflicting process exits.

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
- If Avalonia test/build artifacts are locked, run `dotnet build` first, then `dotnet test --no-build`.

## Run A Single Test
Use `--filter` with a fully qualified name when possible:

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
- `NotesRepository` owns frontmatter parsing, serialization, unique file naming, filtering, and picker scoring.
- `FileWatcherService` monitors the notes folder for external `.txt` and `.md` changes.
- `FolderSettingsService` persists folder, theme, font, AI settings, and window layout.
- `OpenAiTextActionService` and `AiPromptCatalogService` support prompt-driven AI text actions.
- Keyboard shortcuts and focus choreography live primarily in `MainWindow.axaml.cs`.

## C# Style
- Use file-scoped namespaces.
- Use 4-space indentation.
- Keep one top-level type per file.
- Prefer concise members and guard clauses over deeply nested control flow.
- Use expression-bodied members only when they remain easy to scan.
- Prefer small private helpers when logic would otherwise repeat in commands or event handlers.

## Imports And Dependencies
- Order `using` directives as `System.*`, then third-party namespaces, then `QuickNotesTxt.*`.
- Do not add redundant `using` directives.
- Add new packages only when the current lightweight stack cannot reasonably handle the requirement.
- Prefer built-in .NET and Avalonia APIs over new abstractions when the problem is local and UI-specific.

## Types And Nullability
- Treat nullable annotations as part of the API contract.
- Prefer explicit null handling over the null-forgiving operator.
- Use `var` when the right-hand side makes the type obvious; otherwise spell the type out.
- Use collection expressions and target-typed `new` when they improve readability and match local style.
- Keep observable properties simple and derive display-only properties when that reduces view logic.

## Naming Conventions
- Use `PascalCase` for types, methods, properties, enums, and enum members.
- Use `_camelCase` for private fields.
- Prefix interfaces with `I`.
- Name methods by behavior, not implementation detail.
- Keep boolean members positive and readable, such as `HasSelectedFolder`, `IsNotePickerOpen`, or `HasPickerPreview`.

## Error Handling And Control Flow
- Prefer guard clauses and early returns.
- Catch only expected exceptions such as JSON, I/O, cancellation, HTTP, or modeled UI-state exceptions.
- Swallow exceptions only for known benign cases with a clear cancellation or UI reason.
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string and path comparisons.
- Keep filesystem behavior deterministic and explicit.
- For destructive note actions, preserve the current confirmation flow unless the user explicitly asks to change it.

## Avalonia / MVVM Guidance
- Keep business logic in services and view models, not in code-behind.
- Keep code-behind focused on windowing, resize/chrome behavior, focus, clipboard, keyboard routing, and editor-specific input wiring.
- Follow the existing CommunityToolkit.Mvvm pattern: `[ObservableProperty]`, `[RelayCommand]`, partial hooks, and `ObservableObject` base types.
- `MainViewModel` should remain the owner of save scheduling, watcher suppression, filtering, picker state, rename/delete flows, theme state, font state, and AI command state.
- Reuse existing focus request patterns before introducing new view-to-viewmodel event channels.

## Filesystem, Themes, Fonts, And AI
- Keep note parse and serialize behavior aligned; notes use frontmatter plus body content.
- `NotesRepository` supports both `.md` and `.txt` inputs and uses deterministic file naming.
- Keep title sanitization and unique file path behavior deterministic.
- Custom themes are loaded by `ThemeLoaderService` from the platform local app data themes directory.
- Bundled fonts are discovered by `FontCatalogService`; keep asset paths and resource URIs aligned.
- Never log, commit, or hardcode user API keys, project IDs, or organization IDs.

## Testing Guidance
- Add or update tests when changing note parsing, serialization, search, filtering, picker ranking, rename/delete behavior, theme loading/export, font discovery, AI prompt loading, AI request/response handling, or settings persistence.
- Relevant test files include `tests/QuickNotesTxt.Tests/NotesRepositoryTests.cs`, `tests/QuickNotesTxt.Tests/ThemeLoaderServiceTests.cs`, `tests/QuickNotesTxt.Tests/FontCatalogServiceTests.cs`, `tests/QuickNotesTxt.Tests/FolderSettingsServiceTests.cs`, `tests/QuickNotesTxt.Tests/AiPromptCatalogServiceTests.cs`, `tests/QuickNotesTxt.Tests/OpenAiTextActionServiceTests.cs`, and `tests/QuickNotesTxt.Tests/NoteSummaryTests.cs`.
- Use temp directories in filesystem tests and clean them up in `Dispose()`.
- Prefer focused unit tests for ranking and formatting helpers before adding broader UI-driven tests.

## Change Checklist For Agents
- Read the surrounding file before editing.
- Keep changes scoped to the user request.
- Avoid unrelated renames or formatting churn.
- Update tests when changing covered behavior.
- Run targeted validation for the area you touched.
- Mention any commands you could not run or environment-specific failures.

## Validation Baseline
- `dotnet build QuickNotesTxt.sln` should succeed.
- `dotnet test QuickNotesTxt.sln --no-build` should pass after a successful build.
- `dotnet test ... --filter` should work for single-test execution.
- Treat `artifacts/verify/` output as generated verification data, not editable source.
