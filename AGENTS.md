# Project Agent Guide

## Project Purpose
GroundNotes is a local-first desktop notes app for plain-text folders. It edits `.md` and `.txt` notes directly, preserves frontmatter metadata, renders markdown/editor enhancements, watches the filesystem, and includes prompt-action plus chat-based AI workflows.

## Stack and Entry Points
- .NET 10 SDK pinned by `global.json` and `mise.toml` (`10.0.103`, roll forward latest feature).
- Avalonia UI 11 desktop app in `src/GroundNotes`.
- Forked AvaloniaEdit editor in `extern/AvaloniaEdit`.
- xUnit tests in `tests/GroundNotes.Tests`.
- Shared C# settings in `Directory.Build.props`: nullable enabled, implicit usings enabled, latest language version.

## Agent Guide Network
- `src/GroundNotes/AGENTS.md`: app architecture, MVVM boundaries, editor/image-preview hazards.
- `tests/GroundNotes.Tests/AGENTS.md`: test harness patterns, fakes, Avalonia test setup.
- `extern/AvaloniaEdit/AGENTS.md`: fork-specific hazards and validation.
- Follow the nearest nested guide in addition to this root guide.

## Build, Test, and Run
- Restore: `dotnet restore GroundNotes.sln`.
- Build all projects: `dotnet build GroundNotes.sln`.
- Build the app: `dotnet build src/GroundNotes/GroundNotes.csproj`.
- Build the editor fork when it changes: `dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`.
- Run tests: `dotnet test GroundNotes.sln` or target `tests/GroundNotes.Tests/GroundNotes.Tests.csproj` with `--filter` for focused loops.
- Run the app only when a graphical desktop session is available: `dotnet run --project src/GroundNotes`.
- For Windows try-out/deployment from WSL after changes are ready: `bash scripts/publish-and-install-wsl.sh`.

## Repository Practices
- Keep edits small, architecture-aligned, and easy to review.
- Read nearby code before changing it and preserve existing patterns over broad refactors.
- Add or update focused tests for behavior changes.
- Prefer build/test validation in headless environments rather than launching the GUI.
- Do not commit secrets, tokens, machine-local credentials, or generated build outputs.
- Do not touch unrelated working-tree changes.

## Coding Conventions
- Use file-scoped namespaces and 4-space indentation.
- Order `using` directives as `System.*`, third-party namespaces, then `GroundNotes.*`.
- Prefer `sealed` concrete classes, guard clauses, explicit boundary null checks, and cohesive methods.
- Use `var` when the right-hand type is obvious; otherwise use explicit types.
- Use `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase` for string/path comparisons.
- Respect nullable contracts and avoid the null-forgiving operator unless there is no practical alternative.

## Product and Data Invariants
- Preserve support for `.md` and `.txt` notes, frontmatter compatibility, filename sanitization, deterministic parsing, search/list ordering, save behavior, and persisted local settings/layout data.
- Keep business logic in services/view models and UI-only wiring in Avalonia views/controllers.
- Keep prompt-action AI services separate from conversational chat services.
- Treat markdown image preview syntax `![](path)|NN` as persisted text with render-only preview behavior layered on top.

## Ignored and Generated Areas
- Avoid `bin/`, `obj/`, `TestResults/`, `artifacts/`, `publish/`, `out/`, IDE settings, and environment files.
- Generated or third-party fork changes need local justification and focused validation.
