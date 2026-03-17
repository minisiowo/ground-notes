using System.Text.Json;
using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class FolderSettingsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "QuickNotesTxt.Tests", Guid.NewGuid().ToString("N"));
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
        Assert.Equal("Dark", settings.ThemeName);
    }

    [Fact]
    public async Task SetFontNameAsync_RoundTripsThroughSettingsFile()
    {
        await _service.SetFontNameAsync("Iosevka");
        await _service.SetFontVariantNameAsync("Bold");

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("Iosevka", settings.FontName);
        Assert.Equal("Bold", settings.FontVariantName);
    }

    [Fact]
    public async Task SetSidebarFontNameAsync_RoundTripsThroughSettingsFile()
    {
        await _service.SetSidebarFontNameAsync("MonaspaceXenon");
        await _service.SetSidebarFontVariantNameAsync("Medium");

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("MonaspaceXenon", settings.SidebarFontName);
        Assert.Equal("Medium", settings.SidebarFontVariantName);
    }

    [Fact]
    public async Task SetCodeFontNameAsync_RoundTripsThroughSettingsFile()
    {
        await _service.SetCodeFontNameAsync("JetBrainsMono");
        await _service.SetCodeFontVariantNameAsync("SemiBold");

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
    public async Task SetAiSettingsAsync_RoundTripsThroughSettingsFile()
    {
        var expected = new AiSettings("secret", "gpt-4.1-mini", true, "proj_123", "org_456");

        await _service.SetAiSettingsAsync(expected);

        var settings = await _service.GetAiSettingsAsync();

        Assert.Equal(expected, settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
