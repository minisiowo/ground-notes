# Agent Guide: extern/AvaloniaEdit/src/AvaloniaEdit/Rendering

## Scope
- This guide applies to `Rendering/` and descendants. It is the high-risk boundary for the GroundNotes visual indentation and line-wrapping fork patches.

## Mental Model
- `TextView.BuildVisualLine` constructs elements, inserts any zero-document-length `VisualIndentationElement`, applies transformers, then formats one or more Avalonia `TextLine` rows.
- `IVisualLineIndentationProvider` controls leading visual-only columns, trailing reserved width, and an optional continuation alignment column. `IVisualLineWrappingProvider` overrides wrapping per document line, while `TextView.DefaultTextWrapping` decouples the default from horizontal scrolling.
- `VisualLine` is the coordinate bridge among document offsets, visual columns, formatted rows, and screen positions. Its `WrappedLineContinuationIndent` must be applied symmetrically when drawing, finding positions, and hit testing.

## Coupled Files
- Treat `TextView.cs`, `VisualLine.cs`, `VisualIndentationElement.cs`, `VisualLineElement.cs`, `VisualLineTextSource.cs`, `InlineObjectRun.cs`, and `BackgroundGeometryBuilder.cs` as one behavioral surface when changing wrapped-row positioning.
- Preserve `VisualIndentationElement` as visual length with zero document length. Its line-border/caret-stop behavior prevents a duplicate caret stop at the real content-start offset.
- Keep provider interfaces narrow host hooks. GroundNotes owns Markdown classification and image/table policy; this fork owns reusable formatting and coordinate primitives.

## Hazards
- Check first rows and continuation rows separately, including blank and indentation-only lines, native leading whitespace, explicit continuation columns, narrow widths, trailing insets, and virtual space.
- Verify both directions of every mapping: document offset to visual column/point and pointer point back to document position. Selection/background rectangles and inline controls must use the same row start offset as text drawing.
- `VisualLineIndentationProvider` is an auto-property and does not invalidate layout by itself; callers must redraw when attaching it or when its answers change. `DefaultTextWrapping` and `VisualLineWrappingProvider` clear visual lines and invalidate measure in their setters.

## Tests
- Build the fork, then run focused GroundNotes coverage in `EditorThemeControllerTests`, `VisualIndentationEditingTests`, `MarkdownVisualLineIndentationProviderTests`, and relevant image/table rendering tests.
- Rendering assertions require the Avalonia headless harness and queued render work to be flushed as described in `tests/GroundNotes.Tests/AGENTS.md`.
