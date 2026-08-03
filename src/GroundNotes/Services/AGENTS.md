# Agent Guide: src/GroundNotes/Services

## Scope
This guide applies to `Services/` and descendants unless a deeper `AGENTS.md` overrides it.

## Mental Model
- Interfaces are constructor seams used by `App.axaml.cs` and tests. Keep filesystem, network, settings, and dialogs behind those seams rather than reaching into Avalonia views.
- `NotesRepository` owns the on-disk note format and deterministic querying. `NoteMutationService` serializes writes, rejects stale source hashes, and broadcasts mutations with an origin ID so multiple workspace windows can reconcile changes.
- `FileWatcherService` reports only top-level `.md`/`.txt` changes and coalesces filesystem bursts. It does not marshal to the UI thread; consumers do that.
- `FolderSettingsService` owns atomic `settings.json` replacement, normalization, and legacy shortcut migration. Update its record mapping in both directions whenever `AppSettings` changes.
- Prompt actions/title suggestions share `IOpenAiCompletionsClient`; conversational chat remains behind `IAiChatService`. Do not bypass these abstractions or make tests call the network.

## Persistence Contracts
- Notes are UTF-8 top-level files. Same-stem `.md` wins over `.txt`; new saves use `.md`, sanitize titles, detect collisions across both extensions, and may move the old path.
- Frontmatter handling is intentionally small, not a general YAML parser. Preserve unknown lines and the structured `title`, `tags`, `createdAt`, and `updatedAt` fields; malformed editable frontmatter must block saving rather than be rewritten.
- Preserve `SourceContentHash` conflict checks, `preserveTimestamp`, mutation locking/events, platform-aware path behavior, search scoring, and stable tie-break ordering.
- `NoteAssetService` manages only direct children of `<notes>/assets`; keep path-containment, collision, and `![](assets/name)|scale` semantics intact.
- AI prompts and custom slash commands are user data under `<notes>/.groundnotes/`; loaders can create starter files and migration markers. Treat malformed entries as warnings where current APIs do, and retain ID/filename collision checks.

## When Editing
- Prefer atomic settings updates through `UpdateSettings*`; a read-modify-save sequence can lose another window's changes.
- Keep synchronous layout saves available for window-closing paths.
- Add focused coverage in the matching repository/service test class. High-risk suites include `NotesRepositoryTests`, `NoteMutationServiceTests`, `FileWatcherServiceTests`, `FolderSettingsServiceTests`, prompt/slash catalog tests, `NoteAssetServiceTests`, and OpenAI service tests using fake HTTP handlers.
