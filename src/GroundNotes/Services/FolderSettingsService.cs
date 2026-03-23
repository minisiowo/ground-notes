using System.Text.Json;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class FolderSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);

    public FolderSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroundNotes"))
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
            var record = await LoadRecordAsync(cancellationToken);
            return MapToAppSettings(record);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public AppSettings GetSettingsSync()
    {
        _settingsLock.Wait();
        try
        {
            return MapToAppSettings(LoadRecordSync());
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSettings(settings);
        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            await SaveAsync(MapToRecord(normalized), cancellationToken);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public void SaveSettingsSync(AppSettings settings)
    {
        var normalized = NormalizeSettings(settings);
        _settingsLock.Wait();
        try
        {
            SaveSync(MapToRecord(normalized));
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _settingsLock.WaitAsync(cancellationToken);
        try
        {
            var current = MapToAppSettings(await LoadRecordAsync(cancellationToken));
            var updated = NormalizeSettings(update(current));
            await SaveAsync(MapToRecord(updated), cancellationToken);
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

    public Task SetAiSettingsAsync(AiSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = AiSettings.Normalize(settings);
        return UpdateSettingsAsync(current => current with { AiSettings = normalized }, cancellationToken);
    }

    private static AppSettings NormalizeSettings(AppSettings settings)
    {
        return settings with
        {
            EditorIndentSize = EditorDisplaySettings.NormalizeIndentSize(settings.EditorIndentSize),
            EditorLineHeightFactor = EditorDisplaySettings.NormalizeLineHeightFactor(settings.EditorLineHeightFactor),
            AiSettings = AiSettings.Normalize(settings.AiSettings)
        };
    }

    private static AppSettings MapToAppSettings(SettingsRecord record)
    {
        return NormalizeSettings(new AppSettings(
            record.NotesFolder,
            record.EditorFontSize,
            record.UiFontSize,
            record.EditorIndentSize,
            record.EditorLineHeightFactor,
            record.FontName,
            record.FontVariantName,
            record.SidebarFontName,
            record.SidebarFontVariantName,
            record.CodeFontName,
            record.CodeFontVariantName,
            record.ThemeName,
            record.WindowLayout is null
                ? null
                : new WindowLayout(
                    record.WindowLayout.Width,
                    record.WindowLayout.Height,
                    record.WindowLayout.X,
                    record.WindowLayout.Y,
                    record.WindowLayout.IsMaximized,
                    record.WindowLayout.SidebarWidth,
                    record.WindowLayout.SidebarCollapsed),
            new AiSettings(
                record.OpenAiApiKey ?? string.Empty,
                record.OpenAiModel ?? string.Empty,
                record.AiEnabled ?? AiSettings.Default.IsEnabled,
                record.OpenAiProjectId ?? string.Empty,
                record.OpenAiOrganizationId ?? string.Empty)));
    }

    private static SettingsRecord MapToRecord(AppSettings settings)
    {
        return new SettingsRecord
        {
            NotesFolder = settings.NotesFolder,
            EditorFontSize = settings.EditorFontSize,
            UiFontSize = settings.UiFontSize,
            EditorIndentSize = settings.EditorIndentSize,
            EditorLineHeightFactor = settings.EditorLineHeightFactor,
            FontName = settings.FontName,
            FontVariantName = settings.FontVariantName,
            SidebarFontName = settings.SidebarFontName,
            SidebarFontVariantName = settings.SidebarFontVariantName,
            CodeFontName = settings.CodeFontName,
            CodeFontVariantName = settings.CodeFontVariantName,
            ThemeName = settings.ThemeName,
            WindowLayout = settings.WindowLayout is null
                ? null
                : new WindowLayoutRecord
                {
                    Width = settings.WindowLayout.Width,
                    Height = settings.WindowLayout.Height,
                    X = settings.WindowLayout.X,
                    Y = settings.WindowLayout.Y,
                    IsMaximized = settings.WindowLayout.IsMaximized,
                    SidebarWidth = settings.WindowLayout.SidebarWidth,
                    SidebarCollapsed = settings.WindowLayout.SidebarCollapsed
                },
            OpenAiApiKey = settings.AiSettings.ApiKey,
            OpenAiModel = settings.AiSettings.DefaultModel,
            AiEnabled = settings.AiSettings.IsEnabled,
            OpenAiProjectId = settings.AiSettings.ProjectId,
            OpenAiOrganizationId = settings.AiSettings.OrganizationId
        };
    }

    private async Task<SettingsRecord> LoadRecordAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsRecord();
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<SettingsRecord>(stream, s_jsonOptions, cancellationToken);
        return settings ?? new SettingsRecord();
    }

    private SettingsRecord LoadRecordSync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsRecord();
        }

        var json = File.ReadAllText(_settingsFilePath);
        var settings = JsonSerializer.Deserialize<SettingsRecord>(json, s_jsonOptions);
        return settings ?? new SettingsRecord();
    }

    private async Task SaveAsync(SettingsRecord record, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, record, cancellationToken: cancellationToken);
    }

    private void SaveSync(SettingsRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        var json = JsonSerializer.Serialize(record);
        File.WriteAllText(_settingsFilePath, json);
    }

    private sealed class SettingsRecord
    {
        public string? NotesFolder { get; set; }
        public double? EditorFontSize { get; set; }
        public double? UiFontSize { get; set; }
        public int? EditorIndentSize { get; set; }
        public double? EditorLineHeightFactor { get; set; }
        public string? FontName { get; set; }
        public string? FontVariantName { get; set; }
        public string? SidebarFontName { get; set; }
        public string? SidebarFontVariantName { get; set; }
        public string? CodeFontName { get; set; }
        public string? CodeFontVariantName { get; set; }
        public string? ThemeName { get; set; }
        public WindowLayoutRecord? WindowLayout { get; set; }
        public string? OpenAiApiKey { get; set; }
        public string? OpenAiModel { get; set; }
        public bool? AiEnabled { get; set; }
        public string? OpenAiProjectId { get; set; }
        public string? OpenAiOrganizationId { get; set; }
    }

    private sealed class WindowLayoutRecord
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsMaximized { get; set; }
        public double? SidebarWidth { get; set; }
        public bool? SidebarCollapsed { get; set; }
    }
}
