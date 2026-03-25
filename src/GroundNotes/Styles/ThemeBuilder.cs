using Avalonia.Media;

namespace GroundNotes.Styles;

public static class ThemeBuilder
{
    public static ThemeSemanticTokens BuildTokens(AppTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(theme.Palette);

        var palette = theme.Palette;
        var overrides = theme.Overrides;

        return new ThemeSemanticTokens(
            palette.AppBackground,
            palette.PaneBackground,
            palette.SurfaceBackground,
            palette.SurfaceHover,
            palette.SurfacePressed,
            palette.SurfaceRaised,
            palette.SelectionBackground,
            overrides?.SelectionBorder ?? palette.Accent,
            palette.TextSelectionBrush,
            palette.EditorTextSelectionBrush,
            palette.BorderBase,
            overrides?.FocusBorder ?? palette.Accent,
            palette.PrimaryText,
            palette.SecondaryText,
            palette.MutedText,
            palette.PlaceholderText,
            overrides?.EditorText ?? palette.PrimaryText,
            overrides?.AppText ?? palette.PrimaryText,
            overrides?.MarkdownHeading1 ?? palette.Accent,
            overrides?.MarkdownHeading2 ?? palette.Warning,
            overrides?.MarkdownHeading3 ?? Blend(palette.Success, palette.SecondaryText, 0.35),
            overrides?.MarkdownLinkLabel ?? palette.Accent,
            overrides?.MarkdownLinkUrl ?? Blend(palette.SecondaryText, palette.Accent, theme.IsLight ? 0.14 : 0.22),
            overrides?.MarkdownTaskDone ?? palette.Success,
            overrides?.MarkdownTaskPending ?? palette.Warning,
            overrides?.MarkdownStrikethrough ?? palette.MutedText,
            overrides?.MarkdownRule ?? Blend(palette.BorderBase, palette.SecondaryText, theme.IsLight ? 0.08 : 0.16),
            overrides?.MarkdownBlockquote ?? Blend(palette.SecondaryText, palette.AccentSoft, theme.IsLight ? 0.18 : 0.26),
            overrides?.MarkdownFenceMarker ?? Blend(palette.MutedText, palette.AccentSoft, theme.IsLight ? 0.12 : 0.18),
            overrides?.MarkdownFenceInfo ?? Blend(palette.Accent, palette.SecondaryText, theme.IsLight ? 0.18 : 0.30),
            overrides?.MarkdownInlineCodeForeground ?? Blend(palette.Accent, palette.PrimaryText, theme.IsLight ? 0.12 : 0.18),
            overrides?.MarkdownInlineCodeBackground ?? Blend(palette.SurfaceRaised, palette.AccentSoft, theme.IsLight ? 0.10 : 0.18),
            overrides?.MarkdownCodeBlockForeground ?? Blend(palette.PrimaryText, palette.AccentSoft, theme.IsLight ? 0.08 : 0.15),
            overrides?.MarkdownCodeBlockBackground ?? Blend(palette.PaneBackground, palette.AccentSoft, theme.IsLight ? 0.08 : 0.14),
            overrides?.TitleBarButtonHover ?? palette.SurfaceHover,
            overrides?.TitleBarCloseHover ?? palette.Danger,
            Blend(palette.PaneBackground, palette.SurfaceRaised, theme.IsLight ? 0.38 : 0.52));
    }

