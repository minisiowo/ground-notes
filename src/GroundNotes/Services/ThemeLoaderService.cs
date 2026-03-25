using System.Text.Json;
using Avalonia.Media;
using GroundNotes.Styles;

namespace GroundNotes.Services;

public sealed class ThemeLoaderService : IThemeLoaderService
{
    private static JsonSerializerOptions s_readOptions => JsonDefaults.ReadOptions;

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
                var json = await File.ReadAllTextAsync(file);
                var theme = DeserializeTheme(json);

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
        var exportDocument = new ThemeDocumentV2(theme.Name, theme.IsLight, theme.Palette, theme.Overrides);

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, exportDocument, s_writeOptions);
    }

    internal static AppTheme? DeserializeTheme(string json)
    {
        AppTheme? v2Theme = null;
        try
        {
            v2Theme = JsonSerializer.Deserialize<AppTheme>(json, s_readOptions);
        }
        catch (JsonException)
        {
        }

        if (v2Theme?.Palette is not null && !string.IsNullOrWhiteSpace(v2Theme.Name))
        {
            return v2Theme;
        }

        var legacyTheme = JsonSerializer.Deserialize<LegacyThemeDocument>(json, s_readOptions);
        return legacyTheme is null ? null : MapLegacyTheme(legacyTheme);
    }

    private static AppTheme? MapLegacyTheme(LegacyThemeDocument legacy)
    {
        if (string.IsNullOrWhiteSpace(legacy.Name) || legacy.IsLight is null)
        {
            return null;
        }

        if (HasMissingRequiredLegacyFields(legacy))
        {
            return null;
        }

        return new AppTheme
        {
            Name = legacy.Name,
            IsLight = legacy.IsLight.Value,
            Palette = new ThemePalette
            {
                AppBackground = legacy.AppBackground!,
                PaneBackground = legacy.PaneBackground!,
                SurfaceBackground = legacy.SurfaceBackground!,
                SurfaceHover = legacy.SurfaceHover!,
                SurfacePressed = legacy.SurfacePressed!,
                SurfaceRaised = legacy.SurfaceRaised!,
                BorderBase = legacy.BorderBase!,
                PrimaryText = legacy.PrimaryText!,
                SecondaryText = legacy.SecondaryText!,
                MutedText = legacy.MutedText!,
                PlaceholderText = legacy.PlaceholderText!,
                Accent = legacy.MarkdownHeading1!,
                AccentSoft = legacy.MarkdownFenceInfo!,
                SelectionBackground = legacy.SelectionBackground!,
                TextSelectionBrush = legacy.TextSelectionBrush!,
                EditorTextSelectionBrush = legacy.EditorTextSelectionBrush!,
                Success = legacy.MarkdownTaskDone!,
                Warning = legacy.MarkdownTaskPending!,
                Danger = legacy.TitleBarCloseHover!,
            },
            Overrides = new ThemeTokenOverrides
            {
                SelectionBorder = legacy.SelectionBorder,
                FocusBorder = legacy.FocusBorder,
                EditorText = legacy.EditorText,
                AppText = legacy.AppText,
                MarkdownHeading1 = legacy.MarkdownHeading1,
                MarkdownHeading2 = legacy.MarkdownHeading2,
                MarkdownHeading3 = legacy.MarkdownHeading3,
                MarkdownLinkLabel = legacy.MarkdownLinkLabel,
                MarkdownLinkUrl = legacy.MarkdownLinkUrl,
                MarkdownTaskDone = legacy.MarkdownTaskDone,
                MarkdownTaskPending = legacy.MarkdownTaskPending,
                MarkdownStrikethrough = legacy.MarkdownStrikethrough,
                MarkdownRule = legacy.MarkdownRule,
                MarkdownBlockquote = legacy.MarkdownBlockquote,
                MarkdownFenceMarker = legacy.MarkdownFenceMarker,
                MarkdownFenceInfo = legacy.MarkdownFenceInfo,
                MarkdownInlineCodeForeground = legacy.MarkdownInlineCodeForeground,
                MarkdownInlineCodeBackground = legacy.MarkdownInlineCodeBackground,
                MarkdownCodeBlockForeground = legacy.MarkdownCodeBlockForeground,
                MarkdownCodeBlockBackground = legacy.MarkdownCodeBlockBackground,
                TitleBarButtonHover = legacy.TitleBarButtonHover,
                TitleBarCloseHover = legacy.TitleBarCloseHover,
            }
        };
    }

    private static bool HasMissingRequiredLegacyFields(LegacyThemeDocument legacy)
    {
        return string.IsNullOrWhiteSpace(legacy.AppBackground)
               || string.IsNullOrWhiteSpace(legacy.PaneBackground)
               || string.IsNullOrWhiteSpace(legacy.SurfaceBackground)
               || string.IsNullOrWhiteSpace(legacy.SurfaceHover)
               || string.IsNullOrWhiteSpace(legacy.SurfacePressed)
               || string.IsNullOrWhiteSpace(legacy.SurfaceRaised)
               || string.IsNullOrWhiteSpace(legacy.SelectionBackground)
               || string.IsNullOrWhiteSpace(legacy.SelectionBorder)
               || string.IsNullOrWhiteSpace(legacy.TextSelectionBrush)
               || string.IsNullOrWhiteSpace(legacy.EditorTextSelectionBrush)
               || string.IsNullOrWhiteSpace(legacy.BorderBase)
               || string.IsNullOrWhiteSpace(legacy.FocusBorder)
               || string.IsNullOrWhiteSpace(legacy.PrimaryText)
               || string.IsNullOrWhiteSpace(legacy.SecondaryText)
               || string.IsNullOrWhiteSpace(legacy.MutedText)
               || string.IsNullOrWhiteSpace(legacy.PlaceholderText)
               || string.IsNullOrWhiteSpace(legacy.EditorText)
               || string.IsNullOrWhiteSpace(legacy.AppText)
               || string.IsNullOrWhiteSpace(legacy.MarkdownHeading1)
               || string.IsNullOrWhiteSpace(legacy.MarkdownHeading2)
               || string.IsNullOrWhiteSpace(legacy.MarkdownHeading3)
               || string.IsNullOrWhiteSpace(legacy.MarkdownLinkLabel)
               || string.IsNullOrWhiteSpace(legacy.MarkdownLinkUrl)
               || string.IsNullOrWhiteSpace(legacy.MarkdownTaskDone)
               || string.IsNullOrWhiteSpace(legacy.MarkdownTaskPending)
               || string.IsNullOrWhiteSpace(legacy.MarkdownStrikethrough)
               || string.IsNullOrWhiteSpace(legacy.MarkdownRule)
               || string.IsNullOrWhiteSpace(legacy.MarkdownBlockquote)
               || string.IsNullOrWhiteSpace(legacy.MarkdownFenceMarker)
               || string.IsNullOrWhiteSpace(legacy.MarkdownFenceInfo)
               || string.IsNullOrWhiteSpace(legacy.MarkdownInlineCodeForeground)
               || string.IsNullOrWhiteSpace(legacy.MarkdownInlineCodeBackground)
               || string.IsNullOrWhiteSpace(legacy.MarkdownCodeBlockForeground)
               || string.IsNullOrWhiteSpace(legacy.MarkdownCodeBlockBackground)
               || string.IsNullOrWhiteSpace(legacy.TitleBarButtonHover)
               || string.IsNullOrWhiteSpace(legacy.TitleBarCloseHover);
    }

    private static bool AreAllColorsValid(AppTheme theme)
    {
        try
        {
            var tokens = ThemeBuilder.BuildTokens(theme);
            string[] colorValues =
            [
                tokens.AppBackground,
                tokens.PaneBackground,
                tokens.SurfaceBackground,
                tokens.SurfaceHover,
                tokens.SurfacePressed,
                tokens.SurfaceRaised,
                tokens.SelectionBackground,
                tokens.SelectionBorder,
                tokens.TextSelectionBrush,
                tokens.EditorTextSelectionBrush,
                tokens.BorderBase,
                tokens.FocusBorder,
                tokens.PrimaryText,
                tokens.SecondaryText,
                tokens.MutedText,
                tokens.PlaceholderText,
                tokens.EditorText,
                tokens.AppText,
                tokens.MarkdownHeading1,
                tokens.MarkdownHeading2,
                tokens.MarkdownHeading3,
                tokens.MarkdownLinkLabel,
                tokens.MarkdownLinkUrl,
                tokens.MarkdownTaskDone,
                tokens.MarkdownTaskPending,
                tokens.MarkdownStrikethrough,
                tokens.MarkdownRule,
                tokens.MarkdownBlockquote,
                tokens.MarkdownFenceMarker,
                tokens.MarkdownFenceInfo,
                tokens.MarkdownInlineCodeForeground,
                tokens.MarkdownInlineCodeBackground,
                tokens.MarkdownCodeBlockForeground,
                tokens.MarkdownCodeBlockBackground,
                tokens.TitleBarButtonHover,
                tokens.TitleBarCloseHover,
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
        catch
        {
            return false;
        }
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

    private sealed record ThemeDocumentV2(string Name, bool IsLight, ThemePalette Palette, ThemeTokenOverrides? Overrides);

    private sealed class LegacyThemeDocument
    {
        public string? Name { get; set; }
        public bool? IsLight { get; set; }
        public string? AppBackground { get; set; }
        public string? PaneBackground { get; set; }
        public string? SurfaceBackground { get; set; }
        public string? SurfaceHover { get; set; }
        public string? SurfacePressed { get; set; }
        public string? SurfaceRaised { get; set; }
        public string? SelectionBackground { get; set; }
        public string? SelectionBorder { get; set; }
        public string? TextSelectionBrush { get; set; }
        public string? EditorTextSelectionBrush { get; set; }
        public string? BorderBase { get; set; }
        public string? FocusBorder { get; set; }
        public string? PrimaryText { get; set; }
        public string? SecondaryText { get; set; }
        public string? MutedText { get; set; }
        public string? PlaceholderText { get; set; }
        public string? EditorText { get; set; }
        public string? AppText { get; set; }
        public string? MarkdownHeading1 { get; set; }
        public string? MarkdownHeading2 { get; set; }
        public string? MarkdownHeading3 { get; set; }
        public string? MarkdownLinkLabel { get; set; }
        public string? MarkdownLinkUrl { get; set; }
        public string? MarkdownTaskDone { get; set; }
        public string? MarkdownTaskPending { get; set; }
        public string? MarkdownStrikethrough { get; set; }
        public string? MarkdownRule { get; set; }
        public string? MarkdownBlockquote { get; set; }
        public string? MarkdownFenceMarker { get; set; }
        public string? MarkdownFenceInfo { get; set; }
        public string? MarkdownInlineCodeForeground { get; set; }
        public string? MarkdownInlineCodeBackground { get; set; }
        public string? MarkdownCodeBlockForeground { get; set; }
        public string? MarkdownCodeBlockBackground { get; set; }
        public string? TitleBarButtonHover { get; set; }
        public string? TitleBarCloseHover { get; set; }
    }
}
