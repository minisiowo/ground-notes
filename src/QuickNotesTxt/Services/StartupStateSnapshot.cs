using QuickNotesTxt.Models;
using QuickNotesTxt.Styles;

namespace QuickNotesTxt.Services;

public sealed record StartupStateSnapshot(
    AppSettings Settings,
    WindowLayout? Layout,
    AppTheme Theme,
    IReadOnlyList<BundledFontFamilyOption> Fonts,
    BundledFontFamilyOption TerminalFontFamily,
    BundledFontVariantOption TerminalFontVariant,
    BundledFontFamilyOption SidebarFontFamily,
    BundledFontVariantOption SidebarFontVariant,
    BundledFontFamilyOption CodeFontFamily,
    BundledFontVariantOption CodeFontVariant,
    double UiFontSize);
