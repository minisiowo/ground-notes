using GroundNotes.Models;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class KeyboardShortcutBindingViewModelTests
{
    [Fact]
    public void Capture_DirectBindingRecordsCompleteCombination()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "F8"),
            ApplicationShortcutModifier.Alt,
            ["F8", "K"]);

        vm.BeginRecording();
        vm.Capture("K", control: true, shift: true, alt: false, meta: false);

        Assert.False(vm.IsRecording);
        Assert.Equal("K", vm.SelectedKey);
        Assert.True(vm.Control);
        Assert.True(vm.Shift);
        Assert.Equal("Ctrl+Shift+K", vm.Display);
    }

    [Fact]
    public void Capture_ApplicationModifierBindingRecordsOnlyMainKey()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Modifier, "F8"),
            ApplicationShortcutModifier.Alt,
            ["F8", "K"]);

        vm.BeginRecording();
        vm.Capture("K", control: true, shift: true, alt: false, meta: false);

        Assert.False(vm.IsRecording);
        Assert.Equal("K", vm.SelectedKey);
        Assert.False(vm.Control);
        Assert.False(vm.Shift);
        Assert.Equal("Alt+K", vm.Display);
    }

    [Fact]
    public void CancelRecording_PreservesExistingBinding()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "K", Control: true),
            ApplicationShortcutModifier.Control,
            ["K"]);

        vm.BeginRecording();
        vm.CancelRecording();

        Assert.False(vm.IsRecording);
        Assert.Equal("Ctrl+K", vm.Display);
    }
}
