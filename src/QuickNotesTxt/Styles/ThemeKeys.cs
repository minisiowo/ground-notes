namespace QuickNotesTxt.Styles;

/// <summary>
/// Compile-time constants for all resource keys used by <see cref="ThemeService"/>.
/// Using these instead of bare string literals catches typos at build time.
/// </summary>
public static class ThemeKeys
{
    // ── App-level brushes ──────────────────────────────────────
    public const string AppBackgroundBrush = "AppBackgroundBrush";
    public const string PaneBackgroundBrush = "PaneBackgroundBrush";
    public const string SurfaceBackgroundBrush = "SurfaceBackgroundBrush";
    public const string SurfaceHoverBrush = "SurfaceHoverBrush";
    public const string SurfacePressedBrush = "SurfacePressedBrush";
    public const string SurfaceRaisedBrush = "SurfaceRaisedBrush";
    public const string SelectionBackgroundBrush = "SelectionBackgroundBrush";
    public const string SelectionBorderBrush = "SelectionBorderBrush";
    public const string TextSelectionBrush = "TextSelectionBrush";
    public const string EditorTextSelectionBrush = "EditorTextSelectionBrush";
    public const string BorderBrushBase = "BorderBrushBase";
    public const string FocusBorderBrush = "FocusBorderBrush";
    public const string PrimaryTextBrush = "PrimaryTextBrush";
    public const string SecondaryTextBrush = "SecondaryTextBrush";
    public const string MutedTextBrush = "MutedTextBrush";
    public const string PlaceholderTextBrush = "PlaceholderTextBrush";
    public const string EditorTextBrush = "EditorTextBrush";
    public const string AppTextBrush = "AppTextBrush";
    public const string TitleBarButtonHoverBrush = "TitleBarButtonHoverBrush";
    public const string TitleBarCloseHoverBrush = "TitleBarCloseHoverBrush";

    // ── Font size resources ────────────────────────────────────
    public const string AppFontSize = "AppFontSize";
    public const string AppFontSizeSmall = "AppFontSizeSmall";
    public const string AppFontSizeLarge = "AppFontSizeLarge";

    // ── FluentTheme colors ─────────────────────────────────────
    public const string SystemAccentColor = "SystemAccentColor";
    public const string SystemAccentColorDark1 = "SystemAccentColorDark1";
    public const string SystemAccentColorDark2 = "SystemAccentColorDark2";
    public const string SystemAccentColorLight1 = "SystemAccentColorLight1";
    public const string SystemAccentColorLight2 = "SystemAccentColorLight2";
    public const string SystemAccentColorLight3 = "SystemAccentColorLight3";

    // ── Button ─────────────────────────────────────────────────
    public const string ButtonBackground = "ButtonBackground";
    public const string ButtonBackgroundPointerOver = "ButtonBackgroundPointerOver";
    public const string ButtonBackgroundPressed = "ButtonBackgroundPressed";
    public const string ButtonForeground = "ButtonForeground";
    public const string ButtonForegroundPointerOver = "ButtonForegroundPointerOver";
    public const string ButtonForegroundPressed = "ButtonForegroundPressed";
    public const string ButtonBorderBrush = "ButtonBorderBrush";
    public const string ButtonBorderBrushPointerOver = "ButtonBorderBrushPointerOver";
    public const string ButtonBorderBrushPressed = "ButtonBorderBrushPressed";

    // ── TextBox ────────────────────────────────────────────────
    public const string TextControlBackground = "TextControlBackground";
    public const string TextControlBackgroundPointerOver = "TextControlBackgroundPointerOver";
    public const string TextControlBackgroundFocused = "TextControlBackgroundFocused";
    public const string TextControlForeground = "TextControlForeground";
    public const string TextControlForegroundPointerOver = "TextControlForegroundPointerOver";
    public const string TextControlForegroundFocused = "TextControlForegroundFocused";
    public const string TextControlBorderBrush = "TextControlBorderBrush";
    public const string TextControlBorderBrushPointerOver = "TextControlBorderBrushPointerOver";
    public const string TextControlBorderBrushFocused = "TextControlBorderBrushFocused";
    public const string TextControlPlaceholderForeground = "TextControlPlaceholderForeground";
    public const string TextControlPlaceholderForegroundPointerOver = "TextControlPlaceholderForegroundPointerOver";
    public const string TextControlPlaceholderForegroundFocused = "TextControlPlaceholderForegroundFocused";
    public const string TextControlSelectionHighlightColor = "TextControlSelectionHighlightColor";

