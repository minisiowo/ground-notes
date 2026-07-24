namespace GroundNotes.Models;

public sealed record KeyboardShortcutBinding(
    string Key,
    bool Control = false,
    bool Shift = false,
    bool Alt = false,
    bool Meta = false);
