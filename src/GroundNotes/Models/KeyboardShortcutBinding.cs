namespace GroundNotes.Models;

public sealed record KeyboardShortcutBinding(
    KeyboardShortcutBindingKind Kind,
    string Key,
    bool Control = false,
    bool Shift = false,
    bool Alt = false,
    bool Meta = false);
