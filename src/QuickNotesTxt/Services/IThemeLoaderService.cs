using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Services;

public interface IThemeLoaderService
{
    string ThemesDirectory { get; }
    Task<IReadOnlyList<AppTheme>> LoadAllThemesAsync();
    Task ExportThemeAsync(AppTheme theme);
}
