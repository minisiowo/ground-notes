using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Services;

public interface IAppAppearanceService
{
    void ApplyTheme(AppTheme theme);

    void ApplyUiFontSize(double fontSize);

    void ApplyUiFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant);

    void ApplyFileListFontSize(double fontSize);

    void ApplyTerminalFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant);

    void ApplyCodeFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant);

    void ApplyScrollBars(bool show);
}
