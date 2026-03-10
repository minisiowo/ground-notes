namespace QuickNotesTxt.Styles;

/// <summary>
/// Defines all color tokens for a QuickNotesTxt theme.
/// To create a custom theme, copy an existing one and change the hex values.
/// </summary>
public sealed class AppTheme
{
    public required string Name { get; init; }

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

    // ── Title bar ────────────────────────────────────────────
    public required string TitleBarButtonHover { get; init; }
    public required string TitleBarCloseHover { get; init; }

    // ── Built-in themes ──────────────────────────────────────

    public static AppTheme Dark { get; } = new()
    {
        Name = "Dark",
        AppBackground = "#1E1E1E",
        PaneBackground = "#252526",
        SurfaceBackground = "#1E1E1E",
        SurfaceHover = "#2A2D2E",
        SurfacePressed = "#1A1A1A",
        SurfaceRaised = "#2D2D2D",
        SelectionBackground = "#264F78",
        SelectionBorder = "#007ACC",
        TextSelectionBrush = "#264F78",
        EditorTextSelectionBrush = "#264F78",
        BorderBase = "#3C3C3C",
        FocusBorder = "#007ACC",
        PrimaryText = "#D4D4D4",
        SecondaryText = "#9D9D9D",
        MutedText = "#6A6A6A",
        PlaceholderText = "#5A5A5A",
        EditorText = "#D4D4D4",
        AppText = "#CCCCCC",
        TitleBarButtonHover = "#333333",
        TitleBarCloseHover = "#E81123",
    };

    public static AppTheme Light { get; } = new()
    {
        Name = "Light",
        AppBackground = "#F5F5F5",
        PaneBackground = "#FFFFFF",
        SurfaceBackground = "#F5F5F5",
        SurfaceHover = "#EAEAEA",
        SurfacePressed = "#DEDEDE",
        SurfaceRaised = "#EFEFEF",
        SelectionBackground = "#CCE5FF",
        SelectionBorder = "#0078D4",
        TextSelectionBrush = "#ADD6FF",
        EditorTextSelectionBrush = "#ADD6FF",
        BorderBase = "#C8C8C8",
        FocusBorder = "#005FB8",
        PrimaryText = "#1B1B1B",
        SecondaryText = "#5A5A5A",
        MutedText = "#8A8A8A",
        PlaceholderText = "#9E9E9E",
        EditorText = "#1B1B1B",
        AppText = "#1B1B1B",
        TitleBarButtonHover = "#E5E5E5",
        TitleBarCloseHover = "#C42B1C",
    };

    public static AppTheme Nord { get; } = new()
    {
        Name = "Nord",
        AppBackground = "#2E3440",
        PaneBackground = "#3B4252",
        SurfaceBackground = "#2E3440",
        SurfaceHover = "#434C5E",
        SurfacePressed = "#272C36",
        SurfaceRaised = "#3B4252",
        SelectionBackground = "#3B4F6E",
        SelectionBorder = "#88C0D0",
        TextSelectionBrush = "#3B4F6E",
        EditorTextSelectionBrush = "#4C6076",
        BorderBase = "#4C566A",
        FocusBorder = "#81A1C1",
        PrimaryText = "#ECEFF4",
        SecondaryText = "#D8DEE9",
        MutedText = "#8791A5",
        PlaceholderText = "#7B8394",
        EditorText = "#ECEFF4",
        AppText = "#ECEFF4",
        TitleBarButtonHover = "#434C5E",
        TitleBarCloseHover = "#BF616A",
    };

    public static AppTheme AmoledBlack { get; } = new()
    {
        Name = "Amoled Black",
        AppBackground = "#000000",
        PaneBackground = "#050505",
        SurfaceBackground = "#000000",
        SurfaceHover = "#0A0A0A",
        SurfacePressed = "#111111",
        SurfaceRaised = "#0D0D0D",
        SelectionBackground = "#1A1A1A",
        SelectionBorder = "#888888",
        TextSelectionBrush = "#222222",
        EditorTextSelectionBrush = "#2A2A2A",
        BorderBase = "#1E1E1E",
        FocusBorder = "#999999",
        PrimaryText = "#E8E8E8",
        SecondaryText = "#A0A0A0",
        MutedText = "#6E6E6E",
        PlaceholderText = "#5E5E5E",
        EditorText = "#E8E8E8",
        AppText = "#E8E8E8",
        TitleBarButtonHover = "#141414",
        TitleBarCloseHover = "#8B1A1A",
    };

    public static AppTheme Claude { get; } = new()
    {
        Name = "Claude",
        AppBackground = "#262624",
        PaneBackground = "#30302E",
        SurfaceBackground = "#262624",
        SurfaceHover = "#3A3A38",
        SurfacePressed = "#1F1F1D",
        SurfaceRaised = "#333333",
        SelectionBackground = "#3D2A22",
        SelectionBorder = "#DA7756",
        TextSelectionBrush = "#4A3028",
        EditorTextSelectionBrush = "#553A30",
        BorderBase = "#3D3D3D",
        FocusBorder = "#DA7756",
        PrimaryText = "#F0F0F0",
        SecondaryText = "#CCCCCC",
        MutedText = "#999999",
        PlaceholderText = "#808080",
        EditorText = "#F0F0F0",
        AppText = "#F0F0F0",
        TitleBarButtonHover = "#3A3A38",
        TitleBarCloseHover = "#8B2E1A",
    };

    /// <summary>
    /// Returns all built-in themes.
    /// </summary>
    public static IReadOnlyList<AppTheme> BuiltInThemes { get; } = [Dark, Light, Nord, AmoledBlack, Claude];
}
