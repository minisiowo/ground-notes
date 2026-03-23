using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using GroundNotes.Styles;

namespace GroundNotes.Services;

public sealed class ThemeLoaderService : IThemeLoaderService
{
    private static readonly JsonSerializerOptions s_readOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public string ThemesDirectory { get; }

    public ThemeLoaderService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroundNotes", "themes"))
    {
    }

    public ThemeLoaderService(string themesDirectory)
    {
        ThemesDirectory = themesDirectory;
    }

    public async Task<IReadOnlyList<AppTheme>> LoadAllThemesAsync()
    {
        var themes = new List<AppTheme>(AppTheme.BuiltInThemes);

        if (!Directory.Exists(ThemesDirectory))
        {
            return themes;
        }

        var builtInNames = new HashSet<string>(
            AppTheme.BuiltInThemes.Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(ThemesDirectory, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var theme = await JsonSerializer.DeserializeAsync<AppTheme>(stream, s_readOptions);

                if (theme is null || string.IsNullOrWhiteSpace(theme.Name))
                {
                    continue;
                }

                if (builtInNames.Contains(theme.Name))
                {
                    continue;
                }

                if (!AreAllColorsValid(theme))
                {
                    continue;
                }

                themes.Add(theme);
            }
            catch (JsonException)
            {
                // Skip malformed files
            }
        }

        return themes;
    }

    public async Task ExportThemeAsync(AppTheme theme)
    {
        Directory.CreateDirectory(ThemesDirectory);

        var sanitizedName = SanitizeFileName(theme.Name);
        var filePath = Path.Combine(ThemesDirectory, sanitizedName + ".json");

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, theme, s_writeOptions);
    }

    private static bool AreAllColorsValid(AppTheme theme)
    {
        string[] colorValues =
        [
            theme.AppBackground,
            theme.PaneBackground,
            theme.SurfaceBackground,
            theme.SurfaceHover,
            theme.SurfacePressed,
            theme.SurfaceRaised,
            theme.SelectionBackground,
            theme.SelectionBorder,
            theme.TextSelectionBrush,
            theme.EditorTextSelectionBrush,
            theme.BorderBase,
            theme.FocusBorder,
            theme.PrimaryText,
            theme.SecondaryText,
            theme.MutedText,
            theme.PlaceholderText,
            theme.EditorText,
            theme.AppText,
            theme.MarkdownHeading1,
            theme.MarkdownHeading2,
            theme.MarkdownHeading3,
            theme.MarkdownLinkLabel,
            theme.MarkdownLinkUrl,
            theme.MarkdownTaskDone,
            theme.MarkdownTaskPending,
            theme.MarkdownStrikethrough,
            theme.MarkdownRule,
            theme.MarkdownBlockquote,
            theme.MarkdownFenceMarker,
            theme.MarkdownFenceInfo,
            theme.MarkdownInlineCodeForeground,
            theme.MarkdownInlineCodeBackground,
            theme.MarkdownCodeBlockForeground,
            theme.MarkdownCodeBlockBackground,
            theme.TitleBarButtonHover,
            theme.TitleBarCloseHover,
        ];

        foreach (var hex in colorValues)
        {
            if (string.IsNullOrWhiteSpace(hex) || !Color.TryParse(hex, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
