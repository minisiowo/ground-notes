# GroundNotes AvaloniaEdit Fork Notes

- Upstream repository: `https://github.com/AvaloniaUI/AvaloniaEdit`
- Upstream base commit: `85ffd45f7a4568e02b403854bf442f7b8ea5aa77`
- Upstream package lineage: `Avalonia.AvaloniaEdit 11.4.0`

## Local patch purpose

GroundNotes needs stable wrapped-line indentation for:

- fenced code blocks that use a visual-only inset, and
- ordinary lines that rely on native leading whitespace indentation.

The local patch adds a narrow `IVisualLineIndentationProvider` hook in `TextView` so host apps can:

- inject a visual-only leading indentation before wrapped continuation indentation is calculated, and
- override the wrapped continuation alignment column without shifting the first rendered row.

GroundNotes also carries matching `TextView` / `VisualLine` positioning patches so wrapped rows,
inline objects, hit-testing, and visual-column mapping all honor that visual-only continuation indent.

## Upgrade procedure

1. Refresh `extern/AvaloniaEdit` from the desired upstream commit.
2. Reapply the `IVisualLineIndentationProvider`, `VisualIndentationElement`, wrapped continuation-alignment hook, and wrapped-line positioning patches in `src/AvaloniaEdit/Rendering/`.
3. Build `extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`.
4. Build and run the GroundNotes editor regression tests.
