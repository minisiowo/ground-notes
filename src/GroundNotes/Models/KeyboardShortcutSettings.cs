namespace GroundNotes.Models;

public sealed record KeyboardShortcutSettings(
    Dictionary<string, List<KeyboardShortcutBinding>> Bindings)
{
    public static KeyboardShortcutSettings CreateDefault()
    {
        return new KeyboardShortcutSettings(
            KeyboardShortcutCatalog.Definitions.ToDictionary(
                definition => definition.Id,
                definition => definition.DefaultBindings.ToList(),
                StringComparer.Ordinal));
    }

    public bool Equals(KeyboardShortcutSettings? other)
    {
        if (other is null || Bindings.Count != other.Bindings.Count)
        {
            return false;
        }

        return Bindings.All(pair =>
            other.Bindings.TryGetValue(pair.Key, out var otherBindings)
            && pair.Value.SequenceEqual(otherBindings));
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var pair in Bindings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            foreach (var binding in pair.Value)
            {
                hash.Add(binding);
            }
        }

        return hash.ToHashCode();
    }

    public static KeyboardShortcutSettings Normalize(KeyboardShortcutSettings? settings)
    {
        var defaults = CreateDefault();
        if (settings is null)
        {
            return defaults;
        }

        var configuredBindings = settings.Bindings ?? new Dictionary<string, List<KeyboardShortcutBinding>>(StringComparer.Ordinal);
        var bindings = new Dictionary<string, List<KeyboardShortcutBinding>>(StringComparer.Ordinal);
        foreach (var definition in KeyboardShortcutCatalog.Definitions)
        {
            if (configuredBindings.TryGetValue(definition.Id, out var configured))
            {
                bindings[definition.Id] = KeyboardShortcutCatalog.NormalizeBindings(configured).ToList();
                continue;
            }

            var requiresConflictSafeDefault = definition.Id is KeyboardShortcutActionIds.ToggleZenMode
                or KeyboardShortcutActionIds.NewNoteWindow;
            bindings[definition.Id] = requiresConflictSafeDefault
                                      && KeyboardShortcutCatalog.HasConfiguredConflict(
                                          definition,
                                          definition.DefaultBindings,
                                          configuredBindings)
                ? []
                : definition.DefaultBindings.ToList();
        }

        return new KeyboardShortcutSettings(bindings);
    }
}
