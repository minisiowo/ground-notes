namespace GroundNotes.Models;

public sealed class KeyboardShortcutsHelpDisplayModel
{
    public IReadOnlyList<KeyboardShortcutSection> Sections { get; } = KeyboardShortcutsReference.Sections;
}
