using System.Text.Json;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class FolderSettingsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly FolderSettingsService _service;
    private readonly string _settingsFilePath;

    public FolderSettingsServiceTests()
    {
        _service = new FolderSettingsService(_tempRoot);
        _settingsFilePath = Path.Combine(_tempRoot, "settings.json");
    }

    [Fact]
    public async Task GetSettingsAsync_DeserializesLegacySettingsWithoutFontName()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            notesFolder = "notes",
            editorFontSize = 14d,
            uiFontSize = 12d,
            themeName = "Dark"
        });

        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();

        Assert.Null(settings.FontName);
        Assert.Null(settings.FontVariantName);
        Assert.Null(settings.SidebarFontName);
        Assert.Null(settings.SidebarFontVariantName);
        Assert.Null(settings.CodeFontName);
        Assert.Null(settings.CodeFontVariantName);
        Assert.Equal(EditorDisplaySettings.DefaultIndentSize, settings.EditorIndentSize);
        Assert.Equal(EditorDisplaySettings.DefaultLineHeightFactor, settings.EditorLineHeightFactor);
        Assert.Equal("Dark", settings.ThemeName);
        Assert.Null(settings.WindowLayout?.EditorCanvasWidth);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RoundTripsTerminalFontThroughSettingsFile()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            FontName = "Iosevka",
            FontVariantName = "Bold"
        });

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("Iosevka", settings.FontName);
        Assert.Equal("Bold", settings.FontVariantName);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RoundTripsUiFontAndFileListSizeThroughSettingsFile()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            UiFontName = "MonaspaceXenon",
            UiFontVariantName = "Medium",
            FileListFontSize = 9
        });

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("MonaspaceXenon", settings.UiFontName);
        Assert.Equal("Medium", settings.UiFontVariantName);
        Assert.Equal(9, settings.FileListFontSize);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RoundTripsSidebarFontSizeThroughSettingsFile()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            SidebarFontSize = 9
        });

        var settings = await _service.GetSettingsAsync();

        Assert.Equal(9, settings.SidebarFontSize);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RoundTripsSidebarListAppearanceThroughSettingsFile()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            ShowSidebarListBackground = false,
            ShowSidebarListBorder = false
        });

        var settings = await _service.GetSettingsAsync();

        Assert.False(settings.ShowSidebarListBackground);
        Assert.False(settings.ShowSidebarListBorder);
    }

    [Fact]
    public async Task UpdateSettingsAsync_RoundTripsCodeFontThroughSettingsFile()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            CodeFontName = "JetBrainsMono",
            CodeFontVariantName = "SemiBold"
        });

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("JetBrainsMono", settings.CodeFontName);
        Assert.Equal("SemiBold", settings.CodeFontVariantName);
    }

    [Fact]
    public async Task GetSettingsAsync_DeserializesLegacySettingsWithoutAiFields()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            notesFolder = "notes",
            themeName = "Dark"
        });

        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();

        Assert.Equal(AiSettings.Default.DefaultModel, settings.AiSettings.DefaultModel);
        Assert.Equal(AiReasoningEffortCatalog.DefaultReasoningEffort, settings.AiSettings.DefaultReasoningEffort);
        Assert.True(settings.AiSettings.IsEnabled);
        Assert.True(settings.AiSettings.TitleGeneration.IsEnabled);
        Assert.Equal(AiTitleGenerationSettings.Default.DefaultModel, settings.AiSettings.TitleGeneration.DefaultModel);
        Assert.Equal(AiReasoningEffortCatalog.DefaultReasoningEffort, settings.AiSettings.TitleGeneration.DefaultReasoningEffort);
        Assert.Equal(string.Empty, settings.AiSettings.ApiKey);
    }

    [Fact]
    public async Task GetSettingsAsync_NormalizesWhitespaceAiFields()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            notesFolder = "notes",
            openAiApiKey = "  secret  ",
            openAiModel = "  ",
            aiEnabled = false,
            openAiProjectId = "  proj_123  ",
            openAiOrganizationId = "  org_456  ",
            openAiReasoningEffort = " HIGH ",
            aiTitleGenerationEnabled = false,
            openAiTitleGenerationModel = "  gpt-5.6-luna  ",
            openAiTitleGenerationReasoningEffort = " HIGH "
        });

        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("secret", settings.AiSettings.ApiKey);
        Assert.Equal(AiSettings.Default.DefaultModel, settings.AiSettings.DefaultModel);
        Assert.False(settings.AiSettings.IsEnabled);
        Assert.Equal("proj_123", settings.AiSettings.ProjectId);
        Assert.Equal("org_456", settings.AiSettings.OrganizationId);
        Assert.Equal("high", settings.AiSettings.DefaultReasoningEffort);
        Assert.False(settings.AiSettings.TitleGeneration.IsEnabled);
        Assert.Equal("gpt-5.6-luna", settings.AiSettings.TitleGeneration.DefaultModel);
        Assert.Equal("high", settings.AiSettings.TitleGeneration.DefaultReasoningEffort);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsTitleGenerationSettings()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            AiSettings = settings.AiSettings with
            {
                TitleGeneration = new AiTitleGenerationSettings(false, "custom-title-model", "xhigh")
            }
        });

        var settings = await _service.GetSettingsAsync();

        Assert.False(settings.AiSettings.TitleGeneration.IsEnabled);
        Assert.Equal("custom-title-model", settings.AiSettings.TitleGeneration.DefaultModel);
        Assert.Equal("xhigh", settings.AiSettings.TitleGeneration.DefaultReasoningEffort);
        var json = await File.ReadAllTextAsync(_settingsFilePath);
        Assert.Contains("custom-title-model", json, StringComparison.Ordinal);
        Assert.Contains("AiTitleGenerationEnabled", json, StringComparison.Ordinal);
        Assert.Contains("OpenAiTitleGenerationReasoningEffort", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSettingsSync_ReturnsDefaultsWhenSettingsFileDoesNotExist()
    {
        var settings = _service.GetSettingsSync();

        Assert.Null(settings.NotesFolder);
        Assert.Null(settings.ThemeName);
        Assert.Null(settings.WindowLayout);
        Assert.Equal(AiSettings.Default, settings.AiSettings);
        Assert.True(settings.ShowScrollBars);
        Assert.True(settings.ShowSidebarListBackground);
        Assert.True(settings.ShowSidebarListBorder);
    }

    [Fact]
    public async Task GetSettingsAsync_MigratesMissingVimModeSectionToDefaults()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "{\"themeName\":\"Nord\"}");

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("Nord", settings.ThemeName);
        Assert.Equal(VimModeSettings.Default, settings.VimModeSettings);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsVimModeSettings()
    {
        var vimModeSettings = VimModeSettings.Default with
        {
            IsEnabled = true,
            LeaderKey = ",",
            KeySequenceTimeoutMilliseconds = 2400,
            WhichKeyDelayMilliseconds = 400,
            UseStandardCtrlBindings = false,
            ClipboardMode = VimClipboardMode.InternalOnly,
            ShowStatus = false
        };

        await _service.UpdateSettingsAsync(settings => settings with { VimModeSettings = vimModeSettings });
        var loaded = await _service.GetSettingsAsync();
        var json = await File.ReadAllTextAsync(_settingsFilePath);

        Assert.Equal(vimModeSettings, loaded.VimModeSettings);
        Assert.Contains("\"ClipboardMode\":\"InternalOnly\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSettingsAsync_NormalizesInvalidVimModeValuesWithoutLosingOtherSettings()
    {
        await File.WriteAllTextAsync(
            _settingsFilePath,
            "{\"themeName\":\"Nord\",\"vimModeSettings\":{\"isEnabled\":true,\"leaderKey\":\"  \",\"keySequenceTimeoutMilliseconds\":10,\"whichKeyDelayMilliseconds\":2000,\"clipboardMode\":\"FutureMode\"}}");

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("Nord", settings.ThemeName);
        Assert.NotNull(settings.VimModeSettings);
        Assert.True(settings.VimModeSettings.IsEnabled);
        Assert.Equal(VimModeSettings.DefaultLeaderKey, settings.VimModeSettings.LeaderKey);
        Assert.Equal(VimModeSettings.MinKeySequenceTimeoutMilliseconds, settings.VimModeSettings.KeySequenceTimeoutMilliseconds);
        Assert.Equal(VimModeSettings.MaxWhichKeyDelayMilliseconds, settings.VimModeSettings.WhichKeyDelayMilliseconds);
        Assert.Equal(VimClipboardMode.ExplicitSystemRegister, settings.VimModeSettings.ClipboardMode);
        Assert.True(settings.VimModeSettings.UseStandardCtrlBindings);
        Assert.True(settings.VimModeSettings.ShowStatus);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsExplicitKeyboardShortcuts()
    {
        var shortcuts = KeyboardShortcutSettings.CreateDefault();
        shortcuts.Bindings[KeyboardShortcutActionIds.OpenNotePicker] =
        [
            new KeyboardShortcutBinding("O", Alt: true),
            new KeyboardShortcutBinding("K", Control: true)
        ];

        await _service.UpdateSettingsAsync(settings => settings with { KeyboardShortcuts = shortcuts });
        var loaded = await _service.GetSettingsAsync();

        Assert.Equal(shortcuts.Bindings[KeyboardShortcutActionIds.OpenNotePicker], loaded.KeyboardShortcuts?.Bindings[KeyboardShortcutActionIds.OpenNotePicker]);
        var json = await File.ReadAllTextAsync(_settingsFilePath);
        Assert.DoesNotContain("ApplicationModifier", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kind", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSettingsAsync_MigratesLegacyModifierAndDirectBindings()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            keyboardShortcuts = new
            {
                applicationModifier = 2,
                bindings = new Dictionary<string, object>
                {
                    [KeyboardShortcutActionIds.OpenNotePicker] = new object[]
                    {
                        new { kind = 0, key = "O", control = false, shift = false, alt = false, meta = false },
                        new { kind = 1, key = "K", control = true, shift = true, alt = false, meta = false }
                    }
                }
            }
        });
        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();
        var bindings = settings.KeyboardShortcuts!.Bindings[KeyboardShortcutActionIds.OpenNotePicker];

        Assert.Contains(bindings, binding => binding.Key == "O" && binding.Alt && !binding.Control);
        Assert.Contains(bindings, binding => binding.Key == "K" && binding.Control && binding.Shift);
    }

    [Fact]
    public async Task GetSettingsAsync_PreservesCustomLegacyApplicationModifier()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            keyboardShortcuts = new
            {
                applicationModifier = 2,
                bindings = new Dictionary<string, object>
                {
                    [KeyboardShortcutActionIds.NewNote] = new object[]
                    {
                        new { kind = 0, key = "N", control = false, shift = false, alt = false, meta = false }
                    }
                }
            }
        });
        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();
        var binding = Assert.Single(settings.KeyboardShortcuts!.Bindings[KeyboardShortcutActionIds.NewNote]);

        Assert.Equal("N", binding.Key);
        Assert.True(binding.Alt);
        Assert.False(binding.Control);
        Assert.False(binding.Meta);
    }

    [Fact]
    public async Task GetSettingsAsync_MigratesPreviousDefaultToCurrentBinding()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            keyboardShortcuts = new
            {
                applicationModifier = 1,
                bindings = new Dictionary<string, object>
                {
                    [KeyboardShortcutActionIds.ToggleSidebar] = new object[]
                    {
                        new { kind = 0, key = "L", control = false, shift = false, alt = false, meta = false }
                    }
                }
            }
        });
        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();
        var binding = Assert.Single(settings.KeyboardShortcuts!.Bindings[KeyboardShortcutActionIds.ToggleSidebar]);

        Assert.Equal("B", binding.Key);
        Assert.True(binding.Control);
        Assert.True(binding.Shift);
    }

    [Fact]
    public async Task GetSettingsAsync_CollapsesLegacyTechnicalShortcutAlternatives()
    {
        var legacySettings = JsonSerializer.Serialize(new
        {
            keyboardShortcuts = new
            {
                applicationModifier = 1,
                bindings = new Dictionary<string, object>
                {
                    [KeyboardShortcutActionIds.ShowShortcuts] = new object[]
                    {
                        new { kind = 1, key = "OemQuestion", control = true, shift = true, alt = false, meta = false },
                        new { kind = 1, key = "Oem2", control = true, shift = true, alt = false, meta = false },
                        new { kind = 1, key = "OemQuestion", control = false, shift = true, alt = false, meta = true },
                        new { kind = 1, key = "Oem2", control = false, shift = true, alt = false, meta = true }
                    }
                }
            }
        });
        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();
        var binding = Assert.Single(settings.KeyboardShortcuts!.Bindings[KeyboardShortcutActionIds.ShowShortcuts]);

        Assert.Equal("F1", binding.Key);
        Assert.False(binding.Control);
        Assert.False(binding.Meta);
    }

    [Fact]
    public async Task GetSettingsAsync_DefaultsShowScrollBars_WhenMissingInJson()
    {
        var legacySettings = JsonSerializer.Serialize(new { notesFolder = "notes" });
        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();

        Assert.True(settings.ShowScrollBars);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsShowScrollBars()
    {
        await _service.SaveSettingsAsync(new AppSettings(
            "notes",
            12,
            12,
            4,
            1.15,
            null,
            null,
            null,
            null,
            null,
            null,
            "Dark",
            false,
            false,
            null,
            AiSettings.Default));

        var loaded = await _service.GetSettingsAsync();

        Assert.False(loaded.ShowScrollBars);
    }

    [Fact]
    public async Task GetSettingsSync_MatchesGetSettingsAsync_ForPersistedValues()
    {
        var ai = new AiSettings("secret", "gpt-5.6-sol", false, "proj_sync", "org_sync", "xhigh");
        await _service.SaveSettingsAsync(new AppSettings(
            "notes-sync",
            15,
            13,
            2,
            1.3,
            "IosevkaSlab",
            "Medium",
            "IosevkaSlab",
            "Regular",
            "JetBrainsMono",
            "Bold",
            "Nord",
            true,
            true,
            new WindowLayout(1200, 800, 50, 60, true, 320, false, true, 840, null, null, ["luxoft", "luxoft/template"]),
            ai,
            StandardNoteWindowLayout: new NoteWindowLayout(820, 720),
            ZenNoteWindowLayout: new NoteWindowLayout(680, 760)));

        var asyncSettings = await _service.GetSettingsAsync();
        var syncSettings = _service.GetSettingsSync();

        Assert.Equal(asyncSettings, syncSettings);
        Assert.True(syncSettings.ShowYamlFrontMatterInEditor);
        Assert.True(syncSettings.WindowLayout?.SidebarCalendarExpanded);
        Assert.Equal(840, syncSettings.WindowLayout?.EditorCanvasWidth);
        Assert.Equal(["luxoft", "luxoft/template"], syncSettings.WindowLayout?.SidebarExpandedTagPaths);
        Assert.Equal(new NoteWindowLayout(820, 720), syncSettings.StandardNoteWindowLayout);
        Assert.Equal(new NoteWindowLayout(680, 760), syncSettings.ZenNoteWindowLayout);
    }

    [Fact]
    public void SettingsNoteWindowLayoutService_SavesModesIndependently()
    {
        var layoutService = new SettingsNoteWindowLayoutService(_service);

        layoutService.SaveLayout(NoteWindowMode.Standard, new NoteWindowLayout(840, 700));
        layoutService.SaveLayout(NoteWindowMode.Zen, new NoteWindowLayout(640, 780));

        Assert.Equal(new NoteWindowLayout(840, 700), layoutService.GetLayout(NoteWindowMode.Standard));
        Assert.Equal(new NoteWindowLayout(640, 780), layoutService.GetLayout(NoteWindowMode.Zen));
    }

    [Fact]
    public void UpdateSettingsSync_UpdatesLatestSettingsUnderSingleOperation()
    {
        _service.SaveSettingsSync(_service.GetSettingsSync() with
        {
            NotesFolder = "notes",
            ThemeName = "Nord"
        });

        _service.UpdateSettingsSync(settings => settings with
        {
            StandardNoteWindowLayout = new NoteWindowLayout(840, 700)
        });
        var settings = _service.GetSettingsSync();

        Assert.Equal("notes", settings.NotesFolder);
        Assert.Equal("Nord", settings.ThemeName);
        Assert.Equal(new NoteWindowLayout(840, 700), settings.StandardNoteWindowLayout);
    }

    [Theory]
    [InlineData("{\"themeName\":\"Nord\",\"standardNoteWindowLayout\":{\"height\":700}}")]
    [InlineData("{\"themeName\":\"Nord\",\"standardNoteWindowLayout\":{\"width\":840}}")]
    [InlineData("{\"themeName\":\"Nord\",\"standardNoteWindowLayout\":{\"width\":0,\"height\":700}}")]
    [InlineData("{\"themeName\":\"Nord\",\"standardNoteWindowLayout\":{\"width\":840,\"height\":-1}}")]
    public async Task GetSettingsAsync_IgnoresIncompleteNoteWindowLayoutWithoutLosingOtherSettings(string json)
    {
        await File.WriteAllTextAsync(_settingsFilePath, json);

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("Nord", settings.ThemeName);
        Assert.Null(settings.StandardNoteWindowLayout);
    }

    [Fact]
    public async Task GetSettingsAsync_ClampsOversizedNoteWindowLayout()
    {
        await File.WriteAllTextAsync(
            _settingsFilePath,
            "{\"standardNoteWindowLayout\":{\"width\":50000,\"height\":40000}}");

        var settings = await _service.GetSettingsAsync();

        Assert.Equal(new NoteWindowLayout(10000, 10000), settings.StandardNoteWindowLayout);
    }

    [Fact]
    public void GetSettingsSync_ReturnsDefaultsWhenSettingsFileContainsInvalidJson()
    {
        File.WriteAllText(_settingsFilePath, "not json {");

        var settings = _service.GetSettingsSync();

        Assert.Null(settings.NotesFolder);
        Assert.Null(settings.ThemeName);
        Assert.Null(settings.WindowLayout);
        Assert.Equal(AiSettings.Default, settings.AiSettings);
        Assert.True(settings.ShowScrollBars);
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaultsWhenSettingsFileContainsInvalidJson()
    {
        await File.WriteAllTextAsync(_settingsFilePath, "not json {");

        var settings = await _service.GetSettingsAsync();

        Assert.Null(settings.NotesFolder);
        Assert.Null(settings.ThemeName);
        Assert.Null(settings.WindowLayout);
        Assert.Equal(AiSettings.Default, settings.AiSettings);
        Assert.True(settings.ShowScrollBars);
    }

    [Fact]
    public void UpdateSettingsSync_DoesNotOverwriteInvalidSettingsFile()
    {
        const string invalidJson = "not json {";
        File.WriteAllText(_settingsFilePath, invalidJson);

        Assert.Throws<JsonException>(() =>
            _service.UpdateSettingsSync(settings => settings with { ThemeName = "Nord" }));

        Assert.Equal(invalidJson, File.ReadAllText(_settingsFilePath));
    }

    [Fact]
    public async Task UpdateSettingsAsync_DoesNotOverwriteInvalidSettingsFile()
    {
        const string invalidJson = "not json {";
        await File.WriteAllTextAsync(_settingsFilePath, invalidJson);

        await Assert.ThrowsAsync<JsonException>(() =>
            _service.UpdateSettingsAsync(settings => settings with { ThemeName = "Nord" }));

        Assert.Equal(invalidJson, await File.ReadAllTextAsync(_settingsFilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
