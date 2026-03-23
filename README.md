# QuickNotesTxt

QuickNotesTxt is a desktop notes app built around plain text files in a folder you control.

It is designed for people who want local notes first: no database, no proprietary file format, no forced cloud sync. The app works directly on `.txt` and `.md` files, adds structured frontmatter for metadata, and layers a focused desktop UI on top.

## What It Does

- Works on a normal folder of note files.
- Supports `.txt` and `.md` notes.
- Stores note metadata in simple YAML-like frontmatter.
- Lets you search, filter by tag, sort, rename, and edit notes quickly.
- Watches the notes folder for external filesystem changes.
- Includes theme, UI font, editor font, code font, indentation, and line-height settings.
- Supports OpenAI-powered text actions on selected text.
- Includes a dedicated AI Chat window that can reference notes as context and save conversations back as notes.

## Note Format

QuickNotesTxt stores notes as regular text files with frontmatter followed by the body.

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

## AI Features

QuickNotesTxt has two separate AI workflows.

### 1. Prompt Actions

Prompt actions run on selected editor text.

- Built-in prompts are bundled in `src/QuickNotesTxt/Assets/AiPrompts/`
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
cd quick-notes-txt
```

If you use `mise`:

```bash
mise install
```

Restore dependencies:

```bash
dotnet restore QuickNotesTxt.sln
```

Build the solution:

```bash
dotnet build QuickNotesTxt.sln
```

Run the app:

```bash
dotnet run --project src/QuickNotesTxt
```

On first launch, choose the folder that should hold your notes.

## Build, Test, Publish

Build the full solution:

```bash
dotnet build QuickNotesTxt.sln
```

Build only the desktop app:

```bash
dotnet build src/QuickNotesTxt/QuickNotesTxt.csproj
```

Run all tests:

```bash
dotnet test QuickNotesTxt.sln
```

Run only the test project:

```bash
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj
```

Publish a release build:

```bash
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
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
cd quick-notes-txt
dotnet restore QuickNotesTxt.sln
dotnet run --project src/QuickNotesTxt
```

### Windows

Run from source:

```powershell
git clone <your-repository-url>
cd quick-notes-txt
dotnet restore QuickNotesTxt.sln
dotnet run --project src/QuickNotesTxt
```

Publish a standalone build:

```powershell
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release -r win-x64 --self-contained true
```

Helper scripts are available:

- PowerShell: `.\scripts\publish-and-install-windows.ps1`
- WSL: `./scripts/publish-and-install-wsl.sh`

## Project Layout

```text
src/QuickNotesTxt/               Avalonia desktop application
src/QuickNotesTxt/Models/        note, AI, theme, and font models
src/QuickNotesTxt/Services/      filesystem, settings, AI, themes, fonts
src/QuickNotesTxt/ViewModels/    MVVM state and commands
src/QuickNotesTxt/Views/         Avalonia windows and UI glue
tests/QuickNotesTxt.Tests/       xUnit tests
QuickNotesTxt.sln                solution file
global.json                      pinned SDK version
mise.toml                        optional mise configuration
```

## Troubleshooting

### `dotnet` command not found

Install the .NET 10 SDK and make sure it is on your `PATH`.

### The app builds but no window opens

Make sure you are running it in a graphical desktop session.

### SDK version mismatch

Check `global.json` and install the requested SDK version or a compatible newer feature-band version allowed by `rollForward`.