    // ── ComboBox ───────────────────────────────────────────────
    public const string ComboBoxBackground = "ComboBoxBackground";
    public const string ComboBoxBackgroundPointerOver = "ComboBoxBackgroundPointerOver";
    public const string ComboBoxBackgroundPressed = "ComboBoxBackgroundPressed";
    public const string ComboBoxForeground = "ComboBoxForeground";
    public const string ComboBoxForegroundPointerOver = "ComboBoxForegroundPointerOver";
    public const string ComboBoxBorderBrush = "ComboBoxBorderBrush";
    public const string ComboBoxBorderBrushPointerOver = "ComboBoxBorderBrushPointerOver";
    public const string ComboBoxBorderBrushPressed = "ComboBoxBorderBrushPressed";
    public const string ComboBoxDropDownBackground = "ComboBoxDropDownBackground";
    public const string ComboBoxDropDownBorderBrush = "ComboBoxDropDownBorderBrush";
    public const string ComboBoxDropDownForeground = "ComboBoxDropDownForeground";
    public const string ComboBoxDropdownBorderPadding = "ComboBoxDropdownBorderPadding";
    public const string ComboBoxDropdownContentMargin = "ComboBoxDropdownContentMargin";
    public const string ComboBoxItemForeground = "ComboBoxItemForeground";
    public const string ComboBoxItemForegroundSelected = "ComboBoxItemForegroundSelected";
    public const string ComboBoxItemForegroundPointerOver = "ComboBoxItemForegroundPointerOver";
    public const string ComboBoxItemBackgroundPointerOver = "ComboBoxItemBackgroundPointerOver";
    public const string ComboBoxItemBackgroundSelected = "ComboBoxItemBackgroundSelected";
    public const string ComboBoxItemBackgroundSelectedPointerOver = "ComboBoxItemBackgroundSelectedPointerOver";

    // ── ListBoxItem / ListView ─────────────────────────────────
    public const string ListViewItemBackgroundPointerOver = "ListViewItemBackgroundPointerOver";
    public const string ListViewItemBackgroundSelected = "ListViewItemBackgroundSelected";
    public const string ListViewItemBackgroundSelectedPointerOver = "ListViewItemBackgroundSelectedPointerOver";
    public const string ListViewItemForeground = "ListViewItemForeground";
    public const string ListViewItemForegroundPointerOver = "ListViewItemForegroundPointerOver";
    public const string ListViewItemForegroundSelected = "ListViewItemForegroundSelected";

    // ── MenuItem / ContextMenu ─────────────────────────────────
    public const string MenuFlyoutPresenterBackground = "MenuFlyoutPresenterBackground";
    public const string MenuFlyoutPresenterBorderBrush = "MenuFlyoutPresenterBorderBrush";
    public const string MenuFlyoutItemBackground = "MenuFlyoutItemBackground";
    public const string MenuFlyoutItemBackgroundPointerOver = "MenuFlyoutItemBackgroundPointerOver";
    public const string MenuFlyoutItemBackgroundPressed = "MenuFlyoutItemBackgroundPressed";
    public const string MenuFlyoutItemForeground = "MenuFlyoutItemForeground";
    public const string MenuFlyoutItemForegroundPointerOver = "MenuFlyoutItemForegroundPointerOver";
    public const string MenuFlyoutItemForegroundPressed = "MenuFlyoutItemForegroundPressed";

    // ── Focus visual ───────────────────────────────────────────
    public const string FocusStrokeColorOuter = "FocusStrokeColorOuter";
    public const string FocusStrokeColorInner = "FocusStrokeColorInner";
}
