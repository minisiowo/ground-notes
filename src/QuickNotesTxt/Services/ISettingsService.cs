using QuickNotesTxt.Models;

namespace QuickNotesTxt.Services;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default);

    Task SetAiSettingsAsync(AiSettings settings, CancellationToken cancellationToken = default);

    Task<string?> GetNotesFolderAsync(CancellationToken cancellationToken = default);

    Task SetNotesFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<double?> GetEditorFontSizeAsync(CancellationToken cancellationToken = default);

    Task SetEditorFontSizeAsync(double fontSize, CancellationToken cancellationToken = default);

    Task<double?> GetUiFontSizeAsync(CancellationToken cancellationToken = default);

    Task SetUiFontSizeAsync(double fontSize, CancellationToken cancellationToken = default);

    Task<string?> GetFontNameAsync(CancellationToken cancellationToken = default);

    Task SetFontNameAsync(string fontName, CancellationToken cancellationToken = default);

    Task<string?> GetFontVariantNameAsync(CancellationToken cancellationToken = default);

    Task SetFontVariantNameAsync(string fontVariantName, CancellationToken cancellationToken = default);

    Task<string?> GetSidebarFontNameAsync(CancellationToken cancellationToken = default);

    Task SetSidebarFontNameAsync(string sidebarFontName, CancellationToken cancellationToken = default);

    Task<string?> GetSidebarFontVariantNameAsync(CancellationToken cancellationToken = default);

    Task SetSidebarFontVariantNameAsync(string sidebarFontVariantName, CancellationToken cancellationToken = default);

    Task<string?> GetCodeFontNameAsync(CancellationToken cancellationToken = default);

    Task SetCodeFontNameAsync(string codeFontName, CancellationToken cancellationToken = default);

    Task<string?> GetCodeFontVariantNameAsync(CancellationToken cancellationToken = default);

    Task SetCodeFontVariantNameAsync(string codeFontVariantName, CancellationToken cancellationToken = default);

    Task<string?> GetThemeNameAsync(CancellationToken cancellationToken = default);

    Task SetThemeNameAsync(string themeName, CancellationToken cancellationToken = default);

    Task<WindowLayout?> GetWindowLayoutAsync(CancellationToken cancellationToken = default);

    WindowLayout? GetWindowLayoutSync();

    Task SetWindowLayoutAsync(WindowLayout layout, CancellationToken cancellationToken = default);

    void SetWindowLayoutSync(WindowLayout layout);
}

public sealed record AppSettings(
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
    WindowLayout? WindowLayout,
    AiSettings AiSettings);

public sealed record WindowLayout(double Width, double Height, double X, double Y, bool IsMaximized, double? SidebarWidth = null, bool? SidebarCollapsed = null);
