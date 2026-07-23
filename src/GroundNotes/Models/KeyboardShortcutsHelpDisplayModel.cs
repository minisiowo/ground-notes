namespace GroundNotes.Models;

public sealed class KeyboardShortcutsHelpDisplayModel
{
    public KeyboardShortcutsHelpDisplayModel()
        : this(KeyboardShortcutsReference.Sections)
    {
    }

    public KeyboardShortcutsHelpDisplayModel(IReadOnlyList<KeyboardShortcutSection> sections)
    {
        Sections = sections;
    }

    public IReadOnlyList<KeyboardShortcutSection> Sections { get; }
}
