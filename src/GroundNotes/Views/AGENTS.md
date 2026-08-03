# Agent Guide: src/GroundNotes/Views

## Scope
This guide applies to `Views/` and descendants unless a deeper `AGENTS.md` overrides it.

## Mental Model
- `MainWindow.axaml` declares the workspace; `MainWindow.axaml.cs` and its partials own window/editor/sidebar orchestration. Extract reusable stateful behavior into focused `*Controller` classes instead of adding another independent event system.
- `EditorHostController` is the per-editor facade joining theme/rendering, text synchronization, layout, tables, lists, and Vim. Primary and secondary editors must be configured and disposed consistently.
- `WorkspaceWindowManager` creates independent view models/windows over shared services. Standalone windows are initialized hidden, configured, shown, then revealed; failure paths must dispose windows or view models.

## Editor Contracts
- Guard two-way editor synchronization with `EditorTextSyncController` flags. Replacing a document must preserve/clamp caret and selection, refresh layout/render layers, and avoid feeding normalized text back as a user edit.
- Markdown mode may normalize tables; YAML/frontmatter mode disables markdown presentation and normalization. Apply display-mode changes to every editor host.
- Vim key routing runs in tunnel handlers and coordinates undo groups/table edits. Slash commands, list continuation, general shortcuts, and Vim must retain deliberate precedence.
- Multi-pane dictionaries are keyed by `EditorPaneViewModel.Id`. Add/remove every control, host, subscription, popup target, and synced-path entry together.

## Avalonia Lifecycle
- UI updates originating from watchers/background tasks must reach `Dispatcher.UIThread` before touching controls or observable UI state.
- Pair every event handler, routed handler, timer, bitmap, popup/controller, and editor host with cleanup in close/dispose paths. `MainWindow.DisposeWindowResources` is idempotent and disposes its `MainViewModel`.
- Closing is asynchronous: first ask `MainViewModel.PrepareToCloseAsync`, then save layout synchronously on the approved close. Do not allow a window to disappear with unresolved conflicts or pending invalid frontmatter.
- Layout settings are persisted and normalized. Preserve off-screen recovery, last normal bounds when maximized, pane weights/shared widths, sidebar state, standard/zen standalone sizing, and startup's hidden-until-laid-out behavior.

## Tests
- Controller/math behavior should remain testable without showing the whole app. Relevant suites include `EditorThemeControllerTests`, `EditorMarkdownTableControllerTests`, `VimEditorControllerTests`, `SlashCommandPopupControllerTests`, `MainWindowShortcutTests`, `MainWindowZenModeTests`, `SidebarDragTests`, window placement/chrome tests, and `ModalDialogRunnerTests`.
- For interaction changes, also build the app; only run it in a graphical session.
