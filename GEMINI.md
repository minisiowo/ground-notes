# GEMINI.md - GroundNotes

This file provides instructional context for Gemini CLI when working in the `ground-notes` repository. It complements the existing `CLAUDE.md` and `AGENTS.md` files.

## Project Overview

**GroundNotes** is a lightweight, cross-platform desktop note-taking application for plain-text notes. It is built using **.NET 10** and **Avalonia UI 11.3**. Notes are stored as `.txt` or `.md` files in a user-selected folder, using YAML-like frontmatter for metadata (title, tags, timestamps).

## Project Structure

```
GroundNotes/
├── GroundNotes.sln            # Main solution file
├── src/GroundNotes/           # Main application project
├── tests/GroundNotes.Tests/   # xUnit test suite
├── scripts/                   # Publish/install helper scripts
├── README.md                  # User-facing documentation
├── AGENTS.md                  # Codex/agent instructions
└── CLAUDE.md                  # Claude-specific instructions
```

## Development Workflow

### Build
```bash
dotnet build GroundNotes.sln
```

### Run
```bash
dotnet run --project src/GroundNotes
```

### Test
```bash
dotnet test GroundNotes.sln
```

Target a specific test class:
```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~NotesRepositoryTests"
```

### Publish
```bash
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release -r linux-x64 --self-contained true
```

## Coding Conventions

- **Namespaces:** Use file-scoped namespaces (`namespace GroundNotes.Services;`).
- **One top-level type per file** where practical.
- **Prefer sealed classes** unless inheritance is needed.
- **Keep code-behind minimal** in Avalonia views.
- **Keep filesystem operations deterministic**.
- **Use xUnit** for unit tests in `tests/GroundNotes.Tests/`.

## Notes for AI Changes

- Prompt-style AI actions belong in services like `OpenAiTextActionService`.
- Conversational AI chat belongs in `ChatViewModel` and `OpenAiChatService`.
- Keep note persistence deterministic: saved AI conversations should remain regular notes tagged with `AI`.

## Validation

Always verify changes with unit tests in `tests/GroundNotes.Tests/`.
