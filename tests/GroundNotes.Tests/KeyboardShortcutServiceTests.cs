using Avalonia.Input;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class KeyboardShortcutServiceTests
{
    [Fact]
    public void Matches_ApplicationModifierBindingUsesConfiguredModifier()
    {
        var settings = KeyboardShortcutSettings.CreateDefault() with
        {
            ApplicationModifier = ApplicationShortcutModifier.Alt
        };
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.NewNote, Key.N, KeyModifiers.Alt));
        Assert.False(service.Matches(KeyboardShortcutActionIds.NewNote, Key.N, KeyModifiers.Control));
    }

    [Fact]
    public void Matches_DirectBindingDoesNotChangeWithApplicationModifier()
    {
        var settings = KeyboardShortcutSettings.CreateDefault() with
        {
            ApplicationModifier = ApplicationShortcutModifier.Alt
        };
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.InlineCode, Key.K, KeyModifiers.Control));
        Assert.False(service.Matches(KeyboardShortcutActionIds.InlineCode, Key.K, KeyModifiers.Alt));
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

    [Fact]
    public void Matches_QuestionMarkBindingAcceptsOemAlias()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.ShowShortcuts] =
        [
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "OemQuestion", Shift: true, Alt: true)
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.ShowShortcuts, Key.Oem2, KeyModifiers.Alt | KeyModifiers.Shift));
    }

    [Fact]
    public void Normalize_LegacyDefaultsCollapseTechnicalAlternatives()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.ShowShortcuts] =
        [
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "OemQuestion", Control: true, Shift: true),
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "Oem2", Control: true, Shift: true),
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "OemQuestion", Shift: true, Meta: true),
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "Oem2", Shift: true, Meta: true)
        ];

        var normalized = KeyboardShortcutSettings.Normalize(settings);

        Assert.Single(normalized.Bindings[KeyboardShortcutActionIds.ShowShortcuts]);
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
    public void Normalize_PreviousDefaultsMigrateToNewBindings()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.ToggleSidebar] =
        [
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Modifier, "L")
        ];
        settings.Bindings[KeyboardShortcutActionIds.DeleteLine] =
        [
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "D", Control: true, Shift: true)
        ];

        var normalized = KeyboardShortcutSettings.Normalize(settings);

        Assert.Contains(normalized.Bindings[KeyboardShortcutActionIds.ToggleSidebar], binding => binding.Key == "B" && binding.Control && binding.Shift);
        Assert.Contains(normalized.Bindings[KeyboardShortcutActionIds.DeleteLine], binding => binding.Key == "D" && binding.Control && !binding.Shift);
    }

    [Fact]
    public void Matches_DirectBindingCanUseNoModifier()
    {
        var settings = KeyboardShortcutSettings.CreateDefault();
        settings.Bindings[KeyboardShortcutActionIds.ReloadNotes] =
        [
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "F8")
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.ReloadNotes, Key.F8, KeyModifiers.None));
        Assert.False(service.Matches(KeyboardShortcutActionIds.ReloadNotes, Key.F8, KeyModifiers.Control));
    }

    [Fact]
    public void Matches_SupportsMultipleIndependentBindingsForAction()
    {
        var settings = KeyboardShortcutSettings.CreateDefault() with
        {
            ApplicationModifier = ApplicationShortcutModifier.Alt
        };
        settings.Bindings[KeyboardShortcutActionIds.OpenNotePicker] =
        [
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Modifier, "O"),
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "K", Control: true)
        ];
        var service = new KeyboardShortcutService();
        service.ApplySettings(settings);

        Assert.True(service.Matches(KeyboardShortcutActionIds.OpenNotePicker, Key.O, KeyModifiers.Alt));
        Assert.True(service.Matches(KeyboardShortcutActionIds.OpenNotePicker, Key.K, KeyModifiers.Control));
    }

    [Fact]
    public void Matches_RequiresExactDirectModifiers()
    {
        var service = new KeyboardShortcutService();
        service.ApplySettings(KeyboardShortcutSettings.CreateDefault());

        Assert.True(service.Matches(KeyboardShortcutActionIds.InlineCode, Key.K, KeyModifiers.Control));
        Assert.False(service.Matches(KeyboardShortcutActionIds.InlineCode, Key.K, KeyModifiers.Control | KeyModifiers.Shift));
    }
}
