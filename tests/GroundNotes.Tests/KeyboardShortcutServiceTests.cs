using Avalonia.Input;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class KeyboardShortcutServiceTests
{
    [Fact]
    public void Matches_ExplicitBindingUsesConfiguredModifiers()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.NewNote] =
        [
            new KeyboardShortcutBinding("N", Alt: true)
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.NewNote, Key.N, KeyModifiers.Alt));
        Assert.False(service.Matches(KeyboardShortcutActionIds.NewNote, Key.N, KeyModifiers.Control));
    }

    [Fact]
    public void Matches_DefaultsUseCurrentPlatformModifierWithoutAlternatives()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);
        var primaryModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        Assert.Single(settings.Bindings[KeyboardShortcutActionIds.OpenSettings]);
        Assert.Single(settings.Bindings[KeyboardShortcutActionIds.ToggleYaml]);
        Assert.Single(settings.Bindings[KeyboardShortcutActionIds.ShowShortcuts]);
        Assert.True(service.Matches(KeyboardShortcutActionIds.OpenSettings, Key.OemComma, primaryModifier));
        Assert.True(service.Matches(KeyboardShortcutActionIds.ToggleYaml, Key.Y, primaryModifier | KeyModifiers.Shift));
        Assert.True(service.Matches(KeyboardShortcutActionIds.ShowShortcuts, Key.F1, KeyModifiers.None));
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void CreateDefinitions_UsesPlatformModifierForPrimaryShortcuts(
        bool isMacOS,
        bool expectedControl,
        bool expectedMeta)
    {
        var definitions = KeyboardShortcutCatalog.CreateDefinitions(isMacOS);
        var newNote = definitions.Single(definition => definition.Id == KeyboardShortcutActionIds.NewNote);
        var bold = definitions.Single(definition => definition.Id == KeyboardShortcutActionIds.Bold);

        var primary = Assert.Single(newNote.DefaultBindings);
        Assert.Equal(expectedControl, primary.Control);
        Assert.Equal(expectedMeta, primary.Meta);
        Assert.True(Assert.Single(bold.DefaultBindings).Control);
    }

    [Fact]
    public void Matches_QuestionMarkBindingAcceptsOemAlias()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.ShowShortcuts] =
        [
            new KeyboardShortcutBinding("OemQuestion", Shift: true, Alt: true)
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.ShowShortcuts, Key.Oem2, KeyModifiers.Alt | KeyModifiers.Shift));
    }

    [Fact]
    public void Defaults_UseIntuitiveEditingAndNavigationShortcuts()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        Assert.True(service.Matches(KeyboardShortcutActionIds.ToggleSidebar, Key.B, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.True(service.Matches(KeyboardShortcutActionIds.OpenNotePicker, Key.P, modifier));
        Assert.True(service.Matches(KeyboardShortcutActionIds.DeleteNote, Key.D, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.True(service.Matches(KeyboardShortcutActionIds.DeleteLine, Key.D, KeyModifiers.Control));
        Assert.True(service.Matches(KeyboardShortcutActionIds.MoveLineUp, Key.Up, KeyModifiers.Alt));
        Assert.True(service.Matches(KeyboardShortcutActionIds.MoveLineDown, Key.Down, KeyModifiers.Alt));
        Assert.True(service.Matches(KeyboardShortcutActionIds.ToggleTaskList, Key.X, KeyModifiers.Control | KeyModifiers.Shift));
        Assert.True(service.Matches(KeyboardShortcutActionIds.ToggleCodeBlock, Key.K, KeyModifiers.Control | KeyModifiers.Shift));
    }

    [Fact]
    public void Matches_BindingCanUseNoModifier()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.ReloadNotes] =
        [
            new KeyboardShortcutBinding("F8")
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.ReloadNotes, Key.F8, KeyModifiers.None));
        Assert.False(service.Matches(KeyboardShortcutActionIds.ReloadNotes, Key.F8, KeyModifiers.Control));
    }

    [Fact]
    public void Matches_SupportsMultipleIndependentBindingsForAction()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.OpenNotePicker] =
        [
            new KeyboardShortcutBinding("O", Alt: true),
            new KeyboardShortcutBinding("K", Control: true)
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.OpenNotePicker, Key.O, KeyModifiers.Alt));
        Assert.True(service.Matches(KeyboardShortcutActionIds.OpenNotePicker, Key.K, KeyModifiers.Control));
    }

    [Fact]
    public void Matches_RequiresExactModifiers()
    {
        var service = new KeyboardShortcutService();
        service.ApplySettings(KeyboardShortcutSettings.CreateDefault());

        Assert.True(service.Matches(KeyboardShortcutActionIds.InlineCode, Key.K, KeyModifiers.Control));
        Assert.False(service.Matches(KeyboardShortcutActionIds.InlineCode, Key.K, KeyModifiers.Control | KeyModifiers.Shift));
    }
}
