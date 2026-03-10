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
    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;

        // Match FluentTheme base variant to our theme
        app.RequestedThemeVariant = theme == AppTheme.Light
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        // ── App-level brush resources ────────────────────────
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

        // ── Override FluentTheme internal Color resources ────
        // These are Color values (not brushes) used by FluentTheme control templates
        SetColor(app, "SystemAccentColor", theme.SelectionBorder);
        SetColor(app, "SystemAccentColorDark1", theme.SelectionBorder);
        SetColor(app, "SystemAccentColorDark2", theme.SelectionBackground);
        SetColor(app, "SystemAccentColorLight1", theme.FocusBorder);
        SetColor(app, "SystemAccentColorLight2", theme.FocusBorder);
        SetColor(app, "SystemAccentColorLight3", theme.FocusBorder);

        // ── Override FluentTheme internal Brush resources ────
        // Button
        Set(app, "ButtonBackground", theme.SurfaceBackground);
        Set(app, "ButtonBackgroundPointerOver", theme.SurfaceHover);
        Set(app, "ButtonBackgroundPressed", theme.SurfacePressed);
        Set(app, "ButtonForeground", theme.AppText);
        Set(app, "ButtonForegroundPointerOver", theme.AppText);
        Set(app, "ButtonForegroundPressed", theme.AppText);
        Set(app, "ButtonBorderBrush", theme.BorderBase);
        Set(app, "ButtonBorderBrushPointerOver", theme.FocusBorder);
        Set(app, "ButtonBorderBrushPressed", theme.FocusBorder);

        // TextBox
        Set(app, "TextControlBackground", theme.SurfaceBackground);
        Set(app, "TextControlBackgroundPointerOver", theme.SurfaceBackground);
        Set(app, "TextControlBackgroundFocused", theme.SurfaceBackground);
        Set(app, "TextControlForeground", theme.AppText);
        Set(app, "TextControlForegroundPointerOver", theme.AppText);
        Set(app, "TextControlForegroundFocused", theme.AppText);
        Set(app, "TextControlBorderBrush", theme.BorderBase);
        Set(app, "TextControlBorderBrushPointerOver", theme.FocusBorder);
        Set(app, "TextControlBorderBrushFocused", theme.FocusBorder);
        Set(app, "TextControlPlaceholderForeground", theme.PlaceholderText);
        Set(app, "TextControlPlaceholderForegroundPointerOver", theme.PlaceholderText);
        Set(app, "TextControlPlaceholderForegroundFocused", theme.PlaceholderText);
        Set(app, "TextControlSelectionHighlightColor", theme.TextSelectionBrush);

        // ComboBox
        Set(app, "ComboBoxBackground", theme.SurfaceBackground);
        Set(app, "ComboBoxBackgroundPointerOver", theme.SurfaceBackground);
        Set(app, "ComboBoxBackgroundPressed", theme.SurfacePressed);
        Set(app, "ComboBoxForeground", theme.AppText);
        Set(app, "ComboBoxForegroundPointerOver", theme.AppText);
        Set(app, "ComboBoxBorderBrush", theme.BorderBase);
        Set(app, "ComboBoxBorderBrushPointerOver", theme.FocusBorder);
        Set(app, "ComboBoxBorderBrushPressed", theme.FocusBorder);
        Set(app, "ComboBoxDropDownBackground", theme.PaneBackground);
        Set(app, "ComboBoxDropDownBorderBrush", theme.BorderBase);
        Set(app, "ComboBoxDropDownForeground", theme.AppText);
        app.Resources["ComboBoxDropdownBorderPadding"] = new Avalonia.Thickness(0);
        app.Resources["ComboBoxDropdownContentMargin"] = new Avalonia.Thickness(0);
        Set(app, "ComboBoxItemForeground", theme.AppText);
        Set(app, "ComboBoxItemForegroundSelected", theme.AppText);
        Set(app, "ComboBoxItemForegroundPointerOver", theme.AppText);
        Set(app, "ComboBoxItemBackgroundPointerOver", theme.SurfaceRaised);
        Set(app, "ComboBoxItemBackgroundSelected", theme.SelectionBackground);
        Set(app, "ComboBoxItemBackgroundSelectedPointerOver", theme.SelectionBackground);

        // ListBoxItem (FluentTheme uses ListViewItem resources)
        Set(app, "ListViewItemBackgroundPointerOver", theme.SurfaceRaised);
        Set(app, "ListViewItemBackgroundSelected", theme.SelectionBackground);
        Set(app, "ListViewItemBackgroundSelectedPointerOver", theme.SelectionBackground);
        Set(app, "ListViewItemForeground", theme.AppText);
        Set(app, "ListViewItemForegroundPointerOver", theme.AppText);
        Set(app, "ListViewItemForegroundSelected", theme.AppText);

        // MenuItem / ContextMenu
        Set(app, "MenuFlyoutPresenterBackground", theme.PaneBackground);
        Set(app, "MenuFlyoutPresenterBorderBrush", theme.BorderBase);
        Set(app, "MenuFlyoutItemBackground", theme.PaneBackground);
        Set(app, "MenuFlyoutItemBackgroundPointerOver", theme.SurfaceHover);
        Set(app, "MenuFlyoutItemBackgroundPressed", theme.SurfacePressed);
        Set(app, "MenuFlyoutItemForeground", theme.AppText);
        Set(app, "MenuFlyoutItemForegroundPointerOver", theme.AppText);
        Set(app, "MenuFlyoutItemForegroundPressed", theme.AppText);

        // Focus visual
        Set(app, "FocusStrokeColorOuter", theme.FocusBorder);
        Set(app, "FocusStrokeColorInner", theme.SurfaceBackground);
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
