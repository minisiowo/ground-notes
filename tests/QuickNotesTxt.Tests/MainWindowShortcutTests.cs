using Avalonia.Input;
using QuickNotesTxt.Views;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class MainWindowShortcutTests
{
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
}
