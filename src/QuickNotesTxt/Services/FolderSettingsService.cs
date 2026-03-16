using System.Text.Json;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public sealed class FolderSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);

    public FolderSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickNotesTxt"))
    {
    }

    public FolderSettingsService(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings(null, null, null, null, null, null, null, null, null, null, null, AiSettings.Default);
            }

            await using var stream = File.OpenRead(_settingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, s_jsonOptions, cancellationToken);

            WindowLayout? layout = settings?.WindowWidth is not null && settings.WindowHeight is not null
                ? new WindowLayout(settings.WindowWidth.Value, settings.WindowHeight.Value,
                    settings.WindowX ?? 0, settings.WindowY ?? 0, settings.IsMaximized ?? false,
                    settings.SidebarWidth, settings.SidebarCollapsed)
                : null;

            return new AppSettings(
                settings?.NotesFolder,
                settings?.EditorFontSize,
                settings?.UiFontSize,
                settings?.FontName,
                settings?.FontVariantName,
                settings?.SidebarFontName,
                settings?.SidebarFontVariantName,
                settings?.CodeFontName,
                settings?.CodeFontVariantName,
                settings?.ThemeName,
                layout,
                new AiSettings(
                    settings?.OpenAiApiKey ?? string.Empty,
                    settings?.OpenAiModel ?? AiSettings.Default.DefaultModel,
                    settings?.AiEnabled ?? AiSettings.Default.IsEnabled,
                    settings?.OpenAiProjectId ?? string.Empty,
                    settings?.OpenAiOrganizationId ?? string.Empty));
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.AiSettings;
    }

    public async Task SetAiSettingsAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with
            {
                OpenAiApiKey = settings.ApiKey,
                OpenAiModel = settings.DefaultModel,
                AiEnabled = settings.IsEnabled,
                OpenAiProjectId = settings.ProjectId,
                OpenAiOrganizationId = settings.OrganizationId
            }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetNotesFolderAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.NotesFolder;
    }

    public async Task SetNotesFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { NotesFolder = folderPath }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<double?> GetEditorFontSizeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.EditorFontSize;
    }

    public async Task SetEditorFontSizeAsync(double fontSize, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { EditorFontSize = fontSize }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<double?> GetUiFontSizeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.UiFontSize;
    }

    public async Task SetUiFontSizeAsync(double fontSize, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { UiFontSize = fontSize }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetFontNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.FontName;
    }

    public async Task SetFontNameAsync(string fontName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { FontName = fontName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetFontVariantNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.FontVariantName;
    }

    public async Task SetFontVariantNameAsync(string fontVariantName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { FontVariantName = fontVariantName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetSidebarFontNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.SidebarFontName;
    }

    public async Task SetSidebarFontNameAsync(string sidebarFontName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { SidebarFontName = sidebarFontName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetSidebarFontVariantNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.SidebarFontVariantName;
    }

    public async Task SetSidebarFontVariantNameAsync(string sidebarFontVariantName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { SidebarFontVariantName = sidebarFontVariantName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetCodeFontNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.CodeFontName;
    }

    public async Task SetCodeFontNameAsync(string codeFontName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { CodeFontName = codeFontName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetCodeFontVariantNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.CodeFontVariantName;
    }

    public async Task SetCodeFontVariantNameAsync(string codeFontVariantName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { CodeFontVariantName = codeFontVariantName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<string?> GetThemeNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.ThemeName;
    }

    public async Task SetThemeNameAsync(string themeName, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with { ThemeName = themeName }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<WindowLayout?> GetWindowLayoutAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.WindowLayout;
    }

    public WindowLayout? GetWindowLayoutSync()
    {
        _settingsLock.Wait();
        try
        {
            var record = LoadRecordSync();
            if (record.WindowWidth is not null && record.WindowHeight is not null)
            {
                return new WindowLayout(
                    record.WindowWidth.Value, record.WindowHeight.Value,
                    record.WindowX ?? 0, record.WindowY ?? 0,
                    record.IsMaximized ?? false,
                    record.SidebarWidth, record.SidebarCollapsed);
            }

            return null;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task SetWindowLayoutAsync(WindowLayout layout, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var record = await LoadRecordAsync(cancellationToken);
            await SaveAsync(record with
            {
                WindowWidth = layout.Width,
                WindowHeight = layout.Height,
                WindowX = layout.X,
                WindowY = layout.Y,
                IsMaximized = layout.IsMaximized,
                SidebarWidth = layout.SidebarWidth,
                SidebarCollapsed = layout.SidebarCollapsed
            }, cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public void SetWindowLayoutSync(WindowLayout layout)
    {
        _settingsLock.Wait();
        try
        {
            var record = LoadRecordSync();
            SaveSync(record with
            {
                WindowWidth = layout.Width,
                WindowHeight = layout.Height,
                WindowX = layout.X,
                WindowY = layout.Y,
                IsMaximized = layout.IsMaximized,
                SidebarWidth = layout.SidebarWidth,
                SidebarCollapsed = layout.SidebarCollapsed
            });
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    private async Task<SettingsRecord> LoadRecordAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsRecord(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        return await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, s_jsonOptions, cancellationToken)
               ?? new SettingsRecord(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private SettingsRecord LoadRecordSync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsRecord(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        var json = File.ReadAllText(_settingsFilePath);
        return JsonSerializer.Deserialize<SettingsRecord>(json, s_jsonOptions)
               ?? new SettingsRecord(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
    }

    private async Task SaveAsync(SettingsRecord settings, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, s_jsonOptions, cancellationToken);
    }

    private void SaveSync(SettingsRecord settings)
    {
        var json = JsonSerializer.Serialize(settings, s_jsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }

    private sealed record SettingsRecord(
        string? NotesFolder,
        double? EditorFontSize,
        double? UiFontSize,
        string? FontName,
        string? FontVariantName,
        string? SidebarFontName,
        string? SidebarFontVariantName,
        string? CodeFontName,
        string? CodeFontVariantName,
        string? ThemeName,
        double? WindowWidth,
        double? WindowHeight,
        double? WindowX,
        double? WindowY,
        bool? IsMaximized,
        double? SidebarWidth,
        bool? SidebarCollapsed,
        string? OpenAiApiKey,
        string? OpenAiModel,
        bool? AiEnabled,
        string? OpenAiProjectId,
        string? OpenAiOrganizationId);
}
