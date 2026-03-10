namespace QuickNotesTxt.Services;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<string?> GetNotesFolderAsync(CancellationToken cancellationToken = default);

    Task SetNotesFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<double?> GetEditorFontSizeAsync(CancellationToken cancellationToken = default);

    Task SetEditorFontSizeAsync(double fontSize, CancellationToken cancellationToken = default);

    Task<string?> GetThemeNameAsync(CancellationToken cancellationToken = default);

    Task SetThemeNameAsync(string themeName, CancellationToken cancellationToken = default);

    Task<WindowLayout?> GetWindowLayoutAsync(CancellationToken cancellationToken = default);

    Task SetWindowLayoutAsync(WindowLayout layout, CancellationToken cancellationToken = default);
}

public sealed record AppSettings(string? NotesFolder, double? EditorFontSize, string? ThemeName, WindowLayout? WindowLayout);

public sealed record WindowLayout(double Width, double Height, double X, double Y, bool IsMaximized);
