# Project Agent Guide

## Project Map
- GroundNotes is a .NET 10 Avalonia desktop app over plain top-level `.md` and `.txt` files in a user-selected folder.
- `src/GroundNotes/` contains the app, `tests/GroundNotes.Tests/` the xUnit/Avalonia-headless suite, and `extern/AvaloniaEdit/` the in-repo editor fork referenced directly by the app.
- `scripts/` contains destructive publish-and-install helpers; generated `bin/`, `obj/`, package, and coverage output is not source.

## Working Rules
- Read the nearest nested `AGENTS.md` before editing. Local guides contain the subsystem contracts and focused test guidance; do not infer app behavior from the upstream fork or vice versa.
- Use .NET SDK `10.0.103` (`global.json` permits later feature bands and `mise.toml` pins the exact SDK). Nullable reference types, implicit usings, and the latest C# language version are enabled repository-wide.
- Keep tests isolated from real note folders, the real settings location, and network services. Settings, including the OpenAI API key, live outside the repo at `<LocalApplicationData>/GroundNotes/settings.json`.
- Preserve plain-text compatibility: unknown frontmatter, timestamps where requested, rename/collision behavior, and render-only markdown presentation must survive edits.

## Commands
- Restore: `dotnet restore GroundNotes.sln`.
- Full verification: `dotnet build GroundNotes.sln` then `dotnet test GroundNotes.sln`.
- App-only build: `dotnet build src/GroundNotes/GroundNotes.csproj`.
- Focus one test class: `dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~ClassName"`; use `--no-build` only after building the latest changes.
- Run only in a graphical desktop session: `dotnet run --project src/GroundNotes`; headless sessions should build and test instead.

## Nested Guidance
- `src/GroundNotes/AGENTS.md`: app architecture and links to service, view-model, editor, Vim, and view guidance.
- `tests/GroundNotes.Tests/AGENTS.md`: headless harness, isolation, shared-state hazards, and test conventions.
- `extern/AvaloniaEdit/AGENTS.md`: fork boundaries plus scoped rendering and editing guidance.

## Hazards
- If `original.pdb` is locked, stop the running `dotnet`/GroundNotes process; after a successful build, `dotnet run --project src/GroundNotes --no-build` avoids rebuilding the fork.
- Read `extern/AvaloniaEdit/README-ground-notes.md` before fork changes. Its wrapped-line and caret patches couple rendering, coordinate mapping, hit testing, and editing behavior.
- `scripts/publish-and-install-*` delete app build output and replace platform install directories (`~/.local/opt/GroundNotes` or `C:\Apps\GroundNotes`); run them only when installation is intended.
