# GroundNotes

GroundNotes is a desktop notes app built around plain text files in a folder you control.

It is designed for people who want local notes first: no database, no proprietary file format, no forced cloud sync. The app works directly on `.txt` and `.md` files, adds structured frontmatter for metadata, and layers a focused desktop UI on top.

## What It Does

- Works on a normal folder of note files.
- Supports `.txt` and `.md` notes.
- Stores note metadata in simple YAML-like frontmatter.
- Lets you search, filter by tag, sort, rename, and edit notes quickly.
- Includes markdown-aware editor styling for headings, lists, links, inline code, and fenced code blocks.
- Supports markdown image previews using `![](path)|NN` syntax, including image paste directly into the notes folder `assets/` directory.
- Watches the notes folder for external filesystem changes.
- Includes theme, UI font, editor font, code font, indentation, and line-height settings.
- Supports OpenAI-powered text actions on selected text.
- Includes a dedicated AI Chat window that can reference notes as context and save conversations back as notes.

## Note Format

GroundNotes stores notes as regular text files with frontmatter followed by the body.

Example:

```text
---
title: Project Ideas
tags: [work, backlog]
createdAt: 2026-03-23T08:15:00.0000000+01:00
updatedAt: 2026-03-23T09:30:00.0000000+01:00
---
Build a smaller note-capture flow for quick thoughts.
```

Notes remain readable outside the app and can be edited with any text editor.

### Image Previews

GroundNotes can render local markdown image references directly inside the editor.

- Paste an image into the editor with `Ctrl+V` or `Paste`.
- The image file is saved into `<notes-folder>/assets/`.
- The editor inserts markdown like `![](assets/image-20260401-123456789.png)|100`.
- The `|NN` suffix controls preview scale as a percentage of the source image size.

Examples:

```md
![](assets/photo.png)|100
![](assets/photo.png)|50
![](assets/photo.png)|25
```

The stored note text stays plain markdown; the image preview is render-only behavior in the editor.

## AI Features

GroundNotes has two separate AI workflows.

### 1. Prompt Actions

Prompt actions run on selected editor text.

- Built-in prompts are bundled in `src/GroundNotes/Assets/AiPrompts/`
- Custom prompts are loaded from `<notes-folder>/.quicknotestxt/ai-prompts/`
- Custom prompts can override built-in prompts by using the same `id`

Example prompt definition:

```json
{
  "id": "translate",
  "name": "Translate With AI",
  "description": "Translate selected EN/PL text",
  "model": "gpt-5.4-mini",
  "temperature": 0.7,
  "max_tokens": 500,
  "reasoning_effort": "medium",
  "replaceSelection": true,
  "order": 100,
  "promptTemplate": "Jestes wyspecializowanym tlumaczem... {selected}"
}
```

Optional prompt parameters:

- `temperature`
- `max_tokens`
- `reasoning_effort`

### 2. AI Chat

The AI Chat window is meant for longer interactions than one-shot prompt actions.

- Select the chat model from the toolbar.
- Reference notes with `@`.
- Start chat from the current note with note context pre-attached.
- Save the conversation as a new note.
- Append the chat result back into the originating note.

Saved conversations are regular notes tagged with `AI`.

## Requirements

Before building the project, make sure you have:

1. Git
2. .NET SDK `10.0.103` or a compatible newer SDK in the same feature band
3. A graphical desktop session to run the app

The repository pins the SDK in `global.json`:

```json
{
  "sdk": {
    "version": "10.0.103",
    "rollForward": "latestFeature"
  }
}
```

If you use `mise`, the repo already includes `mise.toml`.

## Getting Started

Clone the repository:

```bash
git clone <your-repository-url>
cd ground-notes
```

If you use `mise`:

```bash
mise install
```

Restore dependencies:

```bash
dotnet restore GroundNotes.sln
```

Build the solution:

```bash
dotnet build GroundNotes.sln
```

Run the app:

```bash
dotnet run --project src/GroundNotes
dotnet run --project src/GroundNotes --no-build
```

On first launch, choose the folder that should hold your notes.

## Build, Test, Publish

Build the full solution:

```bash
dotnet build GroundNotes.sln
```

Build only the desktop app:

```bash
dotnet build src/GroundNotes/GroundNotes.csproj
```

Run all tests:

```bash
dotnet test GroundNotes.sln
```

Run only the test project:

```bash
dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj
```

Publish a release build:

```bash
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release
```

## Platform Notes

### Linux

The app needs a graphical desktop session. In a headless shell you can build and run tests, but not launch the UI.

### Arch Linux

Install prerequisites:

```bash
sudo pacman -S --needed git dotnet-sdk
```

Then build and run from source:

```bash
git clone <your-repository-url>
cd ground-notes
dotnet restore GroundNotes.sln
dotnet run --project src/GroundNotes
```

Install with the helper script:

```bash
./scripts/publish-and-install-linux.sh
```

Optional arguments:

- `./scripts/publish-and-install-linux.sh linux-x64 Release`
- `./scripts/publish-and-install-linux.sh linux-arm64 Release`

The script publishes a self-contained build and installs it to:

```text
~/.local/opt/GroundNotes
```

Example desktop launcher:

```ini
[Desktop Entry]
Type=Application
Name=GroundNotes
Exec=/home/YOUR_USER/.local/opt/GroundNotes/GroundNotes
Terminal=false
Categories=Utility;Office;
```

You can save that as:

```text
~/.local/share/applications/ground-notes.desktop
```

### Windows

Run from source:

```powershell
git clone <your-repository-url>
cd ground-notes
dotnet restore GroundNotes.sln
dotnet run --project src/GroundNotes
```

Publish a standalone build:

```powershell
dotnet publish src/GroundNotes/GroundNotes.csproj -c Release -r win-x64 --self-contained true
```

Helper scripts are available:

- PowerShell: `.\scripts\publish-and-install-windows.ps1`
- WSL: `./scripts/publish-and-install-wsl.sh`

Both scripts publish a self-contained Windows build, refresh the install directory, and recreate a Start Menu shortcut for `GroundNotes`.

Default install location:

```text
C:\Apps\GroundNotes
```

Optional arguments:

- PowerShell: `-Runtime win-arm64 -Configuration Release`
- WSL: `./scripts/publish-and-install-wsl.sh win-arm64 Release`

## Project Layout

```text
src/GroundNotes/               Avalonia desktop application
src/GroundNotes/Models/        note, AI, theme, and font models
src/GroundNotes/Services/      filesystem, settings, AI, themes, fonts
src/GroundNotes/ViewModels/    MVVM state and commands
src/GroundNotes/Views/         Avalonia windows and UI glue
tests/GroundNotes.Tests/       xUnit tests
GroundNotes.sln                solution file
global.json                    pinned SDK version
mise.toml                      optional mise configuration
```

## Troubleshooting

### `dotnet` command not found

Install the .NET 10 SDK and make sure it is on your `PATH`.

### The app builds but no window opens

Make sure you are running it in a graphical desktop session.

### `original.pdb` is locked during `dotnet run` or `dotnet test`

This can happen when a previous `dotnet` process is still holding the forked `AvaloniaEdit` build output open.

- Close the running app and retry.
- Or use `dotnet run --project src/GroundNotes --no-build` after a successful build.

### SDK version mismatch

Check `global.json` and install the requested SDK version or a compatible newer feature-band version allowed by `rollForward`.
