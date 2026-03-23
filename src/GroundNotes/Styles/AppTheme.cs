namespace GroundNotes.Styles;

/// <summary>
/// Defines a theme in terms of a compact palette plus optional semantic overrides.
/// Runtime resources are produced by <see cref="ThemeBuilder"/>.
/// </summary>
public sealed class AppTheme
{
    public required string Name { get; init; }
    public required bool IsLight { get; init; }
    public required ThemePalette Palette { get; init; }
    public ThemeTokenOverrides? Overrides { get; init; }

    public static AppTheme Dark { get; } = new()
    {
        Name = "Dark",
        IsLight = false,
        Palette = new ThemePalette
        {
            AppBackground = "#1E1E1E",
            PaneBackground = "#252526",
            SurfaceBackground = "#1E1E1E",
            SurfaceHover = "#2A2D2E",
            SurfacePressed = "#1A1A1A",
            SurfaceRaised = "#2D2D2D",
            BorderBase = "#3C3C3C",
            PrimaryText = "#D4D4D4",
            SecondaryText = "#9D9D9D",
            MutedText = "#6A6A6A",
            PlaceholderText = "#5A5A5A",
            Accent = "#4AA3FF",
            AccentSoft = "#7FB0D9",
            SelectionBackground = "#264F78",
            TextSelectionBrush = "#264F78",
            EditorTextSelectionBrush = "#66007ACC",
            Success = "#6CB7A8",
            Warning = "#D1B86F",
            Danger = "#E81123",
        },
        Overrides = new ThemeTokenOverrides
        {
            SelectionBorder = "#007ACC",
            FocusBorder = "#007ACC",
            AppText = "#CCCCCC",
            MarkdownHeading2 = "#6CB7A8",
            MarkdownHeading3 = "#8FB7A3",
            MarkdownLinkLabel = "#67B7FF",
            MarkdownTaskDone = "#7DAA88",
            MarkdownRule = "#4C5662",
            MarkdownBlockquote = "#A5B6C7",
            MarkdownFenceMarker = "#5D7185",
            MarkdownFenceInfo = "#7FB0D9",
            MarkdownInlineCodeForeground = "#7CC0FF",
            MarkdownInlineCodeBackground = "#1A2530",
            MarkdownCodeBlockForeground = "#A9D3FF",
            MarkdownCodeBlockBackground = "#18222B",
            TitleBarButtonHover = "#333333",
        }
    };

    public static AppTheme Light { get; } = new()
    {
        Name = "Light",
        IsLight = true,
        Palette = new ThemePalette
        {
            AppBackground = "#F5F5F5",
            PaneBackground = "#FFFFFF",
            SurfaceBackground = "#F5F5F5",
            SurfaceHover = "#EAEAEA",
            SurfacePressed = "#DEDEDE",
            SurfaceRaised = "#EFEFEF",
            BorderBase = "#C8C8C8",
            PrimaryText = "#1B1B1B",
            SecondaryText = "#5A5A5A",
            MutedText = "#8A8A8A",
            PlaceholderText = "#9E9E9E",
            Accent = "#005FB8",
            AccentSoft = "#4C647A",
            SelectionBackground = "#CCE5FF",
            TextSelectionBrush = "#ADD6FF",
            EditorTextSelectionBrush = "#665C86B8",
            Success = "#356859",
            Warning = "#7A5C00",
            Danger = "#C42B1C",
        },
        Overrides = new ThemeTokenOverrides
        {
            SelectionBorder = "#0078D4",
            MarkdownHeading2 = "#7A5C00",
            MarkdownHeading3 = "#356859",
            MarkdownRule = "#B8C2CC",
            MarkdownBlockquote = "#4C647A",
            MarkdownFenceMarker = "#7F8B96",
            MarkdownFenceInfo = "#356859",
            MarkdownInlineCodeForeground = "#004B91",
            MarkdownInlineCodeBackground = "#E7EEF5",
            MarkdownCodeBlockForeground = "#1E3A5F",
            MarkdownCodeBlockBackground = "#E1E8EF",
            TitleBarButtonHover = "#E5E5E5",
        }
    };

