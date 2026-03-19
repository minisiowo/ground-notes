using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface ISettingsService
{
    AppSettings GetSettingsSync();

    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    void SaveSettingsSync(AppSettings settings);

    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default);

    Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default);

    Task SetAiSettingsAsync(AiSettings settings, CancellationToken cancellationToken = default);
}

public sealed record AppSettings(
    string? NotesFolder,
    double? EditorFontSize,
    double? UiFontSize,
    int? EditorIndentSize,
    double? EditorLineHeightFactor,
    string? FontName,
    string? FontVariantName,
    string? SidebarFontName,
    string? SidebarFontVariantName,
    string? CodeFontName,
    string? CodeFontVariantName,
    string? ThemeName,
    WindowLayout? WindowLayout,
    AiSettings AiSettings);

public sealed record WindowLayout(double Width, double Height, double X, double Y, bool IsMaximized, double? SidebarWidth = null, bool? SidebarCollapsed = null);
