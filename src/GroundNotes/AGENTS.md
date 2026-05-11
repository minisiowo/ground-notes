# Agent Guide: src/GroundNotes

## Local Scope
- This is the Avalonia desktop app. `App.axaml.cs` is the manual composition root; there is no DI container.
- Keep domain and persistence logic in `Services/`, state and commands in `ViewModels/`, UI wiring/controllers in `Views/`, markdown/editor behavior in `Editors/`, and persisted contracts in `Models/`.

## Architecture Boundaries
- Follow MVVM with CommunityToolkit.Mvvm and existing `[ObservableProperty]` / `[RelayCommand]` patterns.
- Preserve the `MainViewModel` partial-file organization instead of folding behavior into one large file.
- Keep code-behind focused on Avalonia UI concerns: editor hosting, popups, chrome, gestures, and controller wiring.
- Keep prompt-style AI actions separate from conversational chat services.

## Sensitive Areas
- Be conservative around note parsing, filename sanitization, search/list ordering, save timing, watcher suppression, and persisted settings/layout data.
- Treat markdown image previews as render-only behavior over persisted text. Do not add placeholder lines or fake document content.
- For editor/image-preview/multi-pane work, inspect the relevant `Views/*Controller.cs`, `Views/MainWindow.axaml*`, and `Editors/Markdown*` files before changing behavior.
- Fork-level caret, layout, wrapped-line, and hit-testing fixes belong in `extern/AvaloniaEdit/src/AvaloniaEdit/` rather than app-side workarounds.

## Validation
- Build app changes with `dotnet build src/GroundNotes/GroundNotes.csproj`.
- Add or update focused tests in `tests/GroundNotes.Tests` for behavior changes, especially repository, settings, watcher/save, editor, and AI flows.