    public static AppTheme AmoledBlack { get; } = new()
    {
        Name = "Amoled Black",
        IsLight = false,
        Palette = new ThemePalette
        {
            AppBackground = "#000000",
            PaneBackground = "#050505",
            SurfaceBackground = "#000000",
            SurfaceHover = "#0A0A0A",
            SurfacePressed = "#111111",
            SurfaceRaised = "#0D0D0D",
            BorderBase = "#1E1E1E",
            PrimaryText = "#E8E8E8",
            SecondaryText = "#A0A0A0",
            MutedText = "#6E6E6E",
            PlaceholderText = "#5E5E5E",
            Accent = "#E0A15C",
            AccentSoft = "#D9B77D",
            SelectionBackground = "#1A1A1A",
            TextSelectionBrush = "#222222",
            EditorTextSelectionBrush = "#55E0A15C",
            Success = "#9FBF9F",
            Warning = "#D3A96B",
            Danger = "#8B1A1A",
        },
        Overrides = new ThemeTokenOverrides
        {
            SelectionBorder = "#888888",
            FocusBorder = "#999999",
            MarkdownHeading2 = "#B88A52",
            MarkdownHeading3 = "#9FBF9F",
            MarkdownLinkLabel = "#F0C68A",
            MarkdownBlockquote = "#BFB7A6",
            MarkdownFenceMarker = "#6A625A",
            MarkdownInlineCodeForeground = "#F0C68A",
            MarkdownInlineCodeBackground = "#111111",
            MarkdownCodeBlockForeground = "#F4D4A6",
            MarkdownCodeBlockBackground = "#141414",
            TitleBarButtonHover = "#141414",
        }
    };

    public static AppTheme Claude { get; } = new()
    {
        Name = "Claude",
        IsLight = false,
        Palette = new ThemePalette
        {
            AppBackground = "#262624",
            PaneBackground = "#30302E",
            SurfaceBackground = "#262624",
            SurfaceHover = "#3A3A38",
            SurfacePressed = "#1F1F1D",
            SurfaceRaised = "#333333",
            BorderBase = "#3D3D3D",
            PrimaryText = "#F0F0F0",
            SecondaryText = "#CCCCCC",
            MutedText = "#999999",
            PlaceholderText = "#808080",
            Accent = "#DA7756",
            AccentSoft = "#E3B980",
            SelectionBackground = "#3D2A22",
            TextSelectionBrush = "#4A3028",
            EditorTextSelectionBrush = "#66DA7756",
            Success = "#C9B07A",
            Warning = "#DA9A56",
            Danger = "#8B2E1A",
        },
        Overrides = new ThemeTokenOverrides
        {
            MarkdownHeading2 = "#C9B07A",
            MarkdownHeading3 = "#C9B07A",
            MarkdownLinkLabel = "#E3B980",
            MarkdownLinkUrl = "#B7A28E",
            MarkdownTaskDone = "#C9B07A",
            MarkdownStrikethrough = "#9D9488",
            MarkdownRule = "#4A433E",
            MarkdownBlockquote = "#D3C6B4",
            MarkdownFenceMarker = "#8B7C72",
            MarkdownFenceInfo = "#E3B980",
            MarkdownInlineCodeForeground = "#E3B980",
            MarkdownInlineCodeBackground = "#3A312D",
            MarkdownCodeBlockForeground = "#E9CFA1",
            MarkdownCodeBlockBackground = "#35302C",
            TitleBarButtonHover = "#3A3A38",
        }
    };

