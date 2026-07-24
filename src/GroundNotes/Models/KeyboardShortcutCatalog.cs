namespace GroundNotes.Models;

public static class KeyboardShortcutCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<KeyboardShortcutBinding>> s_legacyDefaultBindings =
        new Dictionary<string, IReadOnlyList<KeyboardShortcutBinding>>(StringComparer.Ordinal)
        {
            [KeyboardShortcutActionIds.ShowShortcuts] =
            [
                Direct("OemQuestion", control: true, shift: true),
                Direct("Oem2", control: true, shift: true),
                Direct("OemQuestion", shift: true, meta: true),
                Direct("Oem2", shift: true, meta: true)
            ],
            [KeyboardShortcutActionIds.ToggleYaml] = [Direct("Y", control: true, shift: true), Direct("Y", shift: true, meta: true)],
            [KeyboardShortcutActionIds.GenerateTitleSuggestions] = [Direct("Enter", control: true), Direct("Enter", meta: true)],
            [KeyboardShortcutActionIds.ChatSend] = [Direct("Enter", control: true), Direct("Enter", meta: true)]
        };

    public static IReadOnlyList<KeyboardShortcutDefinition> Definitions { get; } = CreateDefinitions(OperatingSystem.IsMacOS());

    public static KeyboardShortcutDefinition? Find(string actionId)
    {
        return Definitions.FirstOrDefault(definition => string.Equals(definition.Id, actionId, StringComparison.Ordinal));
    }

    public static IReadOnlyList<KeyboardShortcutBinding> NormalizeBindings(
        IEnumerable<KeyboardShortcutBinding> bindings)
    {
        return bindings
            .Where(binding => !string.IsNullOrWhiteSpace(binding.Key))
            .Select(Canonicalize)
            .Distinct()
            .ToList();
    }

    internal static IReadOnlyList<KeyboardShortcutBinding> NormalizeLegacyBindings(
        KeyboardShortcutDefinition definition,
        IReadOnlyList<KeyboardShortcutBinding> configured,
        KeyboardShortcutBinding applicationModifier)
    {
        if (MatchesLegacyDefaults(definition, configured, applicationModifier))
        {
            return GetMigratedDefaultBindings(definition, applicationModifier);
        }

        return NormalizeBindings(configured);
    }

    internal static bool IsCompleteLegacyDefaultConfiguration(
        IReadOnlyDictionary<string, List<KeyboardShortcutBinding>> configuredBindings,
        KeyboardShortcutBinding applicationModifier)
    {
        return Definitions
            .Where(definition => definition.Id is not KeyboardShortcutActionIds.ToggleSidebar
                                      and not KeyboardShortcutActionIds.ToggleCodeBlock)
            .All(definition => configuredBindings.TryGetValue(definition.Id, out var configured)
                               && MatchesLegacyDefaults(definition, configured, applicationModifier));
    }

    public static string CanonicalizeKey(string key)
    {
        return string.Equals(key, "Oem2", StringComparison.OrdinalIgnoreCase) ? "OemQuestion" : key;
    }

    internal static IReadOnlyList<KeyboardShortcutDefinition> CreateDefinitions(bool isMacOS)
    {
        return
        [
            Define(KeyboardShortcutActionIds.OpenSettings, "Open settings", "General", KeyboardShortcutScope.MainWindow, Primary("OemComma", isMacOS)),
            Define(KeyboardShortcutActionIds.ShowShortcuts, "Show keyboard shortcuts", "General", KeyboardShortcutScope.Global, Direct("F1")),
            Define(KeyboardShortcutActionIds.ToggleYaml, "Toggle YAML front matter", "General", KeyboardShortcutScope.MainWindow, Primary("Y", isMacOS, shift: true)),
            Define(KeyboardShortcutActionIds.ToggleSidebar, "Toggle sidebar", "General", KeyboardShortcutScope.MainWindow, Direct("B", control: true, shift: true)),
            Define(KeyboardShortcutActionIds.ReloadNotes, "Reload notes", "Notes", KeyboardShortcutScope.MainWindow, Primary("R", isMacOS)),
            Define(KeyboardShortcutActionIds.NewNote, "New note", "Notes", KeyboardShortcutScope.MainWindow, Primary("N", isMacOS)),
            Define(KeyboardShortcutActionIds.OpenNotePicker, "Open note picker", "Notes", KeyboardShortcutScope.MainWindow, Primary("P", isMacOS)),
            Define(KeyboardShortcutActionIds.DeleteNote, "Delete current note", "Notes", KeyboardShortcutScope.MainWindow, Direct("D", control: true, shift: true)),
            Define(KeyboardShortcutActionIds.ClosePane, "Close active pane", "Editor", KeyboardShortcutScope.MainWindow, Primary("W", isMacOS)),
            Define(KeyboardShortcutActionIds.EqualizePanes, "Equalize pane widths", "Editor", KeyboardShortcutScope.MainWindow, Primary("D0", isMacOS)),
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
            Define(KeyboardShortcutActionIds.GenerateTitleSuggestions, "Generate title suggestions", "AI", KeyboardShortcutScope.TitleSuggestions, Primary("Enter", isMacOS)),
            Define(KeyboardShortcutActionIds.ChatSend, "Send message", "AI chat", KeyboardShortcutScope.Chat, Primary("Enter", isMacOS)),
            Define(KeyboardShortcutActionIds.ChatSave, "Save conversation as note", "AI chat", KeyboardShortcutScope.Chat, Direct("S", control: true))
        ];
    }

    private static IReadOnlyList<KeyboardShortcutBinding> GetMigratedDefaultBindings(
        KeyboardShortcutDefinition definition,
        KeyboardShortcutBinding applicationModifier)
    {
        return definition.Id switch
        {
            KeyboardShortcutActionIds.OpenSettings => [WithApplicationModifier("OemComma", applicationModifier)],
            KeyboardShortcutActionIds.ReloadNotes => [WithApplicationModifier("R", applicationModifier)],
            KeyboardShortcutActionIds.NewNote => [WithApplicationModifier("N", applicationModifier)],
            KeyboardShortcutActionIds.OpenNotePicker => [WithApplicationModifier("P", applicationModifier)],
            KeyboardShortcutActionIds.ClosePane => [WithApplicationModifier("W", applicationModifier)],
            KeyboardShortcutActionIds.EqualizePanes => [WithApplicationModifier("D0", applicationModifier)],
            _ => definition.DefaultBindings.ToList()
        };
    }

    private static bool MatchesLegacyDefaults(
        KeyboardShortcutDefinition definition,
        IReadOnlyList<KeyboardShortcutBinding> configured,
        KeyboardShortcutBinding applicationModifier)
    {
        var previous = GetPreviousDefaultBindings(definition.Id, applicationModifier);
        if (previous is not null && BindingsEqual(configured, previous))
        {
            return true;
        }

        var legacy = GetLegacyDefaultBindings(definition, applicationModifier);
        return BindingsEqual(configured, legacy);
    }

    private static IReadOnlyList<KeyboardShortcutBinding> GetLegacyDefaultBindings(
        KeyboardShortcutDefinition definition,
        KeyboardShortcutBinding applicationModifier)
    {
        return definition.Id switch
        {
            KeyboardShortcutActionIds.OpenSettings =>
            [WithApplicationModifier("OemComma", applicationModifier), Direct("OemComma", meta: true)],
            KeyboardShortcutActionIds.ClosePane =>
            [WithApplicationModifier("W", applicationModifier), Direct("W", meta: true)],
            KeyboardShortcutActionIds.EqualizePanes =>
            [
                WithApplicationModifier("D0", applicationModifier),
                WithApplicationModifier("NumPad0", applicationModifier),
                Direct("D0", meta: true),
                Direct("NumPad0", meta: true)
            ],
            _ when s_legacyDefaultBindings.TryGetValue(definition.Id, out var legacy) => legacy,
            KeyboardShortcutActionIds.ReloadNotes => [WithApplicationModifier("R", applicationModifier)],
            KeyboardShortcutActionIds.NewNote => [WithApplicationModifier("N", applicationModifier)],
            KeyboardShortcutActionIds.OpenNotePicker => [WithApplicationModifier("P", applicationModifier)],
            _ => definition.DefaultBindings
        };
    }

    private static IReadOnlyList<KeyboardShortcutBinding>? GetPreviousDefaultBindings(
        string actionId,
        KeyboardShortcutBinding applicationModifier)
    {
        return actionId switch
        {
            KeyboardShortcutActionIds.ShowShortcuts => [Primary("OemQuestion", OperatingSystem.IsMacOS(), shift: true)],
            KeyboardShortcutActionIds.ToggleSidebar => [WithApplicationModifier("L", applicationModifier)],
            KeyboardShortcutActionIds.OpenNotePicker => [WithApplicationModifier("O", applicationModifier)],
            KeyboardShortcutActionIds.DeleteNote => [WithApplicationModifier("D", applicationModifier)],
            KeyboardShortcutActionIds.MoveLineUp => [Direct("Up", control: true, shift: true)],
            KeyboardShortcutActionIds.MoveLineDown => [Direct("Down", control: true, shift: true)],
            KeyboardShortcutActionIds.DeleteLine => [Direct("D", control: true, shift: true)],
            KeyboardShortcutActionIds.ToggleTaskList => [Direct("D7", control: true, shift: true)],
            _ => null
        };
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

    private static KeyboardShortcutBinding Primary(
        string key,
        bool isMacOS,
        bool shift = false,
        bool alt = false)
    {
        return isMacOS
            ? Direct(key, shift: shift, alt: alt, meta: true)
            : Direct(key, control: true, shift: shift, alt: alt);
    }

    private static KeyboardShortcutBinding WithApplicationModifier(
        string key,
        KeyboardShortcutBinding applicationModifier)
    {
        return applicationModifier with { Key = key };
    }

    private static KeyboardShortcutBinding Direct(
        string key,
        bool control = false,
        bool shift = false,
        bool alt = false,
        bool meta = false)
    {
        return new KeyboardShortcutBinding(key, control, shift, alt, meta);
    }
}
