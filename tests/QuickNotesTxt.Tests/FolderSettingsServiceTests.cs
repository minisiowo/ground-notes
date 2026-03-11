using System.Text.Json;
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
        Assert.Equal("Dark", settings.ThemeName);
    }

    [Fact]
    public async Task SetFontNameAsync_RoundTripsThroughSettingsFile()
    {
        await _service.SetFontNameAsync("IosevkaSerif");

        var settings = await _service.GetSettingsAsync();

        Assert.Equal("IosevkaSerif", settings.FontName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
