namespace GroundNotes.Models;

public static class KeyboardShortcutCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<KeyboardShortcutBinding>> s_legacyDefaultBindings =
        new Dictionary<string, IReadOnlyList<KeyboardShortcutBinding>>(StringComparer.Ordinal)
        {
            [KeyboardShortcutActionIds.OpenSettings] = [App("OemComma"), Direct("OemComma", meta: true)],
            [KeyboardShortcutActionIds.ShowShortcuts] =
            [
                Direct("OemQuestion", control: true, shift: true),
                Direct("Oem2", control: true, shift: true),
                Direct("OemQuestion", shift: true, meta: true),
                Direct("Oem2", shift: true, meta: true)
            ],
            [KeyboardShortcutActionIds.ToggleYaml] = [Direct("Y", control: true, shift: true), Direct("Y", shift: true, meta: true)],
            [KeyboardShortcutActionIds.ClosePane] = [App("W"), Direct("W", meta: true)],
            [KeyboardShortcutActionIds.EqualizePanes] = [App("D0"), App("NumPad0"), Direct("D0", meta: true), Direct("NumPad0", meta: true)],
            [KeyboardShortcutActionIds.GenerateTitleSuggestions] = [Direct("Enter", control: true), Direct("Enter", meta: true)],
            [KeyboardShortcutActionIds.ChatSend] = [Direct("Enter", control: true), Direct("Enter", meta: true)]
        };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<KeyboardShortcutBinding>> s_previousDefaultBindings =
        new Dictionary<string, IReadOnlyList<KeyboardShortcutBinding>>(StringComparer.Ordinal)
        {
            [KeyboardShortcutActionIds.ShowShortcuts] = [Primary("OemQuestion", shift: true)],
            [KeyboardShortcutActionIds.ToggleSidebar] = [App("L")],
            [KeyboardShortcutActionIds.OpenNotePicker] = [App("O")],
            [KeyboardShortcutActionIds.DeleteNote] = [App("D")],
            [KeyboardShortcutActionIds.MoveLineUp] = [Direct("Up", control: true, shift: true)],
            [KeyboardShortcutActionIds.MoveLineDown] = [Direct("Down", control: true, shift: true)],
            [KeyboardShortcutActionIds.DeleteLine] = [Direct("D", control: true, shift: true)],
            [KeyboardShortcutActionIds.ToggleTaskList] = [Direct("D7", control: true, shift: true)]
        };

    public static IReadOnlyList<KeyboardShortcutDefinition> Definitions { get; } =
    [
        Define(KeyboardShortcutActionIds.OpenSettings, "Open settings", "General", KeyboardShortcutScope.MainWindow, App("OemComma")),
        Define(KeyboardShortcutActionIds.ShowShortcuts, "Show keyboard shortcuts", "General", KeyboardShortcutScope.Global, Direct("F1")),
        Define(KeyboardShortcutActionIds.ToggleYaml, "Toggle YAML front matter", "General", KeyboardShortcutScope.MainWindow, Primary("Y", shift: true)),
        Define(KeyboardShortcutActionIds.ToggleSidebar, "Toggle sidebar", "General", KeyboardShortcutScope.MainWindow, Direct("B", control: true, shift: true)),
        Define(KeyboardShortcutActionIds.ReloadNotes, "Reload notes", "Notes", KeyboardShortcutScope.MainWindow, App("R")),
        Define(KeyboardShortcutActionIds.NewNote, "New note", "Notes", KeyboardShortcutScope.MainWindow, App("N")),
        Define(KeyboardShortcutActionIds.OpenNotePicker, "Open note picker", "Notes", KeyboardShortcutScope.MainWindow, App("P")),
        Define(KeyboardShortcutActionIds.DeleteNote, "Delete current note", "Notes", KeyboardShortcutScope.MainWindow, Direct("D", control: true, shift: true)),
        Define(KeyboardShortcutActionIds.ClosePane, "Close active pane", "Editor", KeyboardShortcutScope.MainWindow, App("W")),
        Define(KeyboardShortcutActionIds.EqualizePanes, "Equalize pane widths", "Editor", KeyboardShortcutScope.MainWindow, App("D0")),
        Define(KeyboardShortcutActionIds.ToggleTaskState, "Toggle task state or insert line below", "Editor", KeyboardShortcutScope.Editor, Direct("Enter", control: true)),
        Define(KeyboardShortcutActionIds.Bold, "Bold", "Editor", KeyboardShortcutScope.Editor, Direct("B", control: true)),
        Define(KeyboardShortcutActionIds.Italic, "Italic", "Editor", KeyboardShortcutScope.Editor, Direct("I", control: true)),
        Define(KeyboardShortcutActionIds.InlineCode, "Inline code", "Editor", KeyboardShortcutScope.Editor, Direct("K", control: true)),
        Define(KeyboardShortcutActionIds.ToggleCodeBlock, "Toggle code block", "Editor", KeyboardShortcutScope.Editor, Direct("K", control: true, shift: true)),
        Define(KeyboardShortcutActionIds.MoveLineUp, "Move line up", "Editor", KeyboardShortcutScope.Editor, Direct("Up", alt: true)),
        Define(KeyboardShortcutActionIds.MoveLineDown, "Move line down", "Editor", KeyboardShortcutScope.Editor, Direct("Down", alt: true)),
        Define(KeyboardShortcutActionIds.DeleteLine, "Delete current line", "Editor", KeyboardShortcutScope.Editor, Direct("D", control: true)),
        Define(KeyboardShortcutActionIds.ToggleTaskList, "Toggle task list", "Editor", KeyboardShortcutScope.Editor, Direct("X", control: true, shift: true)),
        Define(KeyboardShortcutActionIds.ToggleBulletList, "Toggle bullet list", "Editor", KeyboardShortcutScope.Editor, Direct("D8", control: true, shift: true)),
        Define(KeyboardShortcutActionIds.Heading1, "Heading 1", "Editor", KeyboardShortcutScope.Editor, Direct("D1", control: true, alt: true)),
        Define(KeyboardShortcutActionIds.Heading2, "Heading 2", "Editor", KeyboardShortcutScope.Editor, Direct("D2", control: true, alt: true)),
        Define(KeyboardShortcutActionIds.Heading3, "Heading 3", "Editor", KeyboardShortcutScope.Editor, Direct("D3", control: true, alt: true)),
        Define(KeyboardShortcutActionIds.GenerateTitleSuggestions, "Generate title suggestions", "AI", KeyboardShortcutScope.TitleSuggestions, Primary("Enter")),
        Define(KeyboardShortcutActionIds.ChatSend, "Send message", "AI chat", KeyboardShortcutScope.Chat, Primary("Enter")),
        Define(KeyboardShortcutActionIds.ChatSave, "Save conversation as note", "AI chat", KeyboardShortcutScope.Chat, Direct("S", control: true))
    ];

    public static KeyboardShortcutDefinition? Find(string actionId)
    {
        return Definitions.FirstOrDefault(definition => string.Equals(definition.Id, actionId, StringComparison.Ordinal));
    }

    public static IReadOnlyList<KeyboardShortcutBinding> NormalizeBindings(
        KeyboardShortcutDefinition definition,
        IEnumerable<KeyboardShortcutBinding> bindings)
    {
        var configured = bindings.ToList();
        if (MatchesLegacyDefaults(definition, configured))
        {
            return definition.DefaultBindings.ToList();
        }

        return configured
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Key))
            .Select(Canonicalize)
            .Distinct()
            .ToList();
    }

    public static bool IsCompleteLegacyDefaultConfiguration(
        IReadOnlyDictionary<string, List<KeyboardShortcutBinding>> configuredBindings)
    {
        return Definitions
            .Where(definition => definition.Id is not KeyboardShortcutActionIds.ToggleSidebar
                                      and not KeyboardShortcutActionIds.ToggleCodeBlock)
            .All(definition => configuredBindings.TryGetValue(definition.Id, out var configured)
                               && MatchesLegacyDefaults(definition, configured));
    }

    public static string CanonicalizeKey(string key)
    {
        return string.Equals(key, "Oem2", StringComparison.OrdinalIgnoreCase) ? "OemQuestion" : key;
    }

    private static bool MatchesLegacyDefaults(
        KeyboardShortcutDefinition definition,
        IReadOnlyList<KeyboardShortcutBinding> configured)
    {
        if (s_previousDefaultBindings.TryGetValue(definition.Id, out var previousBindings)
            && BindingsEqual(configured, previousBindings))
        {
            return true;
        }

        var legacy = s_legacyDefaultBindings.TryGetValue(definition.Id, out var legacyBindings)
            ? legacyBindings
            : definition.DefaultBindings;
        return BindingsEqual(configured, legacy);
    }

    private static bool BindingsEqual(
        IReadOnlyList<KeyboardShortcutBinding> configured,
        IReadOnlyList<KeyboardShortcutBinding> expected)
    {
        return configured.Count == expected.Count
               && configured.Select(Canonicalize).SequenceEqual(expected.Select(Canonicalize));
    }

    private static KeyboardShortcutBinding Canonicalize(KeyboardShortcutBinding binding)
    {
        return binding with { Key = CanonicalizeKey(binding.Key) };
    }

    private static KeyboardShortcutDefinition Define(
        string id,
        string name,
        string category,
        KeyboardShortcutScope scope,
        params KeyboardShortcutBinding[] bindings)
    {
        return new KeyboardShortcutDefinition(id, name, category, scope, bindings);
    }

    private static KeyboardShortcutBinding App(string key)
    {
        return new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Modifier, key);
    }

    private static KeyboardShortcutBinding Primary(
        string key,
        bool shift = false,
        bool alt = false)
    {
        return OperatingSystem.IsMacOS()
            ? Direct(key, shift: shift, alt: alt, meta: true)
            : Direct(key, control: true, shift: shift, alt: alt);
    }

    private static KeyboardShortcutBinding Direct(
        string key,
        bool control = false,
        bool shift = false,
        bool alt = false,
        bool meta = false)
    {
        return new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, key, control, shift, alt, meta);
    }
}
