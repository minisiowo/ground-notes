# Plan: Smoother Mouse-Wheel Scrolling

## Goal
Improve mouse-wheel scrolling so editor content feels smoother and less jumpy, without changing note content, markdown rendering semantics, or persisted layout data.

## Recommended implementation path
Prefer an app-side smooth wheel controller attached through the shared editor host. This keeps the change local to GroundNotes UI behavior and avoids high-risk edits in the AvaloniaEdit fork unless later evidence shows the built-in logical scroll size is the only viable fix.

## Repository evidence
- `src/GroundNotes/Views/EditorHostController.cs`
  - Shared host for app `TextEditor` instances.
  - Constructed for the main editor in `MainWindow.axaml.cs`, secondary panes, and chat editor.
  - Already controls viewport directly in `ResetViewportToDocumentStart()` by casting `TextArea.TextView` to `IScrollable` and setting `Offset`.
- `src/GroundNotes/Views/MainWindow.axaml`
  - Main and secondary editors are `ae:TextEditor` with `HorizontalScrollBarVisibility="Disabled"`, `VerticalScrollBarVisibility="Auto"`, and `WordWrap="True"`.
- `src/GroundNotes/Views/ChatWindow.axaml`
  - Chat conversation editor uses the same `TextEditor` setup, so host-level integration would affect it too.
- `src/GroundNotes/Views/MainWindow.axaml.cs`
  - `OnPaneWorkspacePointerWheelChanged` maps `Shift+wheel` to horizontal multi-pane workspace scrolling.
  - `OnEditorTextViewScrollOffsetChanged` updates slash-command popups; smooth scrolling will increase event frequency.
