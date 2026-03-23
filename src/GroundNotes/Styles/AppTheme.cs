namespace GroundNotes.Styles;

/// <summary>
/// Defines all color tokens for a GroundNotes theme.
/// To create a custom theme, copy an existing one and change the hex values.
/// </summary>
public sealed class AppTheme
{
    public required string Name { get; init; }
    public required bool IsLight { get; init; }

    // ── Backgrounds ──────────────────────────────────────────
    public required string AppBackground { get; init; }
    public required string PaneBackground { get; init; }
    public required string SurfaceBackground { get; init; }
    public required string SurfaceHover { get; init; }
    public required string SurfacePressed { get; init; }
    public required string SurfaceRaised { get; init; }

    // ── Selection ────────────────────────────────────────────
    public required string SelectionBackground { get; init; }
    public required string SelectionBorder { get; init; }
    public required string TextSelectionBrush { get; init; }
    public required string EditorTextSelectionBrush { get; init; }

    // ── Borders ──────────────────────────────────────────────
    public required string BorderBase { get; init; }
    public required string FocusBorder { get; init; }

    // ── Text ─────────────────────────────────────────────────
    public required string PrimaryText { get; init; }
    public required string SecondaryText { get; init; }
    public required string MutedText { get; init; }
    public required string PlaceholderText { get; init; }
    public required string EditorText { get; init; }
    public required string AppText { get; init; }
    public required string MarkdownHeading1 { get; init; }
    public required string MarkdownHeading2 { get; init; }
    public required string MarkdownHeading3 { get; init; }
    public required string MarkdownLinkLabel { get; init; }
    public required string MarkdownLinkUrl { get; init; }
    public required string MarkdownTaskDone { get; init; }
    public required string MarkdownTaskPending { get; init; }
    public required string MarkdownStrikethrough { get; init; }
    public required string MarkdownRule { get; init; }
    public required string MarkdownBlockquote { get; init; }
    public required string MarkdownFenceMarker { get; init; }
    public required string MarkdownFenceInfo { get; init; }
    public required string MarkdownInlineCodeForeground { get; init; }
    public required string MarkdownInlineCodeBackground { get; init; }
    public required string MarkdownCodeBlockForeground { get; init; }
    public required string MarkdownCodeBlockBackground { get; init; }

    // ── Title bar ────────────────────────────────────────────
    public required string TitleBarButtonHover { get; init; }
    public required string TitleBarCloseHover { get; init; }

    // ── Built-in themes ──────────────────────────────────────

    public static AppTheme Dark { get; } = new()
    {
        Name = "Dark",
        IsLight = false,
        AppBackground = "#1E1E1E",
        PaneBackground = "#252526",
        SurfaceBackground = "#1E1E1E",
        SurfaceHover = "#2A2D2E",
        SurfacePressed = "#1A1A1A",
        SurfaceRaised = "#2D2D2D",
        SelectionBackground = "#264F78",
        SelectionBorder = "#007ACC",
        TextSelectionBrush = "#264F78",
        EditorTextSelectionBrush = "#66007ACC",
        BorderBase = "#3C3C3C",
        FocusBorder = "#007ACC",
        PrimaryText = "#D4D4D4",
        SecondaryText = "#9D9D9D",
        MutedText = "#6A6A6A",
        PlaceholderText = "#5A5A5A",
        EditorText = "#D4D4D4",
        AppText = "#CCCCCC",
        MarkdownHeading1 = "#4AA3FF",
        MarkdownHeading2 = "#6CB7A8",
        MarkdownHeading3 = "#8FB7A3",
        MarkdownLinkLabel = "#67B7FF",
        MarkdownLinkUrl = "#8899AA",
        MarkdownTaskDone = "#7DAA88",
        MarkdownTaskPending = "#D1B86F",
        MarkdownStrikethrough = "#7E7E7E",
        MarkdownRule = "#4C5662",
        MarkdownBlockquote = "#A5B6C7",
        MarkdownFenceMarker = "#5D7185",
        MarkdownFenceInfo = "#7FB0D9",
        MarkdownInlineCodeForeground = "#7CC0FF",
        MarkdownInlineCodeBackground = "#1A2530",
        MarkdownCodeBlockForeground = "#A9D3FF",
        MarkdownCodeBlockBackground = "#18222B",
        TitleBarButtonHover = "#333333",
        TitleBarCloseHover = "#E81123",
    };

    public static AppTheme Light { get; } = new()
    {
        Name = "Light",
        IsLight = true,
        AppBackground = "#F5F5F5",
        PaneBackground = "#FFFFFF",
        SurfaceBackground = "#F5F5F5",
        SurfaceHover = "#EAEAEA",
        SurfacePressed = "#DEDEDE",
        SurfaceRaised = "#EFEFEF",
        SelectionBackground = "#CCE5FF",
        SelectionBorder = "#0078D4",
        TextSelectionBrush = "#ADD6FF",
        EditorTextSelectionBrush = "#665C86B8",
        BorderBase = "#C8C8C8",
        FocusBorder = "#005FB8",
        PrimaryText = "#1B1B1B",
        SecondaryText = "#5A5A5A",
        MutedText = "#8A8A8A",
        PlaceholderText = "#9E9E9E",
        EditorText = "#1B1B1B",
        AppText = "#1B1B1B",
        MarkdownHeading1 = "#005FB8",
        MarkdownHeading2 = "#7A5C00",
        MarkdownHeading3 = "#356859",
        MarkdownLinkLabel = "#005FB8",
        MarkdownLinkUrl = "#5A5A5A",
        MarkdownTaskDone = "#356859",
        MarkdownTaskPending = "#7A5C00",
        MarkdownStrikethrough = "#757575",
        MarkdownRule = "#B8C2CC",
        MarkdownBlockquote = "#4C647A",
        MarkdownFenceMarker = "#7F8B96",
        MarkdownFenceInfo = "#356859",
        MarkdownInlineCodeForeground = "#004B91",
        MarkdownInlineCodeBackground = "#E7EEF5",
        MarkdownCodeBlockForeground = "#1E3A5F",
        MarkdownCodeBlockBackground = "#E1E8EF",
        TitleBarButtonHover = "#E5E5E5",
        TitleBarCloseHover = "#C42B1C",
    };

