using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace GroundNotes.Styles;

/// <summary>
/// Applies an <see cref="AppTheme"/> to the running application by updating
/// <see cref="Application.Resources"/> with <see cref="SolidColorBrush"/> instances.
/// Because styles use <c>DynamicResource</c>, changes take effect immediately.
/// Also overrides FluentTheme internal resources for full consistency.
/// </summary>
public static class ThemeService
{
    public static void ApplySidebarFont(FontFamily fontFamily)
        => ApplySidebarFont(fontFamily, FontWeight.Normal, FontStyle.Normal);

    public static void ApplySidebarFont(FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        SafeSetFont(app, ThemeKeys.SidebarFont, fontFamily);
        SetValue(app, ThemeKeys.SidebarFontWeight, fontWeight);
        SetValue(app, ThemeKeys.SidebarFontStyle, fontStyle);
    }

    public static void ApplyTerminalFont(FontFamily fontFamily)
        => ApplyTerminalFont(fontFamily, FontWeight.Normal, FontStyle.Normal);

    public static void ApplyCodeFont(FontFamily fontFamily)
        => ApplyCodeFont(fontFamily, FontWeight.Normal, FontStyle.Normal);

    public static void ApplyCodeFont(FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        SafeSetFont(app, ThemeKeys.CodeFont, fontFamily);
        SetValue(app, ThemeKeys.CodeFontWeight, fontWeight);
        SetValue(app, ThemeKeys.CodeFontStyle, fontStyle);
    }

    public static void ApplyTerminalFont(FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        SafeSetFont(app, ThemeKeys.TerminalFont, fontFamily);
        SetValue(app, ThemeKeys.TerminalFontWeight, fontWeight);
        SetValue(app, ThemeKeys.TerminalFontStyle, fontStyle);
    }

    private static void SafeSetFont(Application app, string key, FontFamily fontFamily)
    {
        try
        {
            // Try to access the font to see if it's valid. 
            // This might not catch everything if Avalonia defers loading,
            // but it's a first line of defense.
            _ = new Typeface(fontFamily).GlyphTypeface;
            SetValue(app, key, fontFamily);
        }
        catch
        {
            // Fallback to default system font
            SetValue(app, key, FontFamily.Default);
        }
    }

