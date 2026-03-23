using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Services;

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
