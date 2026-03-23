# AGENTS.md

Repository guide for agentic coding assistants working in `ground-notes`.

## Purpose
- Provide the command set and coding conventions for this repo.
- Keep changes minimal, focused, and architecture-aligned.
- Preserve deterministic filesystem and parsing behavior.

## Project Snapshot
- Product: desktop plain-text notes app.
- Stack: .NET 10, Avalonia UI 11, AvaloniaEdit, CommunityToolkit.Mvvm, xUnit.
- Note format: `.txt` files with YAML-like frontmatter and body text.
- AI features: OpenAI-backed text actions plus a dedicated chat window that can attach note context and persist conversations as notes.
- Solution: `GroundNotes.sln`.
- App: `src/GroundNotes/GroundNotes.csproj`.
- Tests: `tests/GroundNotes.Tests/GroundNotes.Tests.csproj`.

## Layout
- `src/GroundNotes/Models/`: note/theme/font plus AI settings, prompts, and chat message models.
- `src/GroundNotes/Services/`: repository, watcher, settings, theme/font, AI prompt catalog, OpenAI text action, and OpenAI chat services.
- `src/GroundNotes/ViewModels/`: primary MVVM state and commands, including `MainViewModel` and `ChatViewModel`.
- `src/GroundNotes/Views/`: Avalonia XAML plus minimal code-behind, including `ChatWindow` and `SettingsWindow`.
- `src/GroundNotes/Styles/`: theme/style resources.
- `src/GroundNotes/Assets/`: bundled assets, including built-in AI prompt definitions in `Assets/AiPrompts/`.
- `tests/GroundNotes.Tests/`: xUnit tests.

## Cursor and Copilot Rules
- `.cursorrules`: not present.
- `.cursor/rules/`: not present.
- `.github/copilot-instructions.md`: not present.
- No editor-specific rule files currently apply.

## Toolchain
- SDK pinned in `global.json`: `10.0.103`.
- `rollForward`: `latestFeature`.
- `Directory.Build.props` enables nullable, implicit usings, latest language version.
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
- Use project-level build for tighter local iteration.

## Run Command
```bash
dotnet run --project src/GroundNotes
```

Run notes:
- Requires graphical desktop session.
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
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ThemeLoaderServiceTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ChatViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~OpenAiTextActionServiceTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~AiPromptCatalogServiceTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "Name~SerializeAndParse"
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
- `MainViewModel` coordinates editor state, selection/filter/sort, save orchestration, AI prompt execution, settings persistence, and opening the chat window.
- `NotesRepository` owns frontmatter parse/serialize and filesystem note operations.
- `FileWatcherService` handles external change monitoring.
- `FolderSettingsService` handles persisted folder/theme/font/window preferences plus AI settings (enable flag, API key, default model, project ID, organization ID).
- `ChatViewModel` owns AI chat transcript state, attached note context, model selection, and saving conversations back through `NotesRepository`.
- Keep prompt-style AI actions (`IAiTextActionService` / `OpenAiTextActionService`) separate from conversational chat (`IAiChatService` / `OpenAiChatService`).
- `AiPromptCatalogService` loads built-in prompts from app assets and folder-specific prompts from `.quicknotestxt/ai-prompts` inside the current notes directory.

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
- Keep DTO/model contracts stable.

## Naming
- `PascalCase`: types, methods, properties, enums, enum values.
- `_camelCase`: private fields.
- Interface names start with `I`.
- Async methods end with `Async`.
- Boolean members use readable positive names (`Is...`, `Has...`, `Can...`).

## Error Handling
- Validate inputs early and fail fast.
- Catch expected specific exceptions (I/O, JSON, cancellation, HTTP).
- Avoid broad catch-all unless rethrowing with context.
- Do not silently swallow exceptions.
- Use `StringComparison.Ordinal`/`OrdinalIgnoreCase` for string and path comparisons.

## Avalonia Boundaries
- Keep code-behind focused on UI-only concerns.
- Keep state transitions and logic in VM/services.
- Reuse established event and focus patterns.
- `ChatWindow` code-behind is responsible for editor synchronization, auto-scroll behavior, mention popup interactions, and window chrome only; chat/history logic belongs in `ChatViewModel`.

## Filesystem and Data Safety
- Preserve frontmatter compatibility.
- Keep filename sanitization deterministic.
- Be conservative around rename/delete behavior changes.
- Preserve AI chat note persistence behavior: saved conversations are regular notes tagged with `AI`, and linked-note reference blocks should remain deterministic.
- Keep custom AI prompts folder behavior stable at `.quicknotestxt/ai-prompts` under the selected notes folder.
- Never commit secrets or local credentials.

## Testing Expectations
- Add or update tests for behavior changes.
- Prefer focused unit tests near changed logic.
- Use temporary directories in filesystem tests and clean up reliably.
- For AI changes, prefer targeted coverage in `ChatViewModelTests`, `OpenAiTextActionServiceTests`, and `AiPromptCatalogServiceTests`.
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
```
