using Avalonia.Input;
using GroundNotes.Models;

namespace GroundNotes.Services;

public interface IKeyboardShortcutService
{
    KeyboardShortcutSettings Settings { get; }

    void ApplySettings(KeyboardShortcutSettings settings);

    bool Matches(string actionId, Key key, KeyModifiers modifiers);

    IReadOnlyList<KeyboardShortcutBinding> GetBindings(string actionId);

    string Format(KeyboardShortcutBinding binding);

    IReadOnlyList<KeyboardShortcutSection> BuildHelpSections();
}
