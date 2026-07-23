namespace GroundNotes.Models;

public sealed record KeyboardShortcutSettings(
    ApplicationShortcutModifier ApplicationModifier,
    Dictionary<string, List<KeyboardShortcutBinding>> Bindings)
{
    public static KeyboardShortcutSettings CreateDefault()
    {
        return new KeyboardShortcutSettings(
            OperatingSystem.IsMacOS() ? ApplicationShortcutModifier.Meta : ApplicationShortcutModifier.Control,
            KeyboardShortcutCatalog.Definitions.ToDictionary(
                definition => definition.Id,
                definition => definition.DefaultBindings.ToList(),
                StringComparer.Ordinal));
    }

    public bool Equals(KeyboardShortcutSettings? other)
    {
        if (other is null || ApplicationModifier != other.ApplicationModifier || Bindings.Count != other.Bindings.Count)
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
        hash.Add(ApplicationModifier);
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
        var usesCompleteLegacyDefaults = KeyboardShortcutCatalog.IsCompleteLegacyDefaultConfiguration(configuredBindings);
        var bindings = new Dictionary<string, List<KeyboardShortcutBinding>>(StringComparer.Ordinal);
        foreach (var definition in KeyboardShortcutCatalog.Definitions)
        {
            bindings[definition.Id] = configuredBindings.TryGetValue(definition.Id, out var configured)
                ? KeyboardShortcutCatalog.NormalizeBindings(definition, configured).ToList()
                : definition.DefaultBindings.ToList();
        }

        var modifier = OperatingSystem.IsMacOS()
                       && usesCompleteLegacyDefaults
                       && settings.ApplicationModifier == ApplicationShortcutModifier.Control
            ? ApplicationShortcutModifier.Meta
            : settings.ApplicationModifier;
        return new KeyboardShortcutSettings(modifier, bindings);
    }
}