    public static AppTheme AmoledBlack { get; } = new()
    {
        Name = "Amoled Black",
        IsLight = false,
        AppBackground = "#000000",
        PaneBackground = "#050505",
        SurfaceBackground = "#000000",
        SurfaceHover = "#0A0A0A",
        SurfacePressed = "#111111",
        SurfaceRaised = "#0D0D0D",
        SelectionBackground = "#1A1A1A",
        SelectionBorder = "#888888",
        TextSelectionBrush = "#222222",
        EditorTextSelectionBrush = "#55E0A15C",
        BorderBase = "#1E1E1E",
        FocusBorder = "#999999",
        PrimaryText = "#E8E8E8",
        SecondaryText = "#A0A0A0",
        MutedText = "#6E6E6E",
        PlaceholderText = "#5E5E5E",
        EditorText = "#E8E8E8",
        AppText = "#E8E8E8",
        MarkdownHeading1 = "#E0A15C",
        MarkdownHeading2 = "#B88A52",
        MarkdownHeading3 = "#9FBF9F",
        MarkdownLinkLabel = "#F0C68A",
        MarkdownLinkUrl = "#8C8C8C",
        MarkdownTaskDone = "#9FBF9F",
        MarkdownTaskPending = "#D3A96B",
        MarkdownStrikethrough = "#707070",
        MarkdownRule = "#242424",
        MarkdownBlockquote = "#BFB7A6",
        MarkdownFenceMarker = "#6A625A",
        MarkdownFenceInfo = "#D9B77D",
        MarkdownInlineCodeForeground = "#F0C68A",
        MarkdownInlineCodeBackground = "#111111",
        MarkdownCodeBlockForeground = "#F4D4A6",
        MarkdownCodeBlockBackground = "#141414",
        TitleBarButtonHover = "#141414",
        TitleBarCloseHover = "#8B1A1A",
    };

    public static AppTheme Claude { get; } = new()
    {
        Name = "Claude",
        IsLight = false,
        AppBackground = "#262624",
        PaneBackground = "#30302E",
        SurfaceBackground = "#262624",
        SurfaceHover = "#3A3A38",
        SurfacePressed = "#1F1F1D",
        SurfaceRaised = "#333333",
        SelectionBackground = "#3D2A22",
        SelectionBorder = "#DA7756",
        TextSelectionBrush = "#4A3028",
        EditorTextSelectionBrush = "#66DA7756",
        BorderBase = "#3D3D3D",
        FocusBorder = "#DA7756",
        PrimaryText = "#F0F0F0",
        SecondaryText = "#CCCCCC",
        MutedText = "#999999",
        PlaceholderText = "#808080",
        EditorText = "#F0F0F0",
        AppText = "#F0F0F0",
        MarkdownHeading1 = "#DA7756",
        MarkdownHeading2 = "#C9B07A",
        MarkdownHeading3 = "#C9B07A",
        MarkdownLinkLabel = "#E3B980",
        MarkdownLinkUrl = "#AFA79B",
        MarkdownTaskDone = "#C9B07A",
        MarkdownTaskPending = "#DA9A56",
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
        TitleBarCloseHover = "#8B2E1A",
    };

    public static AppTheme FlexokiLight { get; } = new()
    {
        Name = "Flexoki Light",
        IsLight = true,
        AppBackground = "#FFFCF0",
        PaneBackground = "#F2F0E5",
        SurfaceBackground = "#FFFCF0",
        SurfaceHover = "#E6E4D9",
        SurfacePressed = "#DAD8CE",
        SurfaceRaised = "#F2F0E5",
        SelectionBackground = "#E0DDD3",
        SelectionBorder = "#205EA6",
        TextSelectionBrush = "#DAD8CE",
        EditorTextSelectionBrush = "#66AD8301",
        BorderBase = "#E6E4D9",
        FocusBorder = "#205EA6",
        PrimaryText = "#100F0F",
        SecondaryText = "#6F6E69",
        MutedText = "#878580",
        PlaceholderText = "#B7B5AC",
        EditorText = "#100F0F",
        AppText = "#100F0F",
        MarkdownHeading1 = "#AF3029",
        MarkdownHeading2 = "#AD8301",
        MarkdownHeading3 = "#66800B",
        MarkdownLinkLabel = "#205EA6",
        MarkdownLinkUrl = "#6F6E69",
        MarkdownTaskDone = "#66800B",
        MarkdownTaskPending = "#AD8301",
        MarkdownStrikethrough = "#878580",
        MarkdownRule = "#DAD8CE",
        MarkdownBlockquote = "#575653",
        MarkdownFenceMarker = "#B7B5AC",
        MarkdownFenceInfo = "#205EA6",
        MarkdownInlineCodeForeground = "#205EA6",
        MarkdownInlineCodeBackground = "#ECE8D8",
        MarkdownCodeBlockForeground = "#1C4E80",
        MarkdownCodeBlockBackground = "#E6E1CF",
        TitleBarButtonHover = "#E6E4D9",
        TitleBarCloseHover = "#AF3029",
    };

    /// <summary>
    /// Returns all built-in themes.
    /// </summary>
    public static IReadOnlyList<AppTheme> BuiltInThemes { get; } = [Dark, Claude, AmoledBlack, Light, FlexokiLight];
}
