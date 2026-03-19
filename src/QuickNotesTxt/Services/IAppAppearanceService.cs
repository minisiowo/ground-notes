using QuickNotesTxt.Models;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Services;

public interface IAppAppearanceService
{
    void ApplyTheme(AppTheme theme);

    void ApplyUiFontSize(double fontSize);

    void ApplyEditorIndentSize(int indentSize);

    void ApplyEditorLineHeight(double lineHeightFactor);

    void ApplyTerminalFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant);

    void ApplySidebarFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant);

    void ApplyCodeFont(BundledFontFamilyOption fontFamily, BundledFontVariantOption fontVariant);
}
