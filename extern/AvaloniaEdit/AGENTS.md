# Agent Guide: extern/AvaloniaEdit

## Scope
- This guide applies to `extern/AvaloniaEdit` and descendants unless a deeper `AGENTS.md` overrides it.
- This subtree is a forked third-party AvaloniaEdit editor used directly by GroundNotes.
- Keep changes narrow and easy to reapply during upstream refreshes; avoid broad upstream-style refactors unless explicitly requested.
- Read `README-ground-notes.md` before changing fork behavior.

## Project Boundaries
- GroundNotes and `GroundNotes.sln` reference only `src/AvaloniaEdit/AvaloniaEdit.csproj`. `src/AvaloniaEdit.TextMate/` and `src/AvaloniaEdit.Demo/` remain upstream integration/sample projects and are not part of the GroundNotes runtime path.
- Shared build settings in `Directory.Build.props` enable assembly signing and treat warnings as errors. The core library targets `netstandard2.0`; do not casually broaden its target or dependency surface.

## GroundNotes Patch Boundaries
- Read `src/AvaloniaEdit/Rendering/AGENTS.md` before layout changes and `src/AvaloniaEdit/Editing/AGENTS.md` before caret/input changes.
- Preserve the local wrapped-line indentation and per-line wrapping patch set in `src/AvaloniaEdit/Rendering/`, especially the provider interfaces, `VisualIndentationElement`, and related `TextView` / `VisualLine` behavior.
- Treat `src/AvaloniaEdit/Rendering/TextView.cs` and `src/AvaloniaEdit/Rendering/VisualLine.cs` as high risk: caret placement, hit testing, inline objects, wrapped rows, and visual-column mapping can regress together.
- App-specific image asset handling belongs in `src/GroundNotes`; fork changes should be limited to editor layout/rendering primitives.

## Generated and Ignored Areas
- Do not edit `bin/`, `obj/`, package outputs, or other generated build artifacts.
- Do not hand-edit generated resource designer code such as `src/AvaloniaEdit/SR.Designer.cs`; change the source resources/generation path instead.

## Validation
- For fork changes, run `dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`.
- Also build the app/solution and run focused GroundNotes editor tests that cover the affected rendering or caret behavior.
