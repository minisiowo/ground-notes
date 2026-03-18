# QuickNotesTxt

QuickNotesTxt is a small desktop app for writing plain-text notes in a folder you choose. It is built with Avalonia and .NET 10.

## What you need

Before you build the project, make sure you have:

1. Git
2. .NET SDK 10.0.103 or newer in the same feature band
3. A graphical desktop session if you want to run the app locally

The repository pins the SDK in `global.json`:

```json
{
  "sdk": {
    "version": "10.0.103",
    "rollForward": "latestFeature"
  }
}
```

If you use `mise`, the repo already contains a matching tool definition in `mise.toml`.

## Get the source

```bash
git clone <your-repository-url>
cd quick-notes-txt
```

## Install the .NET SDK

### Option 1: use mise

If you already use `mise`, run:

```bash
mise install
```

### Option 2: install .NET manually

Install the .NET 10 SDK and confirm it is available:

```bash
dotnet --version
```

You should see a version compatible with `10.0.103`.

## Restore dependencies

```bash
dotnet restore QuickNotesTxt.sln
```

## Build the project

To build everything in Debug:

```bash
dotnet build QuickNotesTxt.sln
```

To build only the desktop app:

```bash
dotnet build src/QuickNotesTxt/QuickNotesTxt.csproj
```

## Run the app

```bash
dotnet run --project src/QuickNotesTxt
```

On first launch, choose a folder where your notes should live. The app works directly with plain-text files in that folder.

## Install on Arch Linux

The simplest option on Arch is to build and run the app from source with the system .NET SDK.

Install prerequisites:

```bash
sudo pacman -S --needed git dotnet-sdk
```

Clone and run:

```bash
git clone <your-repository-url>
cd quick-notes-txt
dotnet restore QuickNotesTxt.sln
dotnet run --project src/QuickNotesTxt
```

If you want a standalone local install instead of running from the source tree:

```bash
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release -r linux-x64 --self-contained true
mkdir -p ~/.local/opt/QuickNotesTxt
cp -r src/QuickNotesTxt/bin/Release/net10.0/linux-x64/publish/* ~/.local/opt/QuickNotesTxt/
~/.local/opt/QuickNotesTxt/QuickNotesTxt
```

On ARM64 Linux, replace `linux-x64` with `linux-arm64`.

Optional desktop launcher:

```bash
mkdir -p ~/.local/share/applications
cat > ~/.local/share/applications/quick-notes-txt.desktop <<EOF
[Desktop Entry]
Type=Application
Name=QuickNotesTxt
Exec=/home/$USER/.local/opt/QuickNotesTxt/QuickNotesTxt
Terminal=false
Categories=Utility;Office;
EOF
```

QuickNotesTxt requires a graphical desktop session.

## Install on Windows

You can run the app from source with the .NET SDK or publish a self-contained build.

Install prerequisites:

1. Install Git.
2. Install the .NET 10 SDK.

Clone the repository:

```powershell
git clone <your-repository-url>
cd quick-notes-txt
```

Run from source:

```powershell
dotnet restore QuickNotesTxt.sln
dotnet run --project src/QuickNotesTxt
```

Create a standalone build:

```powershell
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release -r win-x64 --self-contained true
```

To publish, replace `C:\Apps\QuickNotes`, and recreate a Start Menu shortcut automatically, use one of these helper scripts:

Native Windows PowerShell:

```powershell
.\scripts\publish-and-install-windows.ps1
```

WSL:

```bash
./scripts/publish-and-install-wsl.sh
```

Both scripts:

- publish `QuickNotesTxt` as a self-contained `win-x64` app
- clear and repopulate `C:\Apps\QuickNotes`
- recreate `QuickNotesTxt.lnk` in the current user's Start Menu programs folder

Optional arguments:

- PowerShell: `-Runtime win-arm64 -Configuration Release`
- WSL: `./scripts/publish-and-install-wsl.sh win-arm64 Release`

The published app will be in:

```text
src\QuickNotesTxt\bin\Release\net10.0\win-x64\publish\
```

You can move that folder anywhere you want and launch `QuickNotesTxt.exe`.

On ARM64 Windows, replace `win-x64` with `win-arm64`.

## AI prompts

QuickNotesTxt can load AI prompt actions for selected editor text.

- Built-in prompts are shipped in `src/QuickNotesTxt/Assets/AiPrompts/`
- Custom prompts are loaded from `<notes-folder>/.quicknotestxt/ai-prompts/`
- Prompt files are JSON and can override built-in prompts when they use the same `id`
- Configure the OpenAI API key and default model from `AI Settings` in the app

The built-in prompt format looks like this:

```json
{
  "id": "translate",
  "name": "Translate With AI",
  "description": "Translate selected EN/PL text",
  "model": "gpt-5.4-mini",
  "replaceSelection": true,
  "order": 100,
  "promptTemplate": "... {selected}"
}
```

## Run the tests

```bash
dotnet test QuickNotesTxt.sln
```

At the time this README was written, the solution test suite contains 6 passing tests.

## Create a Release build

```bash
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release
```

The published output will be written to:

```text
src/QuickNotesTxt/bin/Release/net10.0/publish/
```

## Project layout

```text
src/QuickNotesTxt/               Avalonia desktop application
tests/QuickNotesTxt.Tests/       xUnit test project
global.json                      pinned .NET SDK version
mise.toml                        optional mise tool configuration
QuickNotesTxt.sln                solution file
```

## Common workflow

If you just want the shortest possible path from clone to running app:

```bash
git clone <your-repository-url>
cd quick-notes-txt
mise install   # optional, if you use mise
dotnet restore QuickNotesTxt.sln
dotnet run --project src/QuickNotesTxt
```

## Troubleshooting

### `dotnet` command not found

Install the .NET 10 SDK and make sure it is on your `PATH`.

### The app builds but does not open a window

Make sure you are starting it from a graphical desktop session, not a headless shell.

### SDK version mismatch

Check `global.json` and install the SDK version requested by the repo, or a compatible newer feature-band version allowed by `rollForward`.
