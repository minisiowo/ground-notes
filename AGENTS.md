# AGENTS.md

Repository guide for agentic coding assistants working in `ground-notes`.

## Purpose
- Provide the command set, project map, and coding conventions for this repo.
- Keep changes minimal, focused, and architecture-aligned.
- Preserve deterministic note parsing, filesystem behavior, and persisted UI/settings state.

## Project Snapshot
- Product: desktop plain-text notes app for local folders the user controls.
- Stack: .NET 10, Avalonia UI 11, AvaloniaEdit, CommunityToolkit.Mvvm, xUnit.
- Note format: regular `.txt` and `.md` files with YAML-like frontmatter and body text.
- Core features: note search/filter/sort, rename/delete, markdown-aware editor styling, folder watching, theming, bundled fonts, layout persistence, OpenAI-backed text actions, and a dedicated AI chat window.
- Solution: `GroundNotes.sln`.
- App: `src/GroundNotes/GroundNotes.csproj`.
- Tests: `tests/GroundNotes.Tests/GroundNotes.Tests.csproj`.

## Layout
- `src/GroundNotes/Models/`: note, summary, AI, font, and editor/settings data models.
- `src/GroundNotes/Services/`: persistence, search, settings, startup/layout, appearance, fonts/themes, AI, and dialog services.
- `src/GroundNotes/ViewModels/`: MVVM state/commands for main window, settings, confirmations, and AI chat.
- `src/GroundNotes/Views/`: Avalonia XAML plus UI-only controllers/code-behind.
- `src/GroundNotes/Editors/`: markdown parsing, diagnostics, slash commands, styling, and editing helpers for AvaloniaEdit.
- `src/GroundNotes/Styles/`: app theme definitions and shared style resources.
- `src/GroundNotes/Assets/`: bundled AI prompts and bundled fonts.
- `tests/GroundNotes.Tests/`: xUnit coverage for repository, services, view models, markdown editor helpers, shortcuts, and window/layout behavior.
- `scripts/`: local publish/install helpers for Linux, Windows, and WSL workflows.
- `artifacts/verify/`: generated verification output; do not hand-edit unless the task is explicitly about verification artifacts.

## Cursor and Copilot Rules
- `.cursorrules`: not present.
- `.cursor/rules/`: not present.
- `.github/copilot-instructions.md`: not present.
- No editor-specific rule files currently apply.

## Toolchain
- SDK pinned in `global.json`: `10.0.103`.
- `rollForward`: `latestFeature`.
- `Directory.Build.props` enables nullable, implicit usings, and `LangVersion=latest`.
- App target: `net10.0`.
- Avalonia compiled bindings default is disabled in `GroundNotes.csproj`.
- No dedicated lint command is configured.

## Build Commands
```bash
dotnet restore GroundNotes.sln
dotnet build GroundNotes.sln
dotnet build src/GroundNotes/GroundNotes.csproj
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release
```

Build guidance:
- Prefer solution-level build for broad validation.
- Use project-level build for tight iteration.

## Run Command
```bash
dotnet run --project src/GroundNotes
```

Run notes:
- Requires a graphical desktop session.
- In headless shells, run build/tests only.

## Test Commands
Run all tests:
```bash
dotnet test GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```

Run test project only:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build
```

List tests:
```bash
dotnet test GroundNotes.sln --no-build --list-tests
```

## Single-Test Commands (Important)
Preferred single-test pattern:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~GroundNotes.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```

Useful filter variants:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MainViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ChatViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~OpenAiChatServiceTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~OpenAiTextActionServiceTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~AiPromptCatalogServiceTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~SettingsViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownLineParserTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MarkdownEditingCommandsTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MainWindowShortcutTests"
```

Fast iteration loop:
- Build once.
- Run targeted tests with `--no-build --filter`.
- Use `--list-tests` when exact names are unknown.

## Linting and Formatting
- No lint script/tool is defined in this repository.
- No mandatory format command is configured.
- Match local formatting in touched files.
- Avoid large style-only diffs.

## Architecture Rules
- Follow MVVM with CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`).
- Keep business logic in services/view models, not code-behind.
- `MainViewModel` is split across partial files and coordinates note list state, editor state, folder selection, search/tag/sort, save orchestration, note picker, AI prompt execution, chat launching, and appearance/settings persistence.
- `ChatViewModel` owns chat transcript state, note attachments, mention suggestions, model selection, AI requests, and saving/appending chat content through repository/mutation services.
- `SettingsViewModel` owns settings dialog state plus live preview payload generation; keep preview-safe logic there rather than in the window.
- `NotesRepository` owns supported note extensions, frontmatter parse/serialize, deterministic filename sanitization, and note-query scoring/order behavior.
- `NoteSearchService` wraps repository-backed picker search over the current note collection.
- `NoteMutationService` is the write/event boundary for save/delete flows and mutation-origin tracking.
- `FileWatcherService` handles external note change monitoring.
- `FolderSettingsService` persists app settings in LocalApplicationData, including notes folder, fonts, theme, editor layout, window layout, and AI settings.
- `StartupStateService` resolves saved theme/font/layout preferences into startup-ready state before the main view model initializes.
- `SettingsWindowLayoutService` persists window geometry and sidebar layout through `ISettingsService`.
- `AppAppearanceService` and `ThemeService` apply runtime theme/font resources; preserve the current separation between persisted settings and UI resource application.
- `ThemeLoaderService` loads built-in plus user-exported custom themes from the local `themes` directory and validates color payloads.
- `FontCatalogService` enumerates bundled fonts from assets and reads font metadata from font files; avoid regressing deterministic ordering or variant resolution.
- Keep prompt-style AI actions (`IAiTextActionService` / `OpenAiTextActionService`) separate from conversational chat (`IAiChatService` / `OpenAiChatService`).
- `AiPromptCatalogService` loads built-in prompts from app assets and folder-specific prompts from `.quicknotestxt/ai-prompts` under the current notes folder, with custom prompt IDs overriding built-ins.

