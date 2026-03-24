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
    public async Task UpdateSettingsAsync_RoundTripsSidebarFontThroughSettingsFile()
    {
        await _service.UpdateSettingsAsync(settings => settings with
        {
            SidebarFontName = "MonaspaceXenon",
            SidebarFontVariantName = "Medium"
        });

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("MonaspaceXenon", settings.SidebarFontName);
        Assert.Equal("Medium", settings.SidebarFontVariantName);
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
        Assert.True(settings.AiSettings.IsEnabled);
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
            openAiOrganizationId = "  org_456  "
        });

        await File.WriteAllTextAsync(_settingsFilePath, legacySettings);

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("secret", settings.AiSettings.ApiKey);
        Assert.Equal(AiSettings.Default.DefaultModel, settings.AiSettings.DefaultModel);
        Assert.False(settings.AiSettings.IsEnabled);
        Assert.Equal("proj_123", settings.AiSettings.ProjectId);
        Assert.Equal("org_456", settings.AiSettings.OrganizationId);
    }

    [Fact]
    public async Task SetAiSettingsAsync_RoundTripsThroughSettingsFile()
    {
        var expected = new AiSettings("  secret  ", "  gpt-5.4-mini  ", true, "  proj_123  ", "  org_456  ");

        await _service.SetAiSettingsAsync(expected);

        var settings = await _service.GetAiSettingsAsync();

        Assert.Equal(new AiSettings("secret", "gpt-5.4-mini", true, "proj_123", "org_456"), settings);
    }

    [Fact]
    public void GetSettingsSync_ReturnsDefaultsWhenSettingsFileDoesNotExist()
    {
        var settings = _service.GetSettingsSync();

        Assert.Null(settings.NotesFolder);
        Assert.Null(settings.ThemeName);
        Assert.Null(settings.WindowLayout);
        Assert.Equal(AiSettings.Default, settings.AiSettings);
    }

    [Fact]
    public async Task GetSettingsSync_MatchesGetSettingsAsync_ForPersistedValues()
    {
        var ai = new AiSettings("secret", "gpt-5.4", false, "proj_sync", "org_sync");
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
            new WindowLayout(1200, 800, 50, 60, true, 320, false, true),
            ai));

        var asyncSettings = await _service.GetSettingsAsync();
        var syncSettings = _service.GetSettingsSync();

        Assert.Equal(asyncSettings, syncSettings);
        Assert.True(syncSettings.ShowYamlFrontMatterInEditor);
        Assert.True(syncSettings.WindowLayout?.SidebarCalendarExpanded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
