namespace GroundNotes.Models;

public sealed record KeyboardShortcutSection(string Title, IReadOnlyList<KeyboardShortcutEntry> Entries);
