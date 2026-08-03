# Agent Guide: src/GroundNotes

## Scope
This guide applies to `src/GroundNotes/` and descendants unless a deeper `AGENTS.md` overrides it.

## Mental Model
- `App.axaml.cs` is the manual composition root. Add dependencies to its factories/constructors explicitly; there is no DI container, and each workspace window receives its own `MainViewModel` and watcher while repository, mutation, layout, AI, and appearance services may be shared.
- Keep domain and persistence logic in `Services/`, state and commands in `ViewModels/`, Avalonia/editor hosting in `Views/`, markdown/editor algorithms in `Editors/`, and persisted contracts in `Models/`.
- Read the scoped guides in `Services/`, `ViewModels/`, `Editors/`, and `Views/` before changing those subsystems.

## Architecture Boundaries
- Follow MVVM with CommunityToolkit.Mvvm and existing `[ObservableProperty]` / `[RelayCommand]` patterns.
- Preserve the `MainViewModel` partial-file organization instead of folding behavior into one large file.
- Keep code-behind focused on Avalonia UI concerns: editor hosting, popups, chrome, gestures, and controller wiring.
- Keep prompt-style AI actions separate from conversational chat services.
- `GroundNotes.csproj` copies `Assets/Fonts/**` and `Assets/AiPrompts/**` to build/publish output. Built-in prompts are seed inputs; note-folder custom prompts and slash commands live under `.groundnotes/` and may perform legacy migration.

## Sensitive Areas
- Be conservative around note parsing, filename sanitization, search/list ordering, save timing, watcher suppression, and persisted settings/layout data.
- A save can rename a file. Preserve source hashes/conflict detection, unknown frontmatter lines, `.md` preference over same-stem `.txt`, top-level-only enumeration, and timestamp-preserving bulk operations.
- Treat markdown image previews as render-only behavior over persisted text. Do not add placeholder lines or fake document content.
- For editor/image-preview/multi-pane work, inspect the relevant `Views/*Controller.cs`, `Views/MainWindow.axaml*`, and `Editors/Markdown*` files before changing behavior.
- Fork-level caret, layout, wrapped-line, and hit-testing fixes belong in `extern/AvaloniaEdit/src/AvaloniaEdit/` rather than app-side workarounds.
- Long-lived services, view models, editor hosts, timers, and event subscriptions have explicit disposal paths; preserve cleanup when adding listeners or per-window state.

## Validation
- Build app changes with `dotnet build src/GroundNotes/GroundNotes.csproj`.
- Add or update focused tests in `tests/GroundNotes.Tests` for behavior changes, especially repository, settings, watcher/save, editor, and AI flows.
