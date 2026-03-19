using Avalonia.Media;
using QuickNotesTxt.Models;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Services;

public sealed class AppAppearanceService : IAppAppearanceService
{
    public void ApplyTheme(AppTheme theme)
    {
        ThemeService.Apply(theme);
    }

    public void ApplyUiFontSize(double value)
    {
        ThemeService.ApplyUiFontSize(value);
    }

    public void ApplyEditorIndentSize(int indentSize)
    {
        ThemeService.ApplyEditorIndentSize(EditorDisplaySettings.NormalizeIndentSize(indentSize));
    }

    public void ApplyEditorLineHeight(double lineHeightFactor)
    {
        ThemeService.ApplyEditorLineHeight(EditorDisplaySettings.NormalizeLineHeightFactor(lineHeightFactor));
    }

    public void ApplyTerminalFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption variant)
    {
        ThemeService.ApplyTerminalFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);
    }

    public void ApplySidebarFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption variant)
    {
        ThemeService.ApplySidebarFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);
    }

    public void ApplyCodeFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption variant)
    {
        ThemeService.ApplyCodeFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);
    }
}