## Avalonia and Editor Boundaries
- Keep code-behind focused on UI-only concerns.
- Keep state transitions and persistence in view models/services.
- `MainWindow` and `ChatWindow` already use controller/helper classes for window chrome, editor hosting, popup behavior, text sync, and layout; preserve that split instead of pushing logic back into the window class.
- `src/GroundNotes/Editors/` is the home for markdown parsing, styling, diagnostics, and slash-command behavior; markdown rules should stay deterministic and covered by focused tests.
- `ChatWindow` code-behind is responsible for editor synchronization, auto-scroll behavior, mention popup interactions, and window chrome only; chat/history logic belongs in `ChatViewModel`.

## Code Style
- Use file-scoped namespaces.
- 4-space indentation.
- One top-level type per file.
- Prefer `sealed` concrete classes unless inheritance is required.
- Prefer guard clauses and early returns.
- Keep methods small and cohesive.

## Imports
- Order `using` directives as:
  1) `System.*`
  2) third-party namespaces
  3) `GroundNotes.*`
- Remove unused imports.
- Prefer existing platform APIs before introducing dependencies.

## Types and Nullability
- Nullable annotations are part of the type contract.
- Avoid null-forgiving operator (`!`) unless unavoidable.
- Use explicit null checks at boundaries.
- Use `var` when RHS type is obvious, explicit type otherwise.
- Keep record/model contracts stable unless the task explicitly changes persisted data shape.

## Naming
- `PascalCase`: types, methods, properties, enums, enum values.
- `_camelCase`: private fields.
- Interface names start with `I`.
- Async methods end with `Async`.
- Boolean members use readable positive names (`Is...`, `Has...`, `Can...`).

## Error Handling
- Validate inputs early and fail fast.
- Catch expected specific exceptions where practical: I/O, JSON, cancellation, HTTP, and API-specific failures.
- Avoid broad catch-all unless rethrowing with context or intentionally surfacing an end-user status message.
- Do not silently swallow exceptions unless there is an explicit UX reason and the behavior is already established.
- Use `StringComparison.Ordinal`/`OrdinalIgnoreCase` for string and path comparisons.

## Filesystem and Data Safety
- Preserve `.txt` and `.md` support.
- Preserve frontmatter compatibility.
- Keep filename sanitization deterministic.
- Be conservative around rename/delete behavior changes.
- Preserve note ordering/query behavior unless the task explicitly changes ranking/sorting.
- Preserve AI chat note persistence behavior: saved conversations are regular notes tagged with `AI`, and linked-note reference blocks should remain deterministic.
- Keep custom AI prompts folder behavior stable at `.quicknotestxt/ai-prompts` under the selected notes folder.
- Settings are persisted under local app data for `GroundNotes`; avoid incidental breaking changes to serialized settings or theme files.
- Never commit secrets or local credentials.

## Testing Expectations
- Add or update tests for behavior changes.
- Prefer focused unit tests near changed logic.
- Use temporary directories in filesystem tests and clean up reliably.
- For note parsing, rename/save, or query behavior, update `NotesRepositoryTests`.
- For search and list behavior, prefer `MainViewModelTests` and repository/search tests.
- For AI changes, prefer targeted coverage in `ChatViewModelTests`, `OpenAiChatServiceTests`, `OpenAiTextActionServiceTests`, and `AiPromptCatalogServiceTests`.
- For settings/theme/font/layout behavior, prefer `FolderSettingsServiceTests`, `SettingsViewModelTests`, `ThemeLoaderServiceTests`, `FontCatalogServiceTests`, and `SettingsWindowLayoutServiceTests`.
- For editor/markdown behavior, prefer `Markdown*Tests` and `MainWindowShortcutTests`.
- Cover both unsaved chat sessions and persisted chat-note behavior when changing `ChatViewModel`.

## Agent Behavior
- Read nearby code before editing.
- Keep edits scoped to the request.
- Avoid unrelated refactors.
- Validate with targeted tests first, then broader suite if needed.
- If commands cannot run due to environment limits, state that clearly.

## Validation Baseline
- `dotnet build GroundNotes.sln` succeeds.
- `dotnet test GroundNotes.sln --no-build` succeeds after build.
- Single-test execution via `--filter` works reliably.

## Quick Commands
```bash
dotnet restore GroundNotes.sln
dotnet build GroundNotes.sln
dotnet test GroundNotes.sln --no-build
dotnet test GroundNotes.sln --no-build --list-tests
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~GroundNotes.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~GroundNotes.Tests.ChatViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~GroundNotes.Tests.MarkdownLineParserTests"
```
