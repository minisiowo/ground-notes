# GroundNotes Agent Guide

## Scope And Toolchain
- `GroundNotes.sln` contains the Avalonia desktop app, its xUnit tests, and the in-repo AvaloniaEdit fork; the app references the fork as a project, not a NuGet package.
- Use .NET SDK `10.0.103` (`global.json` allows later feature bands; `mise.toml` pins the exact SDK). There is no separate lint, formatter, codegen, or CI workflow in this repo.
- Read the nearest scoped guide before editing: `src/GroundNotes/AGENTS.md`, `tests/GroundNotes.Tests/AGENTS.md`, or `extern/AvaloniaEdit/AGENTS.md`.

## Commands
- Restore: `dotnet restore GroundNotes.sln`.
- Full verification: `dotnet build GroundNotes.sln` then `dotnet test GroundNotes.sln`.
- App-only build: `dotnet build src/GroundNotes/GroundNotes.csproj`.
- Focus one test class: `dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --filter "FullyQualifiedName~ClassName"`; use `--no-build` only after building the latest changes.
- Run only in a graphical desktop session: `dotnet run --project src/GroundNotes`; headless sessions should build and test instead.
- If `original.pdb` is locked, stop the running `dotnet`/GroundNotes process; after a successful build, `dotnet run --project src/GroundNotes --no-build` avoids rebuilding the fork.

## Architecture And Data
- `src/GroundNotes/App.axaml.cs` is the manual composition root; there is no DI container. Business/persistence logic belongs in services and view models, while views/controllers own Avalonia and editor wiring.
- Preserve the `MainViewModel.*.cs` partial split and keep prompt-action AI services separate from conversational chat services.
- Notes are top-level `.md`/`.txt` files in a user-selected folder, not database records. Save operations may rename or migrate files; preserve unknown frontmatter content, requested timestamps, search/list ordering, and filename collision behavior.
- Settings, including the OpenAI API key, are persisted outside the repo at `<LocalApplicationData>/GroundNotes/settings.json`; tests must inject temporary settings/directories and must never use a real notes folder or network call.
- Markdown image syntax `![](path)|NN` is persisted text. Previews and code-block indentation are visual-only layers; do not insert placeholder document content to implement them.

## AvaloniaEdit Fork
- Read `extern/AvaloniaEdit/README-ground-notes.md` before fork changes. The local wrapped-line indentation and positioning patches couple layout, caret placement, hit testing, inline objects, and visual-column mapping.
- Validate fork changes with `dotnet build extern/AvaloniaEdit/src/AvaloniaEdit/AvaloniaEdit.csproj`, the app/solution build, and focused markdown/editor tests.

## Publish Scripts
- The `scripts/publish-and-install-*` helpers delete `src/GroundNotes/bin` and `obj`, publish self-contained binaries, then replace the platform install directory (`~/.local/opt/GroundNotes` or `C:\Apps\GroundNotes`). Run them only when installation is intended.