    public static ThemeResourceSet BuildResources(AppTheme theme)
    {
        var tokens = BuildTokens(theme);
        var brushes = new Dictionary<string, SolidColorBrush>
        {
            [ThemeKeys.AppBackgroundBrush] = ToBrush(tokens.AppBackground),
            [ThemeKeys.PaneBackgroundBrush] = ToBrush(tokens.PaneBackground),
            [ThemeKeys.SurfaceBackgroundBrush] = ToBrush(tokens.SurfaceBackground),
            [ThemeKeys.SurfaceHoverBrush] = ToBrush(tokens.SurfaceHover),
            [ThemeKeys.SurfacePressedBrush] = ToBrush(tokens.SurfacePressed),
            [ThemeKeys.SurfaceRaisedBrush] = ToBrush(tokens.SurfaceRaised),
            [ThemeKeys.SelectionBackgroundBrush] = ToBrush(tokens.SelectionBackground),
            [ThemeKeys.SelectionBorderBrush] = ToBrush(tokens.SelectionBorder),
            [ThemeKeys.TextSelectionBrush] = ToBrush(tokens.TextSelectionBrush),
            [ThemeKeys.EditorTextSelectionBrush] = ToBrush(tokens.EditorTextSelectionBrush),
            [ThemeKeys.BorderBrushBase] = ToBrush(tokens.BorderBase),
            [ThemeKeys.FocusBorderBrush] = ToBrush(tokens.FocusBorder),
            [ThemeKeys.PrimaryTextBrush] = ToBrush(tokens.PrimaryText),
            [ThemeKeys.SecondaryTextBrush] = ToBrush(tokens.SecondaryText),
            [ThemeKeys.MutedTextBrush] = ToBrush(tokens.MutedText),
            [ThemeKeys.PlaceholderTextBrush] = ToBrush(tokens.PlaceholderText),
            [ThemeKeys.EditorTextBrush] = ToBrush(tokens.EditorText),
            [ThemeKeys.AppTextBrush] = ToBrush(tokens.AppText),
            [ThemeKeys.MarkdownHeading1Brush] = ToBrush(tokens.MarkdownHeading1),
            [ThemeKeys.MarkdownHeading2Brush] = ToBrush(tokens.MarkdownHeading2),
            [ThemeKeys.MarkdownHeading3Brush] = ToBrush(tokens.MarkdownHeading3),
            [ThemeKeys.MarkdownLinkLabelBrush] = ToBrush(tokens.MarkdownLinkLabel),
            [ThemeKeys.MarkdownLinkUrlBrush] = ToBrush(tokens.MarkdownLinkUrl),
            [ThemeKeys.MarkdownTaskDoneBrush] = ToBrush(tokens.MarkdownTaskDone),
            [ThemeKeys.MarkdownTaskPendingBrush] = ToBrush(tokens.MarkdownTaskPending),
            [ThemeKeys.MarkdownStrikethroughBrush] = ToBrush(tokens.MarkdownStrikethrough),
            [ThemeKeys.MarkdownRuleBrush] = ToBrush(tokens.MarkdownRule),
            [ThemeKeys.MarkdownBlockquoteBrush] = ToBrush(tokens.MarkdownBlockquote),
            [ThemeKeys.MarkdownFenceMarkerBrush] = ToBrush(tokens.MarkdownFenceMarker),
            [ThemeKeys.MarkdownFenceInfoBrush] = ToBrush(tokens.MarkdownFenceInfo),
            [ThemeKeys.MarkdownInlineCodeForegroundBrush] = ToBrush(tokens.MarkdownInlineCodeForeground),
            [ThemeKeys.MarkdownInlineCodeBackgroundBrush] = ToBrush(tokens.MarkdownInlineCodeBackground),
            [ThemeKeys.MarkdownCodeBlockForegroundBrush] = ToBrush(tokens.MarkdownCodeBlockForeground),
            [ThemeKeys.MarkdownCodeBlockBackgroundBrush] = ToBrush(tokens.MarkdownCodeBlockBackground),
            [ThemeKeys.TitleBarButtonHoverBrush] = ToBrush(tokens.TitleBarButtonHover),
            [ThemeKeys.TitleBarCloseHoverBrush] = ToBrush(tokens.TitleBarCloseHover),
            [ThemeKeys.MenuSurfaceBrush] = ToBrush(tokens.MenuSurface),
        };

        var colors = new Dictionary<string, Color>
        {
            [ThemeKeys.SystemAccentColor] = Color.Parse(tokens.SelectionBorder),
            [ThemeKeys.SystemAccentColorDark1] = Color.Parse(tokens.SelectionBorder),
            [ThemeKeys.SystemAccentColorDark2] = Color.Parse(tokens.SelectionBackground),
            [ThemeKeys.SystemAccentColorLight1] = Color.Parse(tokens.FocusBorder),
            [ThemeKeys.SystemAccentColorLight2] = Color.Parse(tokens.FocusBorder),
            [ThemeKeys.SystemAccentColorLight3] = Color.Parse(tokens.FocusBorder),
        };

        return new ThemeResourceSet
        {
            Brushes = brushes,
            Colors = colors
        };
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        return new SolidColorBrush(Color.Parse(hex));
    }

    internal static string Blend(string baseHex, string mixHex, double mixAmount)
    {
        var baseColor = Color.Parse(baseHex);
        var mixColor = Color.Parse(mixHex);
        var amount = Math.Clamp(mixAmount, 0, 1);

        return Color.FromArgb(
            BlendChannel(baseColor.A, mixColor.A, amount),
            BlendChannel(baseColor.R, mixColor.R, amount),
            BlendChannel(baseColor.G, mixColor.G, amount),
            BlendChannel(baseColor.B, mixColor.B, amount)).ToString();
    }

    private static byte BlendChannel(byte from, byte to, double amount)
    {
        return (byte)Math.Round(from + ((to - from) * amount));
    }
}
