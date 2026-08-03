# Agent Guide: src/GroundNotes/ViewModels

## Scope
This guide applies to `ViewModels/` and descendants unless a deeper `AGENTS.md` overrides it.

## Mental Model
- `MainViewModel` is intentionally split by domain: note persistence in `.Notes.cs`, appearance/settings, AI, sidebar/calendar operations, and slash commands. Extend the matching partial rather than growing `MainViewModel.cs` indiscriminately.
- The primary editor state lives on `MainViewModel`; each secondary editor has an `EditorPaneViewModel`. Changes to open/select/save/conflict behavior usually need symmetric primary and secondary-pane handling.
- Use CommunityToolkit-generated properties and commands for bindable state. Keep focus, popup placement, editor controls, and other Avalonia mechanics as events consumed by `Views/`.

## Save And Reconciliation Rules
- Editor changes are debounced, cloned before persistence, versioned, and serialized through `_notePersistenceLock`. A save may return a new path; merge persistence fields without overwriting edits made while the save was in flight.
- Invalid visible frontmatter, stale hashes, or external edits block switching/closing according to the existing conflict flow. Do not silently discard local edits or auto-reload a dirty pane.
- Local mutations use `NoteMutationService.BeginMutationScope`; watcher suppression and mutation-origin filtering prevent a window from treating its own save as an external edit. Cross-window mutations must still update clean panes or mark dirty panes conflicted.
- Selection guards (`_isApplyingSelection` and sidebar synchronization flags) prevent collection replacement from opening the wrong note. Preserve them around programmatic selection/property updates.
- Folder changes and window close must flush or reject pending saves, stop pane cancellation sources, and dispose watcher/event subscriptions.

## Domain Boundaries
- `ChatViewModel` builds conversational history and optionally persists/appends notes through `INoteMutationService`; prompt actions in `MainViewModel.Ai.cs` are a separate flow.
- Settings editors preview changes before committing. Keep normalization in models/services and preserve cancel/restore behavior.
- Sidebar bulk tag/folder operations can touch many notes and intentionally preserve timestamps; keep path validation, mutation batching, and open-pane updates aligned.

## Tests
- Behavioral changes normally belong in `MainViewModelTests`, `ChatViewModelTests`, `SettingsViewModelTests`, or the matching focused editor view-model test.
- Cover primary and secondary panes, edits that race a save, external mutation/watcher events, invalid frontmatter, conflicts, folder switching, and disposal when the changed path can affect them.