- `src/GroundNotes/Views/ChatWindow.axaml.cs`
  - Uses `TextView.ScrollOffsetChanged` to track whether the user is near the bottom for chat auto-scroll.
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs`
  - Implements `ILogicalScrollable`/`IScrollable` and stores the actual scroll offset.
  - `ILogicalScrollable.ScrollSize => new Size(10, 50)` is likely a key reason wheel scrolling feels chunky.
  - Direct `IScrollable.Offset` changes are clamped by the fork and raise `ScrollOffsetChanged`.
- `extern/AvaloniaEdit/src/AvaloniaEdit/TextEditor.cs`
  - `ScrollToVerticalOffset()` and `ScrollToHorizontalOffset()` are effectively no-ops in this fork, so new code should not rely on them unless deliberately fixed.

## Implementation steps

### 1. Add a small app-side controller
Create a new internal sealed controller, likely:

- `src/GroundNotes/Views/EditorSmoothScrollController.cs`

Responsibilities:
- Attach to a single `AvaloniaEdit.TextEditor` from `EditorHostController`.
- Subscribe to `PointerWheelChanged` on `editor.TextArea` or `editor.TextArea.TextView` using a routing strategy that can intercept default wheel behavior when appropriate.
- For vertical unmodified wheel input, compute a target Y offset and set `((IScrollable)editor.TextArea.TextView).Offset`.
- Clamp offset to `[0, Extent.Height - Viewport.Height]`.
- Preserve fractional/high-resolution wheel deltas.
- Avoid handling events that should bubble to existing parent gestures.

Suggested defaults for first pass:
- Scale vertical wheel delta to a smaller pixel distance than the current apparent 50 px logical step, e.g. around 24-32 px per detent or a value derived from editor line height.
- If implementing animation/inertia, keep it modest: short duration, UI-thread timer, and coalesce wheel events into one target to avoid excessive `TextView` invalidations.
- If no actual offset movement is possible at top/bottom, leave `e.Handled = false` unless product behavior requires trapping wheel in the editor.

### 2. Wire it through `EditorHostController`
Modify `src/GroundNotes/Views/EditorHostController.cs` only after approval:
- Add a private field such as `_smoothScrollController`.
- Instantiate it in the constructor after the editor and text view are available.
- Dispose it from `EditorHostController.Dispose()`.

This should automatically cover:
- Main note editor (`src/GroundNotes/Views/MainWindow.axaml.cs` primary host).
- Secondary pane editors (`EditorHostController` created per secondary editor).
- Chat editor (`src/GroundNotes/Views/ChatWindow.axaml.cs`).

Chat must receive this behavior too. Do not add an opt-out flag for `ChatWindow`; the `EditorHostController` integration should apply uniformly to note editors, secondary pane editors, and the chat conversation editor.

### 3. Preserve existing gesture behavior
In the controller:
- Ignore `Shift+wheel` so `MainWindow.OnPaneWorkspacePointerWheelChanged()` can keep horizontal pane scrolling when the pointer is over editor content.
- Ignore `Ctrl+wheel` to avoid blocking future zoom/font-size gestures or OS conventions.
- Treat `Delta.X` separately; with word wrap and disabled horizontal scrollbar, do not invent horizontal editor scrolling unless explicitly requested.
- Do not use `TextEditor.ScrollToVerticalOffset()` because it is currently a no-op in the fork.

### 4. Add focused tests for scroll math and routing decisions
Prefer a pure/testable core to avoid brittle UI event tests. Possible files:
- `tests/GroundNotes.Tests/EditorSmoothScrollControllerTests.cs`

Test cases:
- Computes smaller vertical movement for standard wheel detents.
- Accumulates or preserves fractional wheel deltas.
- Clamps at top and bottom.
- Returns “do not handle” for `Shift` and `Ctrl` modifiers.
- Returns “do not handle” when no movement is possible at the scroll boundary, if bubbling-at-boundary is chosen.
- Does not produce negative offsets or offsets beyond `Extent - Viewport`.

Follow existing Avalonia test setup patterns from:
- `MarkdownImagePreviewLayerTests`
- `MarkdownVisualLineIndentationProviderTests`
- `EditorThemeControllerTests`

### 5. Optional follow-up: fork-level tuning only if app-side behavior is insufficient
If app-side interception fights Avalonia `ScrollViewer` behavior or feels worse on trackpads, consider a narrower fork change:
- `extern/AvaloniaEdit/src/AvaloniaEdit/Rendering/TextView.cs`
  - Change `ILogicalScrollable.ScrollSize` from fixed `new Size(10, 50)` to a smaller/dynamic vertical value.

Risks make this a second-choice path:
- `TextView.cs` is high-risk per `extern/AvaloniaEdit/AGENTS.md`.
- `ScrollSize` may affect keyboard/logical scrolling, not just mouse wheel.
- Fork changes require additional validation and are harder to rebase.

## Risks and edge cases
- Smooth animation can fire many more `ScrollOffsetChanged` events; check slash-command popup placement and image/code preview overlays.
- Chat auto-scroll near-bottom detection must remain stable and must not fight user scrolling.
- Trackpads and high-resolution wheels may produce fractional `PointerWheelEventArgs.Delta`; do not assume only `+1/-1`.
- Editor sits inside a horizontal workspace `ScrollViewer`; swallowing `Shift+wheel` would regress workspace navigation.
- Very small increments or high-frequency animations may trigger repeated `TextView` measure/visual invalidation and hurt large-note performance.
- Hidden scrollbars (`ThemeService.ApplyScrollBars`) should not affect scroll mechanics.

## Verification commands/checks
After implementation:

```bash
dotnet build src/GroundNotes/GroundNotes.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~EditorSmoothScrollControllerTests"
```

If any AvaloniaEdit fork files are changed:

```bash
dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~MarkdownImagePreviewLayerTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~MarkdownVisualLineIndentationProviderTests"
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~EditorThemeControllerTests"
```

Before final handoff:

```bash
dotnet test GroundNotes.sln
```

Manual smoke checks when a GUI session is available:
- Large markdown note: wheel scrolling feels smoother and remains responsive.
- Image previews and code-copy overlays stay aligned while scrolling.
- Slash command popup follows or hides correctly during scrolling.
- `Shift+wheel` over editor still scrolls the pane workspace horizontally.
- Chat history scrolling does not unexpectedly jump to bottom while reading old messages.

## Assumptions / open questions
- Assumption: the desired improvement applies primarily to editor areas, not every `ScrollViewer` in the app.
- Assumption: preserving existing `Shift+wheel` horizontal workspace behavior is more important than adding horizontal editor scrolling.
- Decision: the chat conversation editor should receive the same smooth wheel behavior as note editors.
- Decision: smooth scrolling should be enabled permanently; do not add a user setting or settings persistence for this change.
