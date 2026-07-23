using Avalonia.Media;
using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Services;

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

    public void ApplyUiFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption variant)
    {
        ThemeService.ApplyUiFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);
    }

    public void ApplyFileListFontSize(double value)
    {
        ThemeService.ApplyFileListFontSize(value);
    }

    public void ApplyTerminalFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption variant)
    {
        ThemeService.ApplyTerminalFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);
    }



    public void ApplyCodeFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption variant)
    {
        ThemeService.ApplyCodeFont(new FontFamily(fontFamily.ResourceUri), variant.FontWeight, variant.FontStyle);
    }

    public void ApplyScrollBars(bool show)
    {
        ThemeService.ApplyScrollBars(show);
    }
}
