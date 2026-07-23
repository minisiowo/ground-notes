using GroundNotes.Models;
using GroundNotes.Styles;

namespace GroundNotes.Services;

public sealed record StartupStateSnapshot(
    AppSettings Settings,
    WindowLayout? Layout,
    AppTheme Theme,
    IReadOnlyList<BundledFontFamilyOption> Fonts,
    BundledFontFamilyOption UiFontFamily,
    BundledFontVariantOption UiFontVariant,
    BundledFontFamilyOption TerminalFontFamily,
    BundledFontVariantOption TerminalFontVariant,
    BundledFontFamilyOption CodeFontFamily,
    BundledFontVariantOption CodeFontVariant,
    double UiFontSize,
    double FileListFontSize);
