using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace QuickNotesTxt.Styles;

/// <summary>
/// Applies an <see cref="AppTheme"/> to the running application by updating
/// <see cref="Application.Resources"/> with <see cref="SolidColorBrush"/> instances.
/// Because styles use <c>DynamicResource</c>, changes take effect immediately.
/// Also overrides FluentTheme internal resources for full consistency.
/// </summary>
public static class ThemeService
{
    public static void ApplyTerminalFont(FontFamily fontFamily)
        => ApplyTerminalFont(fontFamily, FontWeight.Normal, FontStyle.Normal);

    public static void ApplyTerminalFont(FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Resources[ThemeKeys.TerminalFont] = fontFamily;
        app.Resources[ThemeKeys.TerminalFontWeight] = fontWeight;
        app.Resources[ThemeKeys.TerminalFontStyle] = fontStyle;
    }

    public static void ApplyUiFontSize(double fontSize)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Resources[ThemeKeys.AppFontSize] = fontSize;
        app.Resources[ThemeKeys.AppFontSizeSmall] = Math.Max(9d, fontSize - 1d);
        app.Resources[ThemeKeys.AppFontSizeLarge] = fontSize + 2d;
    }

    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        // Match FluentTheme base variant to our theme
        app.RequestedThemeVariant = theme.IsLight
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        // ── App-level brush resources ────────────────────────
        Set(app, ThemeKeys.AppBackgroundBrush, theme.AppBackground);
        Set(app, ThemeKeys.PaneBackgroundBrush, theme.PaneBackground);
        Set(app, ThemeKeys.SurfaceBackgroundBrush, theme.SurfaceBackground);
        Set(app, ThemeKeys.SurfaceHoverBrush, theme.SurfaceHover);
        Set(app, ThemeKeys.SurfacePressedBrush, theme.SurfacePressed);
        Set(app, ThemeKeys.SurfaceRaisedBrush, theme.SurfaceRaised);
        Set(app, ThemeKeys.SelectionBackgroundBrush, theme.SelectionBackground);
        Set(app, ThemeKeys.SelectionBorderBrush, theme.SelectionBorder);
        Set(app, ThemeKeys.TextSelectionBrush, theme.TextSelectionBrush);
        Set(app, ThemeKeys.EditorTextSelectionBrush, theme.EditorTextSelectionBrush);
        Set(app, ThemeKeys.BorderBrushBase, theme.BorderBase);
        Set(app, ThemeKeys.FocusBorderBrush, theme.FocusBorder);
        Set(app, ThemeKeys.PrimaryTextBrush, theme.PrimaryText);
        Set(app, ThemeKeys.SecondaryTextBrush, theme.SecondaryText);
        Set(app, ThemeKeys.MutedTextBrush, theme.MutedText);
        Set(app, ThemeKeys.PlaceholderTextBrush, theme.PlaceholderText);
        Set(app, ThemeKeys.EditorTextBrush, theme.EditorText);
        Set(app, ThemeKeys.AppTextBrush, theme.AppText);
        Set(app, ThemeKeys.MarkdownHeading1Brush, theme.MarkdownHeading1);
        Set(app, ThemeKeys.MarkdownHeading2Brush, theme.MarkdownHeading2);
        Set(app, ThemeKeys.MarkdownHeading3Brush, theme.MarkdownHeading3);
        Set(app, ThemeKeys.MarkdownInlineCodeForegroundBrush, theme.MarkdownInlineCodeForeground);
        Set(app, ThemeKeys.MarkdownInlineCodeBackgroundBrush, theme.MarkdownInlineCodeBackground);
        Set(app, ThemeKeys.MarkdownCodeBlockForegroundBrush, theme.MarkdownCodeBlockForeground);
        Set(app, ThemeKeys.MarkdownCodeBlockBackgroundBrush, theme.MarkdownCodeBlockBackground);
        Set(app, ThemeKeys.TitleBarButtonHoverBrush, theme.TitleBarButtonHover);
        Set(app, ThemeKeys.TitleBarCloseHoverBrush, theme.TitleBarCloseHover);

        // ── Override FluentTheme internal Color resources ────
        // These are Color values (not brushes) used by FluentTheme control templates
        SetColor(app, ThemeKeys.SystemAccentColor, theme.SelectionBorder);
        SetColor(app, ThemeKeys.SystemAccentColorDark1, theme.SelectionBorder);
        SetColor(app, ThemeKeys.SystemAccentColorDark2, theme.SelectionBackground);
        SetColor(app, ThemeKeys.SystemAccentColorLight1, theme.FocusBorder);
        SetColor(app, ThemeKeys.SystemAccentColorLight2, theme.FocusBorder);
        SetColor(app, ThemeKeys.SystemAccentColorLight3, theme.FocusBorder);

        // ── Override FluentTheme internal Brush resources ────
        // Button
        Set(app, ThemeKeys.ButtonBackground, theme.SurfaceBackground);
        Set(app, ThemeKeys.ButtonBackgroundPointerOver, theme.SurfaceHover);
        Set(app, ThemeKeys.ButtonBackgroundPressed, theme.SurfacePressed);
        Set(app, ThemeKeys.ButtonForeground, theme.AppText);
        Set(app, ThemeKeys.ButtonForegroundPointerOver, theme.AppText);
        Set(app, ThemeKeys.ButtonForegroundPressed, theme.AppText);
        Set(app, ThemeKeys.ButtonBorderBrush, theme.BorderBase);
        Set(app, ThemeKeys.ButtonBorderBrushPointerOver, theme.FocusBorder);
        Set(app, ThemeKeys.ButtonBorderBrushPressed, theme.FocusBorder);

        // TextBox
        Set(app, ThemeKeys.TextControlBackground, theme.SurfaceBackground);
        Set(app, ThemeKeys.TextControlBackgroundPointerOver, theme.SurfaceBackground);
        Set(app, ThemeKeys.TextControlBackgroundFocused, theme.SurfaceBackground);
        Set(app, ThemeKeys.TextControlForeground, theme.AppText);
        Set(app, ThemeKeys.TextControlForegroundPointerOver, theme.AppText);
        Set(app, ThemeKeys.TextControlForegroundFocused, theme.AppText);
        Set(app, ThemeKeys.TextControlBorderBrush, theme.BorderBase);
        Set(app, ThemeKeys.TextControlBorderBrushPointerOver, theme.FocusBorder);
        Set(app, ThemeKeys.TextControlBorderBrushFocused, theme.FocusBorder);
        Set(app, ThemeKeys.TextControlPlaceholderForeground, theme.PlaceholderText);
        Set(app, ThemeKeys.TextControlPlaceholderForegroundPointerOver, theme.PlaceholderText);
        Set(app, ThemeKeys.TextControlPlaceholderForegroundFocused, theme.PlaceholderText);
        Set(app, ThemeKeys.TextControlSelectionHighlightColor, theme.TextSelectionBrush);

        // ComboBox
        Set(app, ThemeKeys.ComboBoxBackground, theme.SurfaceBackground);
        Set(app, ThemeKeys.ComboBoxBackgroundPointerOver, theme.SurfaceBackground);
        Set(app, ThemeKeys.ComboBoxBackgroundPressed, theme.SurfacePressed);
        Set(app, ThemeKeys.ComboBoxForeground, theme.AppText);
        Set(app, ThemeKeys.ComboBoxForegroundPointerOver, theme.AppText);
        Set(app, ThemeKeys.ComboBoxBorderBrush, theme.BorderBase);
        Set(app, ThemeKeys.ComboBoxBorderBrushPointerOver, theme.FocusBorder);
        Set(app, ThemeKeys.ComboBoxBorderBrushPressed, theme.FocusBorder);
        Set(app, ThemeKeys.ComboBoxDropDownBackground, theme.PaneBackground);
        Set(app, ThemeKeys.ComboBoxDropDownBorderBrush, theme.BorderBase);
        Set(app, ThemeKeys.ComboBoxDropDownForeground, theme.AppText);
        app.Resources[ThemeKeys.ComboBoxDropdownBorderPadding] = new Avalonia.Thickness(0);
        app.Resources[ThemeKeys.ComboBoxDropdownContentMargin] = new Avalonia.Thickness(0);
        Set(app, ThemeKeys.ComboBoxItemForeground, theme.AppText);
        Set(app, ThemeKeys.ComboBoxItemForegroundSelected, theme.AppText);
        Set(app, ThemeKeys.ComboBoxItemForegroundPointerOver, theme.AppText);
        Set(app, ThemeKeys.ComboBoxItemBackgroundPointerOver, theme.SurfaceRaised);
        Set(app, ThemeKeys.ComboBoxItemBackgroundSelected, theme.SelectionBackground);
        Set(app, ThemeKeys.ComboBoxItemBackgroundSelectedPointerOver, theme.SelectionBackground);

        // ListBoxItem (FluentTheme uses ListViewItem resources)
        Set(app, ThemeKeys.ListViewItemBackgroundPointerOver, theme.SurfaceRaised);
        Set(app, ThemeKeys.ListViewItemBackgroundSelected, theme.SelectionBackground);
        Set(app, ThemeKeys.ListViewItemBackgroundSelectedPointerOver, theme.SelectionBackground);
        Set(app, ThemeKeys.ListViewItemForeground, theme.AppText);
        Set(app, ThemeKeys.ListViewItemForegroundPointerOver, theme.AppText);
        Set(app, ThemeKeys.ListViewItemForegroundSelected, theme.AppText);

        // MenuItem / ContextMenu
        Set(app, ThemeKeys.MenuFlyoutPresenterBackground, theme.PaneBackground);
        Set(app, ThemeKeys.MenuFlyoutPresenterBorderBrush, theme.BorderBase);
        Set(app, ThemeKeys.MenuFlyoutItemBackground, theme.PaneBackground);
        Set(app, ThemeKeys.MenuFlyoutItemBackgroundPointerOver, theme.SurfaceHover);
        Set(app, ThemeKeys.MenuFlyoutItemBackgroundPressed, theme.SurfacePressed);
        Set(app, ThemeKeys.MenuFlyoutItemForeground, theme.AppText);
        Set(app, ThemeKeys.MenuFlyoutItemForegroundPointerOver, theme.AppText);
        Set(app, ThemeKeys.MenuFlyoutItemForegroundPressed, theme.AppText);

        // Focus visual
        Set(app, ThemeKeys.FocusStrokeColorOuter, theme.FocusBorder);
        Set(app, ThemeKeys.FocusStrokeColorInner, theme.SurfaceBackground);
    }

    private static void Set(Application app, string key, string hex)
    {
        var brush = new SolidColorBrush(Color.Parse(hex));
        app.Resources[key] = brush;
    }

    private static void SetColor(Application app, string key, string hex)
    {
        app.Resources[key] = Color.Parse(hex);
    }
}
