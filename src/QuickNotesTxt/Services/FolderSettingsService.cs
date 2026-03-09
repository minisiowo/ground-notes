using System.Text.Json;

namespace QuickNotesTxt.Services;

public sealed class FolderSettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public FolderSettingsService()
    {
        var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickNotesTxt");
        Directory.CreateDirectory(appDataFolder);
        _settingsFilePath = Path.Combine(appDataFolder, "settings.json");
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new AppSettings(null, null);
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, cancellationToken: cancellationToken);
        return new AppSettings(settings?.NotesFolder, settings?.EditorFontSize);
    }

    public async Task<string?> GetNotesFolderAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.NotesFolder;
    }

    public async Task SetNotesFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        await SaveAsync(new SettingsRecord(folderPath, settings.EditorFontSize), cancellationToken);
    }

    public async Task<double?> GetEditorFontSizeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.EditorFontSize;
    }

    public async Task SetEditorFontSizeAsync(double fontSize, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        await SaveAsync(new SettingsRecord(settings.NotesFolder, fontSize), cancellationToken);
    }

    private async Task SaveAsync(SettingsRecord settings, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
    }

    private sealed record SettingsRecord(string? NotesFolder, double? EditorFontSize);
}
