# Agent Guide: extern/AvaloniaEdit/src/AvaloniaEdit/Editing

## Scope
- This guide applies to `Editing/` and descendants. Most code is upstream editor behavior; the local boundary is the independent visual caret-shape hook used by GroundNotes Vim mode.

## Caret Shape Contract
- `TextArea.CaretShape` selects `Bar` or `Block` rendering without changing insertion semantics. Keep it independent from `TextArea.OverstrikeMode`, which changes text input by selecting/replacing the next character.
- `Caret.cs` chooses glyph-aware block geometry, `CaretLayer.cs` renders the block treatment, and `TextArea.OnPropertyChanged` refreshes a visible caret. Update these together if the shape contract changes.
- Caret geometry depends on `Rendering.VisualLine` coordinate mapping. For wrapped or visually indented lines, fix shared positioning in `Rendering/` rather than adding Editing-side offsets.

## Tests
- Cover shape-only behavior with focused `VimEditorControllerTests` and the caret geometry cases in `EditorThemeControllerTests`; verify that block shape still inserts rather than overstrikes.
- Run the fork build plus the focused GroundNotes editor tests after changes.
