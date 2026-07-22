using GroundNotes.Models;
using GroundNotes.ViewModels;
using Xunit;

namespace GroundNotes.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ChangingFontFamily_UpdatesAvailableVariants()
    {
        var vm = new SettingsViewModel(CreateModel());

        vm.SelectedFontFamilyName = "Beta Sans";

        Assert.Equal(["Regular", "Bold"], vm.FontVariantNames);
        Assert.Equal("Regular", vm.SelectedFontVariantName);
    }

    [Fact]
    public void BuildModel_NormalizesDefaultModelWhenBlank()
    {
        var vm = new SettingsViewModel(CreateModel());
        vm.DefaultModel = "  ";

        var model = vm.BuildModel();

        Assert.Equal("gpt-5.6-terra", model.DefaultModel);
    }

    [Fact]
    public void Constructor_PreservesCustomDefaultModelInAvailableOptions()
    {
        var model = CreateModel() with { DefaultModel = "custom-model" };

        var vm = new SettingsViewModel(model);

        Assert.Contains("custom-model", vm.AvailableModels);
        Assert.Equal("custom-model", vm.DefaultModel);
    }

    [Fact]
    public void BuildModel_NormalizesDefaultReasoningEffort()
    {
        var vm = new SettingsViewModel(CreateModel());
        vm.DefaultReasoningEffort = " HIGH ";

        var model = vm.BuildModel();

        Assert.Equal("high", model.DefaultReasoningEffort);
    }

    [Fact]
    public void Constructor_ExposesReasoningEffortOptions()
    {
        var vm = new SettingsViewModel(CreateModel());

        Assert.Equal(["none", "low", "medium", "high", "xhigh", "max"], vm.ReasoningEfforts);
        Assert.Equal("none", vm.DefaultReasoningEffort);
    }

    [Fact]
    public void BuildModel_ParsesIndentSizeAndLineHeight()
    {
        var vm = new SettingsViewModel(CreateModel())
        {
            SelectedIndentSize = "2",
            SelectedLineHeight = "1.3"
        };

        var model = vm.BuildModel();

        Assert.Equal(2, model.EditorIndentSize);
        Assert.Equal(1.3, model.EditorLineHeightFactor);
    }

    private static SettingsDialogModel CreateModel()
    {
        return new SettingsDialogModel(
            ["Dark", "Light"],
            [
                new BundledFontFamilyOption(
                    "alpha",
                    "Alpha Mono",
                    "alpha",
                    [new BundledFontVariantOption("regular", "Regular", default, default)]),
                new BundledFontFamilyOption(
                    "beta",
                    "Beta Sans",
                    "beta",
                    [
                        new BundledFontVariantOption("regular", "Regular", default, default),
                        new BundledFontVariantOption("bold", "Bold", default, default)
                    ])
            ],
            "Dark",
            "Alpha Mono",
            "Regular",
            "Alpha Mono",
            "Regular",
            "Alpha Mono",
            "Regular",
            12,
            12,
            4,
            1.15,
            true,
            true,
            string.Empty,
            "gpt-5.6-terra",
            "none",
            string.Empty,
            string.Empty,
            "/tmp/prompts",
            []);
    }
}
