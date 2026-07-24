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

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public void CreateDefinitions_DefinesZenModeWithPlatformDefault(
        bool isMacOS,
        bool expectedControl,
        bool expectedMeta)
    {
        var definitions = KeyboardShortcutCatalog.CreateDefinitions(isMacOS);
        var zenMode = definitions.Single(definition => definition.Id == KeyboardShortcutActionIds.ToggleZenMode);

        Assert.Equal("Toggle ZEN mode", zenMode.Name);
        Assert.Equal("General", zenMode.Category);
        Assert.Equal(KeyboardShortcutScope.MainWindow, zenMode.Scope);
        var binding = Assert.Single(zenMode.DefaultBindings);
        Assert.Equal("M", binding.Key);
        Assert.Equal(expectedControl, binding.Control);
        Assert.True(binding.Shift);
        Assert.Equal(expectedMeta, binding.Meta);
    }

    [Fact]
    public void CreateDefinitions_DefinesNewNoteWindowWithCtrlShiftN()
    {
        var definitions = KeyboardShortcutCatalog.CreateDefinitions(isMacOS: false);
        var newNoteWindow = definitions.Single(definition => definition.Id == KeyboardShortcutActionIds.NewNoteWindow);

        Assert.Equal("New note in new window", newNoteWindow.Name);
        Assert.Equal("Notes", newNoteWindow.Category);
        Assert.Equal(KeyboardShortcutScope.MainWindow, newNoteWindow.Scope);
        var binding = Assert.Single(newNoteWindow.DefaultBindings);
        Assert.Equal("N", binding.Key);
        Assert.True(binding.Control);
        Assert.True(binding.Shift);
        Assert.False(binding.Alt);
        Assert.False(binding.Meta);
    }

    [Fact]
    public void ApplySettings_AddsNewNoteWindowDefaultToOlderConfiguration()
    {
        var olderSettings = KeyboardShortcutSettings.CreateDefault();
        olderSettings.Bindings.Remove(KeyboardShortcutActionIds.NewNoteWindow);
        var service = new KeyboardShortcutService();

        service.ApplySettings(olderSettings);

        Assert.True(service.Matches(
            KeyboardShortcutActionIds.NewNoteWindow,
            Key.N,
            KeyModifiers.Control | KeyModifiers.Shift));
    }

    [Fact]
    public void ApplySettings_DoesNotAddNewNoteWindowDefaultWhenGestureIsAlreadyConfigured()
    {
        var olderSettings = KeyboardShortcutSettings.CreateDefault();
        olderSettings.Bindings.Remove(KeyboardShortcutActionIds.NewNoteWindow);
        olderSettings.Bindings[KeyboardShortcutActionIds.OpenNotePicker] =
        [
            new KeyboardShortcutBinding("N", Control: true, Shift: true)
        ];
        var service = new KeyboardShortcutService();

        service.ApplySettings(olderSettings);

        Assert.Empty(service.Settings.Bindings[KeyboardShortcutActionIds.NewNoteWindow]);
    }

    [Fact]
    public void ApplySettings_AddsZenModeDefaultToOlderConfiguration()
    {
        var olderSettings = KeyboardShortcutSettings.CreateDefault();
        olderSettings.Bindings.Remove(KeyboardShortcutActionIds.ToggleZenMode);
        var service = new KeyboardShortcutService();
        var primaryModifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        service.ApplySettings(olderSettings);

        var binding = Assert.Single(service.Settings.Bindings[KeyboardShortcutActionIds.ToggleZenMode]);
        Assert.Equal("M", binding.Key);
        Assert.True(binding.Shift);
        Assert.True(service.Matches(
            KeyboardShortcutActionIds.ToggleZenMode,
            Key.M,
            primaryModifier | KeyModifiers.Shift));
    }

    [Fact]
    public void ApplySettings_DoesNotAddZenDefaultWhenGestureIsAlreadyConfigured()
    {
        var olderSettings = KeyboardShortcutSettings.CreateDefault();
        olderSettings.Bindings.Remove(KeyboardShortcutActionIds.ToggleZenMode);
        olderSettings.Bindings[KeyboardShortcutActionIds.OpenNotePicker] =
        [
            new KeyboardShortcutBinding(
                "m",
                Control: !OperatingSystem.IsMacOS(),
                Shift: true,
                Meta: OperatingSystem.IsMacOS())
        ];
        var service = new KeyboardShortcutService();

        service.ApplySettings(olderSettings);

        Assert.Empty(service.Settings.Bindings[KeyboardShortcutActionIds.ToggleZenMode]);
    }

    [Fact]
    public void LegacyDefaultDetection_IgnoresZenModeMissingFromHistoricalConfiguration()
    {
        var applicationModifier = new KeyboardShortcutBinding(string.Empty, Control: true);
        var legacyBindings = CreateCompleteLegacyDefaultBindings();

        var isComplete = KeyboardShortcutCatalog.IsCompleteLegacyDefaultConfiguration(
            legacyBindings,
            applicationModifier);

        Assert.True(isComplete);
        Assert.DoesNotContain(KeyboardShortcutActionIds.ToggleZenMode, legacyBindings.Keys);
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
        Assert.True(service.Matches(KeyboardShortcutActionIds.NewNoteWindow, Key.N, KeyModifiers.Control | KeyModifiers.Shift));
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

    private static Dictionary<string, List<KeyboardShortcutBinding>> CreateCompleteLegacyDefaultBindings()
    {
        string[] legacyActionIds =
        [
            KeyboardShortcutActionIds.OpenSettings,
            KeyboardShortcutActionIds.ShowShortcuts,
            KeyboardShortcutActionIds.ToggleYaml,
            KeyboardShortcutActionIds.ReloadNotes,
            KeyboardShortcutActionIds.NewNote,
            KeyboardShortcutActionIds.OpenNotePicker,
            KeyboardShortcutActionIds.DeleteNote,
            KeyboardShortcutActionIds.ClosePane,
            KeyboardShortcutActionIds.EqualizePanes,
            KeyboardShortcutActionIds.ToggleTaskState,
            KeyboardShortcutActionIds.Bold,
            KeyboardShortcutActionIds.Italic,
            KeyboardShortcutActionIds.InlineCode,
            KeyboardShortcutActionIds.MoveLineUp,
            KeyboardShortcutActionIds.MoveLineDown,
            KeyboardShortcutActionIds.DeleteLine,
            KeyboardShortcutActionIds.ToggleTaskList,
            KeyboardShortcutActionIds.ToggleBulletList,
            KeyboardShortcutActionIds.Heading1,
            KeyboardShortcutActionIds.Heading2,
            KeyboardShortcutActionIds.Heading3,
            KeyboardShortcutActionIds.GenerateTitleSuggestions,
            KeyboardShortcutActionIds.ChatSend,
            KeyboardShortcutActionIds.ChatSave
        ];
        var bindings = legacyActionIds.ToDictionary(
            actionId => actionId,
            actionId => KeyboardShortcutCatalog.Find(actionId)!.DefaultBindings.ToList(),
            StringComparer.Ordinal);

        bindings[KeyboardShortcutActionIds.OpenSettings] =
        [
            new KeyboardShortcutBinding("OemComma", Control: true),
            new KeyboardShortcutBinding("OemComma", Meta: true)
        ];
        bindings[KeyboardShortcutActionIds.ShowShortcuts] =
        [
            new KeyboardShortcutBinding("OemQuestion", Control: true, Shift: true),
            new KeyboardShortcutBinding("Oem2", Control: true, Shift: true),
            new KeyboardShortcutBinding("OemQuestion", Shift: true, Meta: true),
            new KeyboardShortcutBinding("Oem2", Shift: true, Meta: true)
        ];
        bindings[KeyboardShortcutActionIds.ToggleYaml] =
        [
            new KeyboardShortcutBinding("Y", Control: true, Shift: true),
            new KeyboardShortcutBinding("Y", Shift: true, Meta: true)
        ];
        bindings[KeyboardShortcutActionIds.ReloadNotes] = [new KeyboardShortcutBinding("R", Control: true)];
        bindings[KeyboardShortcutActionIds.NewNote] = [new KeyboardShortcutBinding("N", Control: true)];
        bindings[KeyboardShortcutActionIds.OpenNotePicker] = [new KeyboardShortcutBinding("P", Control: true)];
        bindings[KeyboardShortcutActionIds.ClosePane] =
        [
            new KeyboardShortcutBinding("W", Control: true),
            new KeyboardShortcutBinding("W", Meta: true)
        ];
        bindings[KeyboardShortcutActionIds.EqualizePanes] =
        [
            new KeyboardShortcutBinding("D0", Control: true),
            new KeyboardShortcutBinding("NumPad0", Control: true),
            new KeyboardShortcutBinding("D0", Meta: true),
            new KeyboardShortcutBinding("NumPad0", Meta: true)
        ];
        bindings[KeyboardShortcutActionIds.GenerateTitleSuggestions] =
        [
            new KeyboardShortcutBinding("Enter", Control: true),
            new KeyboardShortcutBinding("Enter", Meta: true)
        ];
        bindings[KeyboardShortcutActionIds.ChatSend] =
        [
            new KeyboardShortcutBinding("Enter", Control: true),
            new KeyboardShortcutBinding("Enter", Meta: true)
        ];

        return bindings;
    }
}
