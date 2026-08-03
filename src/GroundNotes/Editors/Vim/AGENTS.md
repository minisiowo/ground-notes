# Agent Guide: src/GroundNotes/Editors/Vim

## Scope
This guide applies to the Vim engine under `Editors/Vim/`.

## Mental Model
- `VimEngine` is UI-independent: it consumes a `VimInput` plus immutable `VimDocumentSnapshot` and emits ordered `VimOperation` values. Keep Avalonia key translation, timers, undo-stack calls, caret application, and leader commands in `Views/VimEditorController`.
- `VimTextBuffer` is the source of line/newline and normalized Normal-mode offset semantics. Motion/operator changes must handle empty text, final lines without delimiters, CRLF/LF, inclusive versus exclusive ranges, counts, and preferred columns.
- A `VimWorkspaceState` shares the register across editor panes; engine reset must not accidentally turn pane switching into an isolated clipboard unless that behavior is intentionally changed.

## When Editing
- Preserve the mode transition contract across Normal, Insert, OperatorPending, Visual, and VisualLine modes and the ordering of register, edit, caret, selection, and history operations.
- Insert edits are grouped for undo by the view controller; table edits may be routed through its external-text-edit handler. Validate both pure engine output and controller integration.
- Add focused cases to `VimEngineTests` for engine semantics and `VimEditorControllerTests` for Avalonia key routing, shared registers, undo groups, leader sequences, timers, or table coordination. Settings normalization belongs with `VimModeSettingsTests`.
