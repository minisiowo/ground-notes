# GEMINI.md - QuickNotesTxt

This file provides instructional context for Gemini CLI when working in the `quick-notes-txt` repository. It complements the existing `CLAUDE.md` and `AGENTS.md` files.

## Project Overview

**QuickNotesTxt** is a lightweight, cross-platform desktop note-taking application for plain-text notes. It is built using **.NET 10** and **Avalonia UI 11**. Notes are stored as `.txt` or `.md` files in a user-selected folder, using YAML-like frontmatter for metadata (title, tags, timestamps).

### Core Features
- **Markdown Support:** Editor with syntax highlighting (via AvaloniaEdit).
- **Auto-save:** Debounced (450ms) auto-saving to disk.
- **AI Integration:** OpenAI-powered text actions (translate, summarize, etc.) with customizable prompt templates.
- **File System Driven:** Monitors the notes folder for external changes using a `FileWatcherService`.
- **Theming & Fonts:** Supports built-in and custom themes, plus bundled fonts (Iosevka, JetBrains Mono, Monaspace).

## Tech Stack

- **Runtime:** .NET 10 (SDK `10.0.103` pinned in `global.json`)
- **UI Framework:** Avalonia UI 11.3
- **Editor Component:** AvaloniaEdit
- **MVVM Framework:** CommunityToolkit.Mvvm (using source generators for properties and commands)
- **Testing:** xUnit
- **Configuration:** `mise` for tool management (optional)

## Project Structure

```text
/
├── QuickNotesTxt.sln           # Main solution file
├── src/QuickNotesTxt/          # Main application project
│   ├── Assets/                 # Fonts and AI prompt JSON files
│   ├── Converters/             # XAML value converters
│   ├── Editors/                # Markdown highlighting and editing logic
│   ├── Models/                 # Note, settings, and prompt data structures
│   ├── Services/               # Business logic (I/O, AI, Themes, Settings)
│   ├── Styles/                 # Avalonia XAML styles and theme definitions
│   ├── ViewModels/             # MVVM state and commands (MainViewModel)
│   └── Views/                  # Avalonia XAML views and code-behind
└── tests/QuickNotesTxt.Tests/  # xUnit test suite
```

## Development Workflow

### Standard Commands
```bash
# Build the entire solution
dotnet build QuickNotesTxt.sln

# Run the desktop application (requires a graphical session)
dotnet run --project src/QuickNotesTxt

# Run all tests
dotnet test QuickNotesTxt.sln

# Run a specific test class
dotnet test tests/QuickNotesTxt.Tests/QuickNotesTxt.Tests.csproj --filter "FullyQualifiedName~NotesRepositoryTests"

# Create a self-contained release build
dotnet publish src/QuickNotesTxt/QuickNotesTxt.csproj -c Release -r linux-x64 --self-contained true
```

### Note on Avalonia
If builds fail due to locked files in `obj/` or `bin/`, ensure no instance of the app or a debugger is running.

## Development Conventions

### Coding Style
- **Namespaces:** Use file-scoped namespaces (`namespace QuickNotesTxt.Services;`).
- **Indentation:** 4-space indentation.
- **Naming:** 
  - `PascalCase` for types, methods, and properties.
  - `_camelCase` for private fields.
  - `I` prefix for interfaces (e.g., `INotesRepository`).
  - `Async` suffix for asynchronous methods.
- **Immutability:** Use `sealed` for concrete classes unless inheritance is specifically required.
- **Nullability:** Nullable reference types are enabled; handle nulls explicitly and avoid the null-forgiving operator (`!`) where possible.

### Architecture (MVVM)
- **View Models:** Inherit from `ViewModelBase` (which inherits from `ObservableObject`). Use `[ObservableProperty]` and `[RelayCommand]` attributes from `CommunityToolkit.Mvvm`.
- **Services:** All business logic, I/O, and external API calls must reside in services. Use interface-based dependency injection where appropriate.
- **Views:** Code-behind should be minimal, focused on UI-only concerns like focus management, windowing, and complex input routing.

### Testing
- Always verify changes with unit tests in `tests/QuickNotesTxt.Tests/`.
- Repository tests should use temporary directories and ensure cleanup in `Dispose()`.
- Mock or use test-specific configurations for services that interact with the filesystem or network.

## Key Services & Components

- **NotesRepository:** Handles reading/writing notes, frontmatter parsing, and unique filename generation.
- **MainViewModel:** The central orchestrator. Manages note selection, editor state, and save scheduling.
- **FileWatcherService:** Uses `FileSystemWatcher` to sync the UI when files are changed outside the app.
- **OpenAiTextActionService:** Handles communication with the OpenAI API for AI-assisted editing.
- **MarkdownLineParser:** Core logic for analyzing Markdown structure for syntax highlighting.
