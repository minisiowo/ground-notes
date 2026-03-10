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
            return new AppSettings(null, null, null, null);
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, cancellationToken: cancellationToken);

        WindowLayout? layout = settings?.WindowWidth is not null && settings.WindowHeight is not null
            ? new WindowLayout(settings.WindowWidth.Value, settings.WindowHeight.Value,
                settings.WindowX ?? 0, settings.WindowY ?? 0, settings.IsMaximized ?? false)
            : null;

        return new AppSettings(settings?.NotesFolder, settings?.EditorFontSize, settings?.ThemeName, layout);
    }

    public async Task<string?> GetNotesFolderAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.NotesFolder;
    }

    public async Task SetNotesFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var record = await LoadRecordAsync(cancellationToken);
        await SaveAsync(record with { NotesFolder = folderPath }, cancellationToken);
    }

    public async Task<double?> GetEditorFontSizeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.EditorFontSize;
    }

    public async Task SetEditorFontSizeAsync(double fontSize, CancellationToken cancellationToken = default)
    {
        var record = await LoadRecordAsync(cancellationToken);
        await SaveAsync(record with { EditorFontSize = fontSize }, cancellationToken);
    }

    public async Task<string?> GetThemeNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.ThemeName;
    }

    public async Task SetThemeNameAsync(string themeName, CancellationToken cancellationToken = default)
    {
        var record = await LoadRecordAsync(cancellationToken);
        await SaveAsync(record with { ThemeName = themeName }, cancellationToken);
    }

    public async Task<WindowLayout?> GetWindowLayoutAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.WindowLayout;
    }

    public async Task SetWindowLayoutAsync(WindowLayout layout, CancellationToken cancellationToken = default)
    {
        var record = await LoadRecordAsync(cancellationToken);
        await SaveAsync(record with
        {
            WindowWidth = layout.Width,
            WindowHeight = layout.Height,
            WindowX = layout.X,
            WindowY = layout.Y,
            IsMaximized = layout.IsMaximized
        }, cancellationToken);
    }

    private async Task<SettingsRecord> LoadRecordAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsRecord(null, null, null, null, null, null, null, null);
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        return await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, cancellationToken: cancellationToken)
               ?? new SettingsRecord(null, null, null, null, null, null, null, null);
    }

    private async Task SaveAsync(SettingsRecord settings, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
    }

    private sealed record SettingsRecord(
        string? NotesFolder,
        double? EditorFontSize,
        string? ThemeName,
        double? WindowWidth,
        double? WindowHeight,
        double? WindowX,
        double? WindowY,
        bool? IsMaximized);
}