    public static void ApplyUiFontSize(double fontSize)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        SetValue(app, ThemeKeys.AppFontSize, fontSize);
        SetValue(app, ThemeKeys.AppFontSizeSmall, Math.Max(9d, fontSize - 1d));
        SetValue(app, ThemeKeys.AppFontSizeLarge, fontSize + 2d);
    }

    public static void ApplyEditorIndentSize(int indentSize)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        SetValue(app, ThemeKeys.EditorIndentationSize, indentSize);
    }

    public static void ApplyEditorLineHeight(double lineHeightFactor)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        SetValue(app, ThemeKeys.EditorLineHeightFactor, lineHeightFactor);
    }

    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null) return;
        var tokens = ThemeBuilder.BuildTokens(theme);
        var resources = ThemeBuilder.BuildResources(theme);

        // Match FluentTheme base variant to our theme
        app.RequestedThemeVariant = theme.IsLight
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        // ── App-level brush resources ────────────────────────
        foreach (var entry in resources.Brushes)
        {
            SetBrush(app, entry.Key, entry.Value);
        }

        // ── Override FluentTheme internal Color resources ────
        // These are Color values (not brushes) used by FluentTheme control templates
        foreach (var entry in resources.Colors)
        {
            SetColor(app, entry.Key, entry.Value);
        }

        // ── Override FluentTheme internal Brush resources ────
        // Button
        Set(app, ThemeKeys.ButtonBackground, tokens.SurfaceBackground);
        Set(app, ThemeKeys.ButtonBackgroundPointerOver, tokens.SurfaceHover);
        Set(app, ThemeKeys.ButtonBackgroundPressed, tokens.SurfacePressed);
        Set(app, ThemeKeys.ButtonForeground, tokens.AppText);
        Set(app, ThemeKeys.ButtonForegroundPointerOver, tokens.AppText);
        Set(app, ThemeKeys.ButtonForegroundPressed, tokens.AppText);
        Set(app, ThemeKeys.ButtonBorderBrush, tokens.BorderBase);
        Set(app, ThemeKeys.ButtonBorderBrushPointerOver, tokens.FocusBorder);
        Set(app, ThemeKeys.ButtonBorderBrushPressed, tokens.FocusBorder);

        // TextBox
        Set(app, ThemeKeys.TextControlBackground, tokens.SurfaceBackground);
        Set(app, ThemeKeys.TextControlBackgroundPointerOver, tokens.SurfaceBackground);
        Set(app, ThemeKeys.TextControlBackgroundFocused, tokens.SurfaceBackground);
        Set(app, ThemeKeys.TextControlForeground, tokens.AppText);
        Set(app, ThemeKeys.TextControlForegroundPointerOver, tokens.AppText);
        Set(app, ThemeKeys.TextControlForegroundFocused, tokens.AppText);
        Set(app, ThemeKeys.TextControlBorderBrush, tokens.BorderBase);
        Set(app, ThemeKeys.TextControlBorderBrushPointerOver, tokens.FocusBorder);
        Set(app, ThemeKeys.TextControlBorderBrushFocused, tokens.FocusBorder);
        Set(app, ThemeKeys.TextControlPlaceholderForeground, tokens.PlaceholderText);
        Set(app, ThemeKeys.TextControlPlaceholderForegroundPointerOver, tokens.PlaceholderText);
        Set(app, ThemeKeys.TextControlPlaceholderForegroundFocused, tokens.PlaceholderText);
        Set(app, ThemeKeys.TextControlSelectionHighlightColor, tokens.TextSelectionBrush);

        // ComboBox
        Set(app, ThemeKeys.ComboBoxBackground, tokens.SurfaceBackground);
        Set(app, ThemeKeys.ComboBoxBackgroundPointerOver, tokens.SurfaceBackground);
        Set(app, ThemeKeys.ComboBoxBackgroundPressed, tokens.SurfacePressed);
        Set(app, ThemeKeys.ComboBoxForeground, tokens.AppText);
        Set(app, ThemeKeys.ComboBoxForegroundPointerOver, tokens.AppText);
        Set(app, ThemeKeys.ComboBoxBorderBrush, tokens.BorderBase);
        Set(app, ThemeKeys.ComboBoxBorderBrushPointerOver, tokens.FocusBorder);
        Set(app, ThemeKeys.ComboBoxBorderBrushPressed, tokens.FocusBorder);
        Set(app, ThemeKeys.ComboBoxDropDownBackground, tokens.PaneBackground);
        Set(app, ThemeKeys.ComboBoxDropDownBorderBrush, tokens.BorderBase);
        Set(app, ThemeKeys.ComboBoxDropDownForeground, tokens.AppText);
        SetValue(app, ThemeKeys.ComboBoxDropdownBorderPadding, new Avalonia.Thickness(0));
        SetValue(app, ThemeKeys.ComboBoxDropdownContentMargin, new Avalonia.Thickness(0));
        Set(app, ThemeKeys.ComboBoxItemForeground, tokens.AppText);
        Set(app, ThemeKeys.ComboBoxItemForegroundSelected, tokens.AppText);
        Set(app, ThemeKeys.ComboBoxItemForegroundPointerOver, tokens.AppText);
        Set(app, ThemeKeys.ComboBoxItemBackgroundPointerOver, tokens.SurfaceRaised);
        Set(app, ThemeKeys.ComboBoxItemBackgroundSelected, tokens.SelectionBackground);
        Set(app, ThemeKeys.ComboBoxItemBackgroundSelectedPointerOver, tokens.SelectionBackground);

        // ListBoxItem (FluentTheme uses ListViewItem resources)
        Set(app, ThemeKeys.ListViewItemBackgroundPointerOver, tokens.SurfaceRaised);
        Set(app, ThemeKeys.ListViewItemBackgroundSelected, tokens.SelectionBackground);
        Set(app, ThemeKeys.ListViewItemBackgroundSelectedPointerOver, tokens.SelectionBackground);
        Set(app, ThemeKeys.ListViewItemForeground, tokens.AppText);
        Set(app, ThemeKeys.ListViewItemForegroundPointerOver, tokens.AppText);
        Set(app, ThemeKeys.ListViewItemForegroundSelected, tokens.AppText);

        // MenuItem / ContextMenu
        Set(app, ThemeKeys.MenuFlyoutPresenterBackground, tokens.PaneBackground);
        Set(app, ThemeKeys.MenuFlyoutPresenterBorderBrush, tokens.BorderBase);
        Set(app, ThemeKeys.MenuFlyoutItemBackground, tokens.PaneBackground);
        Set(app, ThemeKeys.MenuFlyoutItemBackgroundPointerOver, tokens.SurfaceHover);
        Set(app, ThemeKeys.MenuFlyoutItemBackgroundPressed, tokens.SurfacePressed);
        Set(app, ThemeKeys.MenuFlyoutItemForeground, tokens.AppText);
        Set(app, ThemeKeys.MenuFlyoutItemForegroundPointerOver, tokens.AppText);
        Set(app, ThemeKeys.MenuFlyoutItemForegroundPressed, tokens.AppText);

        // Focus visual
        Set(app, ThemeKeys.FocusStrokeColorOuter, tokens.FocusBorder);
        Set(app, ThemeKeys.FocusStrokeColorInner, tokens.SurfaceBackground);
    }

    private static void SetBrush(Application app, string key, SolidColorBrush brush)
    {
        if (app.Resources.TryGetValue(key, out var existing)
            && existing is SolidColorBrush existingBrush
            && existingBrush.Color.Equals(brush.Color))
        {
            return;
        }

        app.Resources[key] = brush;
    }

    private static void Set(Application app, string key, string hex)
    {
        var color = Color.Parse(hex);
        if (app.Resources.TryGetValue(key, out var existing)
            && existing is SolidColorBrush existingBrush
            && existingBrush.Color.Equals(color))
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        app.Resources[key] = brush;
    }

    private static void SetColor(Application app, string key, string hex)
    {
        SetColor(app, key, Color.Parse(hex));
    }

    private static void SetColor(Application app, string key, Color color)
    {
        if (app.Resources.TryGetValue(key, out var existing) && existing is Color existingColor && existingColor.Equals(color))
        {
            return;
        }

        app.Resources[key] = color;
    }

    private static void SetValue<T>(Application app, string key, T value)
    {
        if (app.Resources.TryGetValue(key, out var existing) && existing is T typedExisting && EqualityComparer<T>.Default.Equals(typedExisting, value))
        {
            return;
        }

        app.Resources[key] = value;
    }
}
