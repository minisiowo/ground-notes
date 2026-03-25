using Avalonia.Input;

namespace GroundNotes.Views;

internal static class InputGestureHelper
{
    public static bool IsOpenSettingsGesture(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt) || modifiers.HasFlag(KeyModifiers.Shift))
        {
            return false;
        }

        if (!modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Meta))
        {
            return false;
        }

        return key == Key.OemComma;
    }

    public static bool IsShowShortcutsHelpGesture(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        if (!modifiers.HasFlag(KeyModifiers.Shift))
        {
            return false;
        }

        if (!modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Meta))
        {
            return false;
        }

        return key is Key.Oem2 or Key.OemQuestion;
    }

    public static bool IsRenameTextBoxSubmitKey(Key key) => key == Key.Enter;

    public static bool IsRenameTextBoxCancelKey(Key key) => key == Key.Escape;

    public static bool IsUndoShortcut(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt) || modifiers.HasFlag(KeyModifiers.Shift))
        {
            return false;
        }

        if (!modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Meta))
        {
            return false;
        }

        return key == Key.Z;
    }

    public static bool IsRedoShortcut(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        if (key == Key.Y)
        {
            return modifiers == KeyModifiers.Control;
        }

        if (key != Key.Z || !modifiers.HasFlag(KeyModifiers.Shift))
        {
            return false;
        }

        return modifiers == (KeyModifiers.Control | KeyModifiers.Shift)
            || modifiers == (KeyModifiers.Meta | KeyModifiers.Shift);
    }

    public static bool IsToggleYamlEditorShortcut(Key key, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        return key == Key.Y
            && (modifiers == (KeyModifiers.Control | KeyModifiers.Shift)
                || modifiers == (KeyModifiers.Meta | KeyModifiers.Shift));
    }

    public static bool IsToggleTaskShortcut(Key key, KeyModifiers modifiers)
    {
        return key == Key.Enter
            && modifiers.HasFlag(KeyModifiers.Control)
            && !modifiers.HasFlag(KeyModifiers.Shift)
            && !modifiers.HasFlag(KeyModifiers.Alt);
    }

    public static bool IsMoveLineShortcut(Key key, KeyModifiers modifiers, out bool moveDown)
    {
        moveDown = false;

        if (!modifiers.HasFlag(KeyModifiers.Control)
            || !modifiers.HasFlag(KeyModifiers.Shift)
            || modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        if (key == Key.Up)
        {
            return true;
        }

        if (key == Key.Down)
        {
            moveDown = true;
            return true;
        }

        return false;
    }
}
