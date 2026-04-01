# AGENTS.md

Guide for agentic coding assistants working in `ground-notes`.
## Purpose
- Keep changes small, architecture-aligned, and easy to review.
- Preserve deterministic note parsing, filesystem behavior, and persisted settings/layout behavior.
- Prefer focused edits and targeted validation over broad refactors.
## Repository Snapshot
- Product: local-first desktop notes app for plain-text folders the user owns.
- Stack: .NET 10, Avalonia UI 11, forked `AvaloniaEdit`, CommunityToolkit.Mvvm, xUnit.
- Main solution: `GroundNotes.sln`.
- App project: `src/GroundNotes/GroundNotes.csproj`.
- Test project: `tests/GroundNotes.Tests/GroundNotes.Tests.csproj`.
- AvaloniaEdit fork: `extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`.
- SDK pin: `global.json` -> `10.0.103` with `rollForward: latestFeature`.
- Shared build settings: `Directory.Build.props` enables nullable, implicit usings, and `LangVersion=latest`.
## Important Paths
- `src/GroundNotes/Models/`: persisted/domain data contracts.
- `src/GroundNotes/Services/`: repository, settings, AI, file watching, themes, fonts, layout persistence.
- `src/GroundNotes/ViewModels/`: MVVM state and commands.
- `src/GroundNotes/Views/`: Avalonia XAML and UI-only code-behind/controllers.
- `src/GroundNotes/Editors/`: markdown parsing, styling, slash commands, editor behavior.
- `tests/GroundNotes.Tests/`: xUnit coverage.
- `extern/AvaloniaEdit/`: forked upstream editor source; treat as high-risk third-party code.
## Cursor / Copilot Rules
- `.cursorrules`: not present.
- `.cursor/rules/`: not present.
- `.github/copilot-instructions.md`: not present.
- No editor-specific instruction files currently apply beyond this document.
## Build, Run, Lint, and Format
Restore dependencies:
```bash
dotnet restore GroundNotes.sln
```
Build everything:
```bash
dotnet build GroundNotes.sln
```
Build the app only:
```bash
dotnet build src/GroundNotes/GroundNotes.csproj
```
Build the AvaloniaEdit fork when editor internals change:
```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
```
Run the desktop app:
```bash
dotnet run --project src/GroundNotes
```
Publish a release build:
```bash
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release
```
Lint/format notes:
- No dedicated lint command is configured.
- No mandatory format command is configured.
- Match surrounding style and keep formatting changes targeted.
- In headless environments prefer build/test validation over launching the GUI.
## Test Commands
Run the full suite:
```bash
dotnet test GroundNotes.sln
dotnet test GroundNotes.sln --no-build
```
Run just the test project:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build
```
List tests when you need an exact name:
```bash
dotnet test GroundNotes.sln --no-build --list-tests
```
Run one test method:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~GroundNotes.Tests.NotesRepositoryTests.CreateDraftNote_UsesTimestampTitle"
```
Run one test class:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~NotesRepositoryTests"
```
Useful class filters:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~MainViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~ChatViewModelTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~EditorThemeControllerTests"
```
Fast loop guidance:
- Build once, then use `--no-build --filter` for iteration.
- Rebuild before `--no-build` if project files, tests, or `extern/AvaloniaEdit` changed.
## Architecture Expectations
- Follow MVVM with CommunityToolkit.Mvvm and the existing `[ObservableProperty]` / `[RelayCommand]` patterns.
- Keep business logic in services and view models, not in window code-behind.
- Keep code-behind focused on UI wiring, editor hosting, popup behavior, chrome, and input handling.
- Preserve `MainViewModel` partial-file organization instead of merging it into one large file.
- Keep prompt-style AI actions separate from conversational chat services.
- Preserve deterministic note parsing, filename sanitization, search ordering, save behavior, and persisted local settings.
## Avalonia / Editor Boundaries
- Put markdown parsing and editor behavior in `src/GroundNotes/Editors/`.
- Keep note editor and chat editor behavior aligned through shared editor infrastructure.
- Treat `MarkdownColorizingTransformer.QueryIsFencedCodeLine(...)` as the source of truth for fenced-code state.
- Fork-level editor fixes belong in `extern/AvaloniaEdit/src/AvaloniaEdit/`, not in app-side workarounds.
- Changes in `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/VisualLine.cs` or `TextView.cs` are high risk and need extra validation.
- For multi-pane workspace focus/scroll bugs, remember that Avalonia `ScrollContentPresenter` may consume `RequestBringIntoView` before ancestor handlers see it; source-side suppression on focused descendants can be more effective than only handling the event on the outer `ScrollViewer`.
- Preserve the distinction between inner editor caret visibility and outer workspace scrolling; fixes should avoid breaking caret visibility inside `AvaloniaEdit` while preventing horizontal workspace jumps.
- For workspace spacing polish, remember the left editor gutter is affected by both `WorkspaceHost.Margin` and the sidebar splitter column; equal visual gutters may require dynamic compensation when the splitter is shown or hidden.
- Sidebar collapse/expand can leave editor widths in an intermediate layout state; after animated sidebar width changes, schedule a deferred workspace/editor relayout once layout has settled.
- For multi-pane sizing, preserve the current three-mode model unless the task explicitly changes it: single-pane uses `EditorCanvasWidth`, two-pane uses its own split/overflow logic, and `3+` panes share one common width.
- When moving from `2` panes to `3+`, preserve the current visible pane width as the shared width instead of resetting all panes to a fresh equal split.
- Equal-fit pane layouts can clip the last border by a pixel; prefer flooring computed shared widths and keeping a tiny safety gap instead of fitting exactly to the viewport.
- In single-pane mode, keep resize available from both left and right edges, and treat `Ctrl+0` as a reset back to full-width editor layout.
- When tuning multi-pane spacing, remember there are two horizontal spacing sources in `MainWindow.axaml`: the outer `PaneWorkspaceContent` stack and the `ItemsControl` panel for secondary panes; keep them in sync or the first gap will differ from later ones.
## Code Style
- Use file-scoped namespaces and 4-space indentation.
- Prefer one top-level type per file.
- Prefer `sealed` concrete classes unless inheritance is required.
- Prefer guard clauses, early returns, and small cohesive methods.
- Use expression-bodied members only when they improve readability.
- Avoid broad refactors unless the task explicitly requires them.
## Imports and Formatting
- Order `using` directives as: `System.*`, third-party namespaces, then `GroundNotes.*`.
- Remove unused imports.
- Keep import order and spacing consistent with nearby files.
- Rely on built-in or platform APIs before adding new dependencies.
- There is no repo `.editorconfig` or StyleCop config; follow the surrounding code.
## Types and Nullability
- Nullable annotations are part of the contract; respect them.
- Avoid the null-forgiving operator unless there is no practical alternative.
- Prefer explicit null checks at boundaries.
- Use `var` when the RHS type is obvious; otherwise use an explicit type.
- Keep persisted model shapes stable unless the task explicitly changes data contracts.
- Reuse existing domain and service abstractions before introducing new cross-cutting types.
## Naming and Organization
- `PascalCase`: types, methods, properties, enums, and enum values.
- `_camelCase`: private fields.
- Interfaces start with `I`.
- Async methods end with `Async`.
- Boolean members should read positively: `Is...`, `Has...`, `Can...`.
- Keep test names descriptive and behavior-focused.
## Async, Error Handling, and State
- Propagate async all the way when practical; avoid blocking on tasks.
- Pass cancellation tokens through async flows when the surrounding API already supports them.
- Keep UI-thread interactions explicit and narrow.
- Validate inputs early and fail fast.
- Catch expected specific exceptions where practical.
- Avoid broad catch-all handlers unless rethrowing with context or surfacing a deliberate user-facing status.
- Do not silently swallow exceptions unless the UX already depends on that behavior.
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string and path comparisons.
- Be careful with watcher suppression windows, debounce/save timing, and other persisted state interactions.
## Filesystem and Data Safety
- Preserve support for `.md` and `.txt` notes.
- Preserve frontmatter compatibility.
- Be conservative with rename and delete behavior changes.
- Preserve deterministic AI chat note persistence and linked-note reference formatting.
- Never commit secrets, tokens, or machine-local credentials.
## Testing Expectations
- Add or update tests for behavior changes.
- Prefer focused unit tests near the changed logic.
- Use temp directories for filesystem tests and clean them up reliably.
- For note parsing or filesystem behavior, update `NotesRepositoryTests`.
- For search/list behavior, prefer `MainViewModelTests`; for AI behavior, prefer `ChatViewModelTests` and the `OpenAi*` service tests.
- For settings/editor behavior, prefer the related service/view-model tests already in `tests/GroundNotes.Tests/`.
- When changing the AvaloniaEdit fork, build the fork and app before running tests.
- For multi-pane editor interaction fixes, validate manually with 3+ open panes and a partially off-screen target pane; verify focus can change without horizontal workspace auto-scroll.
- For workspace spacing or sidebar layout fixes, validate both sidebar-visible and sidebar-collapsed states, including collapsing and reopening the sidebar to confirm the editor gutter stays balanced and the right edge does not get clipped.
- For multi-pane width changes, validate all three modes (`1`, `2`, `3+` panes), including `Ctrl+0`, transitions between pane counts, horizontal overflow in the `2`-pane case, and preservation of shared width when adding another pane.
- After changes that are ready to try on Windows, run `bash scripts/publish-and-install-wsl.sh` as the final validation/deployment step.
## Agent Workflow
- Read nearby code before editing.
- Follow existing patterns in the touched area before introducing new ones.
- Keep edits scoped to the request.
- Avoid unrelated refactors.
- Validate with the narrowest useful command first, then broaden if needed.
- If environment limits block validation, say so clearly.
## Validation Baseline
- `dotnet build GroundNotes.sln`
- `dotnet test GroundNotes.sln --no-build`
- Targeted single-test execution with `--filter`
- If editor fork changes: `dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`
