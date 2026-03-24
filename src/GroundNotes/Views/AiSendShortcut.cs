using Avalonia.Input;

namespace GroundNotes.Views;

internal static class AiSendShortcut
{
    public static bool IsSendGesture(Key key, KeyModifiers modifiers)
    {
        if (key != Key.Enter)
        {
            return false;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift) || modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        return modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
    }
}
