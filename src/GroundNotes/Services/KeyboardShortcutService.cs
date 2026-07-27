using Avalonia.Input;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class KeyboardShortcutService : IKeyboardShortcutService
{
    private KeyboardShortcutSettings _settings = KeyboardShortcutSettings.CreateDefault();

    public KeyboardShortcutSettings Settings => KeyboardShortcutSettings.Normalize(_settings);

    public void ApplySettings(KeyboardShortcutSettings settings)
    {
        _settings = KeyboardShortcutSettings.Normalize(settings);
    }

    public bool Matches(string actionId, Key key, KeyModifiers modifiers)
    {
        return GetBindings(actionId).Any(binding => Matches(binding, key, modifiers));
    }

    public IReadOnlyList<KeyboardShortcutBinding> GetBindings(string actionId)
    {
        return _settings.Bindings.TryGetValue(actionId, out var bindings)
            ? bindings
            : [];
    }

    public IReadOnlyList<KeyboardShortcutSection> BuildHelpSections()
    {
        var configurableSections = KeyboardShortcutCatalog.Definitions
            .GroupBy(definition => definition.Category)
            .Select(group => new KeyboardShortcutSection(
                group.Key,
                group.Select(definition => new KeyboardShortcutEntry(
                        definition.Name,
                        string.Join(" or ", GetBindings(definition.Id).Select(Format))))
                    .ToList()))
            .ToList();

        configurableSections.Add(new KeyboardShortcutSection(
            "Vim mode (when enabled)",
            [
                new KeyboardShortcutEntry("Normal / Insert", "Escape / i, a, I, A, o, O"),
                new KeyboardShortcutEntry("Move", "h, j, k, l, w, b, e, 0, ^, $, gg, G"),
                new KeyboardShortcutEntry("Visual / Visual Line", "v / V"),
                new KeyboardShortcutEntry("Delete / change / yank", "d / c / y + motion, dd / cc / yy"),
                new KeyboardShortcutEntry("Text objects", "iw / aw"),
                new KeyboardShortcutEntry("Paste / undo / redo", "p, P / u / Ctrl+R"),
                new KeyboardShortcutEntry("Find note", "Space f f"),
                new KeyboardShortcutEntry("Pane commands", "Space w ... or Ctrl+W ..."),
                new KeyboardShortcutEntry("Cycle focus areas", "F6 / Shift+F6")
            ]));

        configurableSections.Add(new KeyboardShortcutSection(
            "Fixed navigation and editing",
            [
                new KeyboardShortcutEntry("Undo / redo", "Ctrl+Z / Ctrl+Y"),
                new KeyboardShortcutEntry("Copy / cut / paste", "Ctrl+C / Ctrl+X / Ctrl+V"),
                new KeyboardShortcutEntry("Indent / outdent", "Tab / Shift+Tab"),
                new KeyboardShortcutEntry("Close popup or dialog", "Escape"),
                new KeyboardShortcutEntry("Accept popup selection", "Enter"),
                new KeyboardShortcutEntry("Move popup selection", "Up / Down")
            ]));

        return configurableSections;
    }

    public string Format(KeyboardShortcutBinding binding)
    {
        var modifiers = GetModifiers(binding);
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(FormatKey(binding.Key));
        return string.Join('+', parts);
    }

    private static bool Matches(KeyboardShortcutBinding binding, Key key, KeyModifiers modifiers)
    {
        if (!Enum.TryParse<Key>(binding.Key, ignoreCase: true, out var expectedKey))
        {
            return false;
        }

        return expectedKey == key && GetModifiers(binding) == NormalizeModifiers(modifiers);
    }

    private static KeyModifiers GetModifiers(KeyboardShortcutBinding binding)
    {
        var modifiers = KeyModifiers.None;
        if (binding.Control)
        {
            modifiers |= KeyModifiers.Control;
        }

        if (binding.Shift)
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (binding.Alt)
        {
            modifiers |= KeyModifiers.Alt;
        }

        if (binding.Meta)
        {
            modifiers |= KeyModifiers.Meta;
        }

        return modifiers;
    }

    private static KeyModifiers NormalizeModifiers(KeyModifiers modifiers)
    {
        return modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Meta);
    }

    private static string FormatKey(string key)
    {
        return KeyboardShortcutCatalog.CanonicalizeKey(key) switch
        {
            "OemComma" => ",",
            "OemQuestion" => "?",
            "D0" => "0",
            "D1" => "1",
            "D2" => "2",
            "D3" => "3",
            "D7" => "7",
            "D8" => "8",
            "NumPad0" => "Num 0",
            _ => key
        };
    }
}
