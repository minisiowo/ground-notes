using GroundNotes.Models;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class KeyboardShortcutBindingViewModelTests
{
    [Fact]
    public void Capture_RecordsCompleteCombination()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding("F8"),
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
    public void Capture_ReplacesExistingCombination()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding("F8", Alt: true),
            ["F8", "K"]);

        vm.BeginRecording();
        vm.Capture("K", control: true, shift: false, alt: false, meta: false);

        Assert.False(vm.Alt);
        Assert.True(vm.Control);
        Assert.Equal("Ctrl+K", vm.Display);
    }

    [Fact]
    public void Clear_RequestsBindingRemoval()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding("K", Control: true),
            ["K"]);
        var removalRequested = false;
        vm.RemoveRequested += (_, _) => removalRequested = true;

        vm.BeginRecording();
        vm.Clear();

        Assert.True(removalRequested);
        Assert.False(vm.IsRecording);
    }

    [Fact]
    public void CancelRecording_PreservesExistingBinding()
    {
        var vm = new KeyboardShortcutBindingViewModel(
            new KeyboardShortcutBinding("K", Control: true),
            ["K"]);

        vm.BeginRecording();
        vm.CancelRecording();

        Assert.False(vm.IsRecording);
        Assert.Equal("Ctrl+K", vm.Display);
    }
}
