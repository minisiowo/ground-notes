# Agent Guide: src/GroundNotes/Editors

## Scope
This guide applies to `Editors/` and descendants unless a deeper `AGENTS.md` overrides it.

## Mental Model
- Pure parsers/formatters/edit commands should remain independent of Avalonia controls where possible. Rendering providers, transformers, and layers adapt those results to AvaloniaEdit and are wired by `Views/EditorHostController` and `EditorThemeController`.
- Markdown line analysis tracks fenced-code state across lines. Caches and presentation indexes subscribe to `TextDocument.Changed`; invalidate from the changed line when prior context can affect later lines, and always detach on document replacement/disposal.
- Tables couple parser/model, formatter, wrapping/presentation index, colorizer, editing commands, and `Views/EditorMarkdownTableController`. Lists similarly couple parsing, visual indentation, continuation tracking, and `EditorMarkdownListController`.

## Persisted Text Versus Presentation
- Image previews, code-block backgrounds/copy controls, syntax color, wrapped-list indentation, fenced-code inset, and table wrapping are presentation layers. They must not insert placeholder characters or visual indentation into persisted note text.
- Actual editing commands return explicit replacement and selection/caret offsets. Preserve newline style, selection behavior, undo grouping, and offset mapping when changing commands or table formatting.
- The persisted image extension is `![](path)|NN`; keep scale parsing and relative-path resolution compatible with `Services/NoteAssetService`.
- If a fix requires wrapped-line geometry, caret placement, hit testing, inline-object positioning, or visual-column mapping, inspect the in-repo AvaloniaEdit fork first rather than compensating here.

## Performance And Lifecycle
- Rendering runs frequently. Reuse `MarkdownLineAnalysisCache`, fence tracking, style-span buffers, and table indexes; avoid reparsing the full document from per-line render callbacks.
- New document subscriptions, bitmaps, controls, or layers need deterministic invalidation and disposal.

## Tests
- Keep parser/formatter/edit algorithms covered by focused `Markdown*Tests`; visual indentation changes also require `VisualIndentationEditingTests` and fork-aware editor validation.
- Table changes should cover both `MarkdownTableTests` and `EditorMarkdownTableControllerTests`; preview changes should cover provider and layer tests. See `Editors/Vim/AGENTS.md` for Vim-specific validation.