    public static AppTheme CatppuccinMochaMidnight { get; } = new()
    {
        Name = "Catppuccin Mocha Midnight",
        IsLight = false,
        Palette = new ThemePalette
        {
            AppBackground = "#0F1118",
            PaneBackground = "#141823",
            SurfaceBackground = "#10131C",
            SurfaceHover = "#1A2030",
            SurfacePressed = "#0C0F16",
            SurfaceRaised = "#181D2A",
            BorderBase = "#2A3144",
            PrimaryText = "#D9E0EE",
            SecondaryText = "#AEB8D0",
            MutedText = "#7E89A6",
            PlaceholderText = "#626C86",
            Accent = "#CBA6F7",
            AccentSoft = "#F5C2E7",
            SelectionBackground = "#342A46",
            TextSelectionBrush = "#342A46",
            EditorTextSelectionBrush = "#66CBA6F7",
            Success = "#A6E3A1",
            Warning = "#F9E2AF",
            Danger = "#F38BA8",
        },
        Overrides = new ThemeTokenOverrides
        {
            SelectionBorder = "#F5C2E7",
            FocusBorder = "#CBA6F7",
            MarkdownHeading1 = "#CBA6F7",
            MarkdownHeading2 = "#F5C2E7",
            MarkdownHeading3 = "#F2CDCD",
            MarkdownLinkLabel = "#F5C2E7",
            MarkdownLinkUrl = "#B9A1D2",
            MarkdownTaskDone = "#A6E3A1",
            MarkdownTaskPending = "#F9E2AF",
            MarkdownStrikethrough = "#6C7690",
            MarkdownRule = "#3C3550",
            MarkdownBlockquote = "#D6C6E7",
            MarkdownFenceMarker = "#9B88B8",
            MarkdownFenceInfo = "#F5C2E7",
            MarkdownInlineCodeForeground = "#F5C2E7",
            MarkdownInlineCodeBackground = "#211B2C",
            MarkdownCodeBlockForeground = "#CDD6F4",
            MarkdownCodeBlockBackground = "#1B1625",
            TitleBarButtonHover = "#221C2E",
            TitleBarCloseHover = "#E06C92",
        }
    };

    public static AppTheme FlexokiLight { get; } = new()
    {
        Name = "Flexoki Light",
        IsLight = true,
        Palette = new ThemePalette
        {
            AppBackground = "#FFFCF0",
            PaneBackground = "#F2F0E5",
            SurfaceBackground = "#FFFCF0",
            SurfaceHover = "#E6E4D9",
            SurfacePressed = "#DAD8CE",
            SurfaceRaised = "#F2F0E5",
            BorderBase = "#E6E4D9",
            PrimaryText = "#100F0F",
            SecondaryText = "#6F6E69",
            MutedText = "#878580",
            PlaceholderText = "#B7B5AC",
            Accent = "#205EA6",
            AccentSoft = "#AD8301",
            SelectionBackground = "#E0DDD3",
            TextSelectionBrush = "#DAD8CE",
            EditorTextSelectionBrush = "#66AD8301",
            Success = "#66800B",
            Warning = "#AD8301",
            Danger = "#AF3029",
        },
        Overrides = new ThemeTokenOverrides
        {
            MarkdownHeading1 = "#AF3029",
            MarkdownHeading2 = "#AD8301",
            MarkdownHeading3 = "#66800B",
            MarkdownRule = "#DAD8CE",
            MarkdownBlockquote = "#575653",
            MarkdownFenceMarker = "#B7B5AC",
            MarkdownFenceInfo = "#205EA6",
            MarkdownInlineCodeForeground = "#205EA6",
            MarkdownInlineCodeBackground = "#ECE8D8",
            MarkdownCodeBlockForeground = "#1C4E80",
            MarkdownCodeBlockBackground = "#E6E1CF",
            TitleBarButtonHover = "#E6E4D9",
        }
    };

    /// <summary>
    /// Returns all built-in themes.
    /// </summary>
    public static IReadOnlyList<AppTheme> BuiltInThemes { get; } = [Dark, Claude, CatppuccinMochaMidnight, AmoledBlack, Light, FlexokiLight];
}
