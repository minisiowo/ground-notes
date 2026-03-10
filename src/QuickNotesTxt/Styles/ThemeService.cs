using Avalonia;
using Avalonia.Media;

namespace QuickNotesTxt.Styles;

/// <summary>
/// Applies an <see cref="AppTheme"/> to the running application by updating
/// <see cref="Application.Resources"/> with <see cref="SolidColorBrush"/> instances.
/// Because styles use <c>DynamicResource</c>, changes take effect immediately.
/// </summary>
public static class ThemeService
{
    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        Set(app, "AppBackgroundBrush", theme.AppBackground);
        Set(app, "PaneBackgroundBrush", theme.PaneBackground);
        Set(app, "SurfaceBackgroundBrush", theme.SurfaceBackground);
        Set(app, "SurfaceHoverBrush", theme.SurfaceHover);
        Set(app, "SurfacePressedBrush", theme.SurfacePressed);
        Set(app, "SurfaceRaisedBrush", theme.SurfaceRaised);
        Set(app, "SelectionBackgroundBrush", theme.SelectionBackground);
        Set(app, "SelectionBorderBrush", theme.SelectionBorder);
        Set(app, "TextSelectionBrush", theme.TextSelectionBrush);
        Set(app, "EditorTextSelectionBrush", theme.EditorTextSelectionBrush);
        Set(app, "BorderBrushBase", theme.BorderBase);
        Set(app, "FocusBorderBrush", theme.FocusBorder);
        Set(app, "PrimaryTextBrush", theme.PrimaryText);
        Set(app, "SecondaryTextBrush", theme.SecondaryText);
        Set(app, "MutedTextBrush", theme.MutedText);
        Set(app, "PlaceholderTextBrush", theme.PlaceholderText);
        Set(app, "EditorTextBrush", theme.EditorText);
        Set(app, "AppTextBrush", theme.AppText);
        Set(app, "TitleBarButtonHoverBrush", theme.TitleBarButtonHover);
        Set(app, "TitleBarCloseHoverBrush", theme.TitleBarCloseHover);
    }

    private static void Set(Application app, string key, string hex)
    {
        var brush = new SolidColorBrush(Color.Parse(hex));
        app.Resources[key] = brush;
    }
}
