namespace QuickNotesTxt.Services;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<string?> GetNotesFolderAsync(CancellationToken cancellationToken = default);

    Task SetNotesFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<double?> GetEditorFontSizeAsync(CancellationToken cancellationToken = default);

    Task SetEditorFontSizeAsync(double fontSize, CancellationToken cancellationToken = default);
}

public sealed record AppSettings(string? NotesFolder, double? EditorFontSize);
