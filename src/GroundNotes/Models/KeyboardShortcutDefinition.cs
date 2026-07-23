namespace GroundNotes.Models;

public sealed record KeyboardShortcutDefinition(
    string Id,
    string Name,
    string Category,
    KeyboardShortcutScope Scope,
    IReadOnlyList<KeyboardShortcutBinding> DefaultBindings);
