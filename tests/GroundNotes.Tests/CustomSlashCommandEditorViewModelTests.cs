using GroundNotes.Models;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class CustomSlashCommandEditorViewModelTests
{
    [Fact]
    public void NewCommand_DefaultsOrderTo100()
    {
        var vm = new CustomSlashCommandEditorViewModel(null, false);
        Assert.Equal("100", vm.Order);
    }

    [Fact]
    public void BuildCommand_PreservesTemplateExactly()
    {
        var template = "  first\r\n\r\nsecond  ";
        var vm = new CustomSlashCommandEditorViewModel(null, false) { Name = "Test", Id = "test", Template = template, Order = "7" };
        Assert.Equal(template, vm.BuildCommand().Template);
        Assert.Equal(7, vm.BuildCommand().Order);
    }

    [Theory]
    [InlineData("Bad")]
    [InlineData("bad.id")]
    [InlineData("bad id")]
    public void InvalidIdsCannotSave(string id)
    {
        var vm = new CustomSlashCommandEditorViewModel(null, false) { Name = "Test", Id = id, Template = "x" };
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void UnavailableIdsAreCaseInsensitive()
    {
        var vm = new CustomSlashCommandEditorViewModel(null, false, ["Existing"])
        { Name = "Test", Id = "existing", Template = "x" };
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void NameGeneratesLowercaseAsciiSlug()
    {
        var vm = new CustomSlashCommandEditorViewModel(null, false) { Name = " Hello, World! " };
        Assert.Equal("hello-world", vm.Id);
    }

    [Fact]
    public void InvalidOrderCannotSave()
    {
        var vm = new CustomSlashCommandEditorViewModel(null, false) { Name = "Test", Id = "test", Template = "x", Order = "nope" };
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void DuplicateIdFindsAvailableCopySuffix()
    {
        var command = new CustomSlashCommandDefinition("test", "Test", "x");
        var vm = new CustomSlashCommandEditorViewModel(command, true, ["test-copy", "test-copy-2"]);
        Assert.Equal("test-copy-3", vm.Id);
    }
}
