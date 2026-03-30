# AGENTS.md

Guide for agentic coding assistants working in `ground-notes`.
## Purpose
- Keep changes small, architecture-aligned, and easy to review.
- Preserve deterministic note parsing, filesystem behavior, and persisted settings/layout behavior.
- Prefer focused edits and targeted validation over broad refactors.
## Repository Snapshot
- Product: local-first desktop notes app for plain-text folders the user owns.
- Stack: .NET 10, Avalonia UI 11, forked `AvaloniaEdit`, CommunityToolkit.Mvvm, xUnit.
- App project: `src/GroundNotes/GroundNotes.csproj`.
- Test project: `tests/GroundNotes.Tests/GroundNotes.Tests.csproj`.
- Main solution: `GroundNotes.sln`.
- SDK pin: `global.json` -> `10.0.103` with `rollForward: latestFeature`.
- Shared build settings: `Directory.Build.props` enables nullable, implicit usings, and `LangVersion=latest`.
## Important Paths
- `src/GroundNotes/Models/`: persisted/domain data contracts.
- `src/GroundNotes/Services/`: repository, settings, AI, file watching, themes, fonts, layout persistence.
- `src/GroundNotes/ViewModels/`: MVVM state and commands.
- `src/GroundNotes/Views/`: Avalonia XAML and UI-only code-behind/controllers.
- `src/GroundNotes/Editors/`: markdown parsing, styling, slash commands, editor behavior.
- `src/GroundNotes/Styles/`: theme/resource definitions.
- `tests/GroundNotes.Tests/`: xUnit coverage.
- `extern/AvaloniaEdit/`: forked upstream editor source; treat as high-risk third-party code.
- `scripts/`: local helper scripts for publishing/install flows.
## Cursor / Copilot Rules
- `.cursorrules`: not present.
- `.cursor/rules/`: not present.
- `.github/copilot-instructions.md`: not present.
- No editor-specific instruction files currently apply beyond this document.
## Build Commands
```bash
dotnet restore GroundNotes.sln
dotnet build GroundNotes.sln
dotnet build src/GroundNotes/GroundNotes.csproj
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release
```
## Run Command
```bash
dotnet run --project src/GroundNotes
```
Notes:
- GUI is required to run the desktop app; in headless environments, stick to build/test validation.
- Prefer solution-level builds for broad validation, project-level builds for quick iteration.
- If tests or `extern/AvaloniaEdit` change, rebuild before relying on `--no-build`.
## Lint / Format
- No dedicated lint command is configured in this repo.
- No mandatory formatting command is configured.
- Match the surrounding style in touched files.
- Avoid large style-only diffs.
## Test Commands
Run everything:
```bash
dotnet test GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```
Run just the test project:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build
```
List tests:
```bash
dotnet test GroundNotes.sln --no-build --list-tests
```
Single-test pattern:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~GroundNotes.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```
Useful class-level filters:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MainViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ChatViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~SettingsViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownColorizingTransformerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
```

Fast test loop:
- Build once.
- Run focused tests with `--no-build --filter`.
- Use `--list-tests` if the exact fully qualified name is unknown.
## Architecture Expectations
- Follow MVVM with CommunityToolkit.Mvvm; use `[ObservableProperty]` and `[RelayCommand]` in the established style.
- Keep business logic in services/view models, not window code-behind.
- Keep code-behind focused on UI wiring, editor hosting, popup behavior, chrome, and input handling.
- Preserve `MainViewModel` partial-file organization instead of merging it into one large file.
- Keep prompt-style AI actions separate from conversational chat services.
- Preserve deterministic note parsing, filename sanitization, search ordering, and note-save behavior.
- Preserve settings persistence under local app data and the `.quicknotestxt/ai-prompts` custom prompt folder behavior.
## Avalonia / Editor Boundaries
- Put markdown parsing and editor behavior in `src/GroundNotes/Editors/`.
- Keep note editor and chat editor behavior aligned through shared editor infrastructure.
- Treat `MarkdownColorizingTransformer.QueryIsFencedCodeLine(...)` as the source of truth for fenced-code state.
- Fork-level editor fixes belong in `extern/AvaloniaEdit/src/AvaloniaEdit/`, not scattered app-side workarounds.
- Changes in `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` or `TextView.cs` are high risk; validate carefully.
## Code Style
- Use file-scoped namespaces.
- Use 4-space indentation.
- Prefer one top-level type per file.
- Prefer `sealed` concrete classes unless inheritance is required.
- Prefer guard clauses and early returns.
- Keep methods small and cohesive.
- Use expression-bodied members only when they improve readability.
## Imports
- Order `using` directives as:
  1. `System.*`
  2. third-party namespaces
  3. `GroundNotes.*`
- Remove unused imports.
- Rely on built-in/platform APIs before adding new dependencies.
## Types and Nullability
- Nullable annotations are part of the contract; respect them.
- Avoid the null-forgiving operator unless there is no practical alternative.
- Prefer explicit null checks at boundaries.
- Use `var` when the RHS type is obvious; otherwise use an explicit type.
- Keep persisted model shapes stable unless the task explicitly changes data contracts.
## Naming
- `PascalCase`: types, methods, properties, enums, enum values.
- `_camelCase`: private fields.
- Interfaces start with `I`.
- Async methods end with `Async`.
- Boolean members should read positively: `Is...`, `Has...`, `Can...`.
## Error Handling
- Validate inputs early and fail fast.
- Catch expected specific exceptions where practical.
- Avoid broad catch-all handlers unless rethrowing with context or surfacing a deliberate user-facing status.
- Do not silently swallow exceptions unless the UX already depends on that behavior.
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string/path comparisons.
## Filesystem and Data Safety
- Preserve support for `.md` and `.txt` notes.
- Preserve frontmatter compatibility.
- Be conservative with rename/delete behavior changes.
- Preserve deterministic AI chat note persistence and linked-note reference formatting.
- Never commit secrets, tokens, or machine-local credentials.
## Testing Expectations
- Add or update tests for behavior changes.
- Prefer focused unit tests near the changed logic.
- Use temp directories for filesystem tests and clean them up reliably.
- For note parsing, save, rename, and ranking changes, update `NotesRepositoryTests`.
- For search/list behavior, prefer `MainViewModelTests` plus repository/search coverage.
- For AI behavior, prefer `ChatViewModelTests`, `OpenAiChatServiceTests`, `OpenAiTextActionServiceTests`, and `AiPromptCatalogServiceTests`.
- For settings/theme/font/layout behavior, prefer the related service/view-model test files already in `tests/GroundNotes.Tests/`.
- For markdown/editor behavior, prefer `Markdown*Tests`, `EditorThemeControllerTests`, and `MainWindowShortcutTests`.
- When changing the AvaloniaEdit fork, build the fork and app before running tests.
## Agent Workflow
- Read nearby code before editing.
- Follow existing patterns in the touched area before introducing new ones.
- Keep edits scoped to the request.
- Avoid unrelated refactors.
- Validate with the narrowest useful command first, then broaden if needed.
- If environment limits block validation, state that clearly.
## Validation Baseline
- `dotnet build GroundNotes.sln`
- `dotnet test GroundNotes.sln --no-build`
- Targeted single-test execution with `--filter`
- If editor fork changes: `dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`
