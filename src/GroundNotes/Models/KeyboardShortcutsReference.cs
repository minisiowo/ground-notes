namespace GroundNotes.Models;

/// <summary>
/// Human-readable shortcut list for the help window. Keep in sync with key handlers in views and view models.
/// </summary>
public static class KeyboardShortcutsReference
{
    public static IReadOnlyList<KeyboardShortcutSection> Sections { get; } =
    [
        new KeyboardShortcutSection(
            "Main window",
            [
                new("Open settings", "Ctrl+,"),
                new("Show keyboard shortcuts", "Ctrl+Shift+?"),
                new("Toggle YAML front matter panel", "Ctrl+Shift+Y"),
                new("Increase editor font size", "Ctrl+Plus or Ctrl+="),
                new("Decrease editor font size", "Ctrl+Minus"),
                new("Increase UI font size", "Ctrl+Shift+Plus"),
                new("Decrease UI font size", "Ctrl+Shift+Minus"),
                new("Reload notes from disk", "Ctrl+R"),
                new("New note", "Ctrl+N"),
                new("Open note picker", "Ctrl+O"),
                new("Close active pane", "Ctrl+W"),
                new("Delete current note", "Ctrl+D"),
            ]),
        new KeyboardShortcutSection(
            "Editor (Markdown)",
            [
                new("Undo", "Ctrl+Z"),
                new("Redo", "Ctrl+Y or Ctrl+Shift+Z"),
                new("Toggle task line or insert line below", "Ctrl+Enter"),
                new("Delete current note", "Ctrl+D"),
                new("Copy selection", "Ctrl+C"),
                new("Cut selection", "Ctrl+X"),
                new("Bold", "Ctrl+B"),
                new("Italic", "Ctrl+I"),
                new("Inline code", "Ctrl+K"),
                new("Move line up / down", "Ctrl+Shift+Up / Ctrl+Shift+Down"),
                new("Delete current line", "Ctrl+Shift+D"),
                new("Toggle task list", "Ctrl+Shift+7"),
                new("Toggle bullet list", "Ctrl+Shift+8"),
                new("Heading 1 / 2 / 3", "Ctrl+Alt+1 / Ctrl+Alt+2 / Ctrl+Alt+3"),
                new("Indent / outdent", "Tab / Shift+Tab"),
            ]),
        new KeyboardShortcutSection(
            "Note picker (Open note)",
            [
                new("Move selection", "Up / Down"),
                new("Open selected note", "Enter"),
                new("Close picker", "Escape"),
            ]),
        new KeyboardShortcutSection(
            "Slash commands (when popup is open)",
            [
                new("Move selection", "Up / Down"),
                new("Insert command", "Enter"),
                new("Close popup", "Escape"),
            ]),
        new KeyboardShortcutSection(
            "AI chat",
            [
                new("Send message", "Ctrl+Enter"),
                new("Save conversation as note", "Ctrl+S"),
                new("Mention popup: move selection", "Up / Down"),
                new("Mention popup: accept", "Enter"),
                new("Mention popup: dismiss", "Escape"),
            ]),
        new KeyboardShortcutSection(
            "Settings",
            [
                new("Save and close", "Enter"),
                new("Cancel", "Escape"),
            ]),
    ];
}
