using Avalonia.Input;
using GroundNotes.Views;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MainWindowShortcutTests
{
    [Theory]
    [InlineData(Key.Z, KeyModifiers.Control, true)]
    [InlineData(Key.Z, KeyModifiers.Meta, true)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.Y, KeyModifiers.Control, false)]
    [InlineData(Key.Space, KeyModifiers.Control, false)]
    public void IsUndoShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsUndoShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Y, KeyModifiers.Control, true)]
    [InlineData(Key.Y, KeyModifiers.Meta, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift, true)]
    [InlineData(Key.Z, KeyModifiers.Control, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, false)]
    [InlineData(Key.Z, KeyModifiers.Shift, false)]
    [InlineData(Key.Space, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void IsRedoShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsRedoShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Control, true)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.Space, KeyModifiers.Control, false)]
    public void IsToggleTaskShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsToggleTaskShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.OemComma, KeyModifiers.Control, true)]
    [InlineData(Key.OemComma, KeyModifiers.Meta, true)]
    [InlineData(Key.OemComma, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.OemComma, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.OemPeriod, KeyModifiers.Control, false)]
    [InlineData(Key.OemComma, KeyModifiers.None, false)]
    public void IsOpenSettingsGesture_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsOpenSettingsGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Oem2, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.Oem2, KeyModifiers.Meta | KeyModifiers.Shift, true)]
    [InlineData(Key.Oem2, KeyModifiers.Control, false)]
    [InlineData(Key.Oem2, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt, false)]
    [InlineData(Key.OemComma, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void IsShowShortcutsHelpGesture_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsShowShortcutsHelpGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.W, KeyModifiers.Control, true)]
    [InlineData(Key.W, KeyModifiers.Meta, true)]
    [InlineData(Key.W, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.W, KeyModifiers.Control | KeyModifiers.Alt, false)]
    [InlineData(Key.O, KeyModifiers.Control, false)]
    public void IsClosePaneGesture_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsClosePaneGesture(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Y, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(Key.Y, KeyModifiers.Meta | KeyModifiers.Shift, true)]
    [InlineData(Key.Y, KeyModifiers.Control, false)]
    [InlineData(Key.Y, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift, false)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift, false)]
    public void IsToggleYamlEditorShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = MainWindow.IsToggleYamlEditorShortcut(key, modifiers);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Up, KeyModifiers.Control | KeyModifiers.Shift, true, false)]
    [InlineData(Key.Down, KeyModifiers.Control | KeyModifiers.Shift, true, true)]
    [InlineData(Key.Up, KeyModifiers.Control, false, false)]
    [InlineData(Key.Down, KeyModifiers.Shift, false, false)]
    [InlineData(Key.Up, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt, false, false)]
    [InlineData(Key.Left, KeyModifiers.Control | KeyModifiers.Shift, false, false)]
    public void IsMoveLineShortcut_MatchesExpectedShortcut(Key key, KeyModifiers modifiers, bool expected, bool expectedMoveDown)
    {
        var result = MainWindow.IsMoveLineShortcut(key, modifiers, out var moveDown);

        Assert.Equal(expected, result);
        Assert.Equal(expectedMoveDown, moveDown);
    }

    [Theory]
    [InlineData(Key.Enter, true)]
    [InlineData(Key.Escape, false)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.Tab, false)]
    public void IsRenameTextBoxSubmitKey_MatchesExpectedKeys(Key key, bool expected)
    {
        var result = MainWindow.IsRenameTextBoxSubmitKey(key);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Escape, true)]
    [InlineData(Key.Enter, false)]
    [InlineData(Key.Space, false)]
    [InlineData(Key.Tab, false)]
    public void IsRenameTextBoxCancelKey_MatchesExpectedKeys(Key key, bool expected)
    {
        var result = MainWindow.IsRenameTextBoxCancelKey(key);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.Control, true)]
    [InlineData(Key.Enter, KeyModifiers.Meta, true)]
    [InlineData(Key.Enter, KeyModifiers.Control | KeyModifiers.Shift, false)]
    [InlineData(Key.Enter, KeyModifiers.Alt, false)]
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.Tab, KeyModifiers.Control, false)]
    public void IsAiSendGesture_MatchesExpectedKeys(Key key, KeyModifiers modifiers, bool expected)
    {
        var result = AiSendShortcut.IsSendGesture(key, modifiers);

        Assert.Equal(expected, result);
    }
}
