# Agent Guide: extern/AvaloniaEdit

## Local Scope
- This subtree is a forked third-party AvaloniaEdit editor used directly by GroundNotes.
- Keep changes narrow and easy to reapply during upstream refreshes; avoid broad upstream-style refactors unless explicitly requested.
- Read `README-ground-notes.md` before changing fork behavior.

## GroundNotes Patch Boundaries
- Preserve the local wrapped-line indentation patch set in `src/AvaloniaEdit/Rendering/`, especially `IVisualLineIndentationProvider`, `VisualIndentationElement`, and related `TextView` / `VisualLine` behavior.
- Treat `src/AvaloniaEdit/Rendering/TextView.cs` and `src/AvaloniaEdit/Rendering/VisualLine.cs` as high risk: caret placement, hit testing, inline objects, wrapped rows, and visual-column mapping can regress together.
- App-specific image asset handling belongs in `src/GroundNotes`; fork changes should be limited to editor layout/rendering primitives.

## Generated and Ignored Areas
- Do not edit `bin/`, `obj/`, package outputs, or other generated build artifacts.
- Do not hand-edit generated resource designer code such as `src/AvaloniaEdit/SR.Designer.cs`; change the source resources/generation path instead.

## Validation
- For fork changes, run `dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`.
- Also build the app/solution and run focused GroundNotes editor tests that cover the affected rendering or caret behavior.
