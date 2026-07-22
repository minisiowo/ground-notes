using GroundNotes.Models;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class AiPromptEditorViewModelTests
{
    [Fact]
    public void Constructor_ExposesModelAndReasoningOptions()
    {
        var vm = new AiPromptEditorViewModel(null, "gpt-5.6-terra", "none", duplicate: false);

        Assert.Contains("gpt-5.6-sol", vm.AvailableModelOptions);
        Assert.Contains("gpt-5.6-terra", vm.AvailableModelOptions);
        Assert.Contains("gpt-5.6-luna", vm.AvailableModelOptions);
        Assert.Equal("Use default: gpt-5.6-terra", vm.SelectedModelOption);
        Assert.Equal("Use default: none", vm.SelectedReasoningEffortOption);
    }

    [Fact]
    public void BuildPrompt_UsesNullModelAndReasoningForDefaultOptions()
    {
        var vm = new AiPromptEditorViewModel(null, "gpt-5.6-terra", "none", duplicate: false)
        {
            Name = "Summarize",
            Id = "summarize",
            PromptTemplate = "Summarize {selected}"
        };

        var prompt = vm.BuildPrompt();

        Assert.Null(prompt.Model);
        Assert.Null(prompt.ReasoningEffort);
    }

    [Fact]
    public void BuildPrompt_PreservesSelectedModelAndReasoning()
    {
        var vm = new AiPromptEditorViewModel(null, "gpt-5.6-terra", "none", duplicate: false)
        {
            Name = "Deep Fix",
            Id = "deep-fix",
            PromptTemplate = "Fix {selected}",
            SelectedModelOption = "gpt-5.6-sol",
            SelectedReasoningEffortOption = "max"
        };

        var prompt = vm.BuildPrompt();

        Assert.Equal("gpt-5.6-sol", prompt.Model);
        Assert.Equal("max", prompt.ReasoningEffort);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-0.1")]
    [InlineData("2.1")]
    public void CanSave_RejectsInvalidTemperature(string temperature)
    {
        var vm = new AiPromptEditorViewModel(null, "gpt-5.6-terra", "none", duplicate: false)
        {
            Name = "Invalid Temperature",
            Id = "invalid-temperature",
            PromptTemplate = "Prompt {selected}",
            Temperature = temperature
        };

        Assert.False(vm.CanSave);
        Assert.Contains("0 to 2", vm.ValidationMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void CanSave_RequiresSelectedPlaceholder()
    {
        var vm = new AiPromptEditorViewModel(null, "gpt-5.6-terra", "none", duplicate: false)
        {
            Name = "Invalid",
            Id = "invalid",
            PromptTemplate = "No placeholder"
        };

        Assert.False(vm.CanSave);
        Assert.Contains("{selected}", vm.ValidationMessage, StringComparison.Ordinal);
    }
}
