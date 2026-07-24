using Avalonia.Input;
using GroundNotes.Models;
using GroundNotes.Services;
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
    public void Constructor_DoesNotSelectPromptUntilUserChoosesOne()
    {
        var model = CreateModel() with
        {
            AiPrompts = [new AiPromptDefinition("example", "Example", "Prompt {selected}")]
        };

        var vm = new SettingsViewModel(model);

        Assert.Null(vm.SelectedPrompt);
        Assert.False(vm.HasSelectedPrompt);
        Assert.False(vm.CanEditSelectedPrompt);
        Assert.False(vm.CanDeleteSelectedPrompt);
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
    public void ShortcutSearch_FiltersByActionNameAndCategory()
    {
        var vm = new SettingsViewModel(CreateModel());

        vm.ShortcutSearchText = "picker";

        var action = Assert.Single(vm.FilteredShortcutActions);
        Assert.Equal(KeyboardShortcutActionIds.OpenNotePicker, action.Id);
        Assert.Equal($"Showing 1 of {vm.ShortcutActions.Count} shortcuts", vm.ShortcutFilterSummary);

        vm.ShortcutSearchText = "AI chat";

        Assert.NotEmpty(vm.FilteredShortcutActions);
        Assert.All(vm.FilteredShortcutActions, item => Assert.Equal("AI chat", item.Category));
    }

    [Fact]
    public void BuildModel_PreservesExplicitShortcutModifiers()
    {
        var vm = new SettingsViewModel(CreateModel());
        var inlineCode = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.InlineCode);

        Assert.Single(inlineCode.Bindings).Capture("K", control: false, shift: false, alt: true, meta: false);
        var binding = vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.InlineCode].Single();

        Assert.True(binding.Alt);
        Assert.False(binding.Control);
        Assert.Equal("K", binding.Key);
    }

    [Fact]
    public void BuildModel_DirectControlEnterRemainsActiveForTaskToggle()
    {
        var vm = new SettingsViewModel(CreateModel());
        var toggleTask = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.ToggleTaskState);
        var binding = Assert.Single(toggleTask.Bindings);
        binding.Capture("Enter", control: true, shift: false, alt: false, meta: false);
        var service = new KeyboardShortcutService();

        service.ApplySettings(vm.BuildModel().KeyboardShortcuts);

        Assert.False(vm.HasShortcutConflict);
        Assert.True(service.Matches(KeyboardShortcutActionIds.ToggleTaskState, Key.Enter, KeyModifiers.Control));
    }

    [Fact]
    public void BuildModel_AssignsShortcutFromBlankState()
    {
        var vm = new SettingsViewModel(CreateModel());
        var openPicker = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.OpenNotePicker);

        Assert.Single(openPicker.Bindings).Clear();
        Assert.False(openPicker.HasBindings);
        Assert.Empty(vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.OpenNotePicker]);

        var blank = openPicker.GetOrCreateEmptyShortcut();
        Assert.Equal("Blank", blank.Display);
        blank.Capture("O", control: true, shift: false, alt: false, meta: false);

        var binding = Assert.Single(vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.OpenNotePicker]);
        Assert.Equal("O", binding.Key);
        Assert.True(binding.Control);
    }

    [Fact]
    public void UnmodifiedPrintableShortcut_IsBlockedForActionRunningInsideEditor()
    {
        var vm = new SettingsViewModel(CreateModel());
        var deleteNote = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.DeleteNote);
        var binding = Assert.Single(deleteNote.Bindings);
        binding.Capture("D", control: false, shift: false, alt: false, meta: false);

        Assert.True(vm.HasShortcutConflict);
        Assert.False(binding.IsApplied);
        Assert.Contains("interfere with typing", binding.ValidationMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.DeleteNote], configured => configured.Key == "D" && !configured.Control && !configured.Shift && !configured.Alt && !configured.Meta);
    }

    [Theory]
    [InlineData(KeyboardShortcutActionIds.ChatSend, "Space")]
    [InlineData(KeyboardShortcutActionIds.DeleteLine, "Back")]
    [InlineData(KeyboardShortcutActionIds.DeleteLine, "Delete")]
    public void UnmodifiedEditingKeys_AreBlockedInsideTextInputs(string actionId, string key)
    {
        var vm = new SettingsViewModel(CreateModel());
        var action = vm.ShortcutActions.Single(item => item.Id == actionId);
        var binding = action.Bindings.First();
        binding.SelectedKey = key;
        binding.Control = false;
        binding.Shift = false;
        binding.Alt = false;
        binding.Meta = false;

        Assert.True(vm.HasShortcutConflict);
        Assert.False(binding.IsApplied);
        Assert.Contains("interfere with typing", binding.ValidationMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(vm.BuildModel().KeyboardShortcuts.Bindings[actionId], configured => configured.Key == key);
    }

    [Fact]
    public void UnmodifiedFunctionKey_IsAllowedInsideTextInputs()
    {
        var vm = new SettingsViewModel(CreateModel());
        var chatSend = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.ChatSend);
        var binding = chatSend.Bindings.First(item => item.Control);
        binding.SelectedKey = "F8";
        binding.Control = false;

        Assert.False(vm.HasShortcutConflict);
        Assert.Contains(vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.ChatSend], configured => configured.Key == "F8" && !configured.Control);
    }

    [Fact]
    public void FixedEditorShortcutConflict_IsBlocked()
    {
        var vm = new SettingsViewModel(CreateModel());
        var bold = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.Bold);
        var binding = Assert.Single(bold.Bindings);
        binding.SelectedKey = "V";

        Assert.True(vm.HasShortcutConflict);
        Assert.False(binding.IsApplied);
        Assert.Contains("Reserved", binding.ValidationMessage, StringComparison.Ordinal);
        Assert.Empty(vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.Bold]);
    }

    [Fact]
    public void ShortcutConflict_RejectsOnlyConflictingBinding()
    {
        var vm = new SettingsViewModel(CreateModel());
        var newNote = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.NewNote);
        var conflicting = Assert.Single(newNote.Bindings);
        conflicting.Capture("K", control: true, shift: false, alt: false, meta: false);

        var persistedBindings = vm.BuildModel().KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.NewNote];

        Assert.True(vm.HasShortcutConflict);
        Assert.False(conflicting.IsApplied);
        Assert.Contains("Inline code", conflicting.ValidationMessage, StringComparison.Ordinal);
        Assert.Empty(persistedBindings);
    }

    [Fact]
    public void ValidShortcutChange_AppliesWhileAnotherBindingHasConflict()
    {
        var vm = new SettingsViewModel(CreateModel());
        var newNote = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.NewNote);
        var conflicting = Assert.Single(newNote.Bindings);
        conflicting.Capture("K", control: true, shift: false, alt: false, meta: false);
        var sidebar = vm.ShortcutActions.Single(action => action.Id == KeyboardShortcutActionIds.ToggleSidebar);
        var valid = Assert.Single(sidebar.Bindings);

        valid.Capture("J", control: true, shift: true, alt: false, meta: false);
        var model = vm.BuildModel();

        Assert.False(conflicting.IsApplied);
        Assert.True(valid.IsApplied);
        Assert.Contains(model.KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.ToggleSidebar], binding => binding.Key == "J");
        Assert.DoesNotContain(model.KeyboardShortcuts.Bindings[KeyboardShortcutActionIds.NewNote], binding => binding.Key == "K" && binding.Control);
    }

    [Fact]
    public void BuildModel_ParsesUiAndFileListAppearanceSettings()
    {
        var vm = new SettingsViewModel(CreateModel())
        {
            SelectedFileListFontSize = "10",
            ShowSidebarListBackground = false,
            ShowSidebarListBorder = true
        };

        var model = vm.BuildModel();

        Assert.Equal(10, model.FileListFontSize);
        Assert.Equal("Beta Sans", model.SelectedUiFontFamilyName);
        Assert.Equal("Bold", model.SelectedUiFontVariantName);
        Assert.False(model.ShowSidebarListBackground);
        Assert.True(model.ShowSidebarListBorder);
        Assert.Equal(Enumerable.Range(8, 11).Select(size => size.ToString()), vm.FileListFontSizes);
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
            "Beta Sans",
            "Bold",
            "Alpha Mono",
            "Regular",
            "Alpha Mono",
            "Regular",
            12,
            12,
            9,
            true,
            true,
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
            [],
            KeyboardShortcutSettings.CreateDefault());
    }
}
