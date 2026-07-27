namespace GroundNotes.Services.KeySequences;

[Flags]
public enum KeyStrokeModifiers
{
    None = 0,
    Control = 1 << 0,
    Shift = 1 << 1,
    Alt = 1 << 2,
    Meta = 1 << 3
}

/// <summary>
/// A UI-independent logical key together with its modifiers.
/// </summary>
public readonly record struct KeyStroke
{
    private const KeyStrokeModifiers AllModifiers =
        KeyStrokeModifiers.Control |
        KeyStrokeModifiers.Shift |
        KeyStrokeModifiers.Alt |
        KeyStrokeModifiers.Meta;

    public KeyStroke(string key, KeyStrokeModifiers modifiers = KeyStrokeModifiers.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if ((modifiers & ~AllModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers), modifiers, "Unsupported key modifiers.");
        }

        Key = key.Trim().ToLowerInvariant();
        Modifiers = modifiers;
    }

    public string Key { get; }

    public KeyStrokeModifiers Modifiers { get; }

    public bool IsValid => !string.IsNullOrEmpty(Key);

    public string Display
    {
        get
        {
            var parts = new List<string>(5);
            if (Modifiers.HasFlag(KeyStrokeModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(KeyStrokeModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(KeyStrokeModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(KeyStrokeModifiers.Meta))
            {
                parts.Add("Meta");
            }

            parts.Add(FormatKey(Key));
            return string.Join('+', parts);
        }
    }

    public static KeyStroke FromCharacter(
        char character,
        KeyStrokeModifiers modifiers = KeyStrokeModifiers.None)
    {
        return new KeyStroke(character == ' ' ? "space" : character.ToString(), modifiers);
    }

    public override string ToString()
    {
        return Display;
    }

    private static string FormatKey(string key)
    {
        return key switch
        {
            "space" => "Space",
            "backspace" => "Backspace",
            "escape" => "Esc",
            "return" => "Enter",
            _ when key.Length == 1 => key,
            _ when key.Length > 1 => char.ToUpperInvariant(key[0]) + key[1..],
            _ => string.Empty
        };
    }
}
