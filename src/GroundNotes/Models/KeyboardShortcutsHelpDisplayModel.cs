namespace GroundNotes.Models;

public sealed class KeyboardShortcutsHelpDisplayModel
{


    public KeyboardShortcutsHelpDisplayModel(IReadOnlyList<KeyboardShortcutSection> sections)
    {
        Sections = sections;
    }

    public IReadOnlyList<KeyboardShortcutSection> Sections { get; }
}
